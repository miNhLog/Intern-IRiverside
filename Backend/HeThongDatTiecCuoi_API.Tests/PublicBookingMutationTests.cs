using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

/// <summary>
/// UC15 self-service cancellation/rescheduling rules verified against the InMemory provider.
/// SQL Server row locks and Serializable isolation are skipped by the controller for non-relational
/// providers, so these tests cover business eligibility, state transitions and audit/history only.
/// </summary>
public sealed class PublicBookingMutationTests : IDisposable
{
    private const string BookingCode = "DT-20261027-UC15A";
    private const string ExpiredBookingCode = "DT-20261027-UC15B";
    private const string Phone = "0901234567";

    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly ApplicationDbContext _db;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DateTime _bookedAt = DateTime.Now.AddHours(-2);

    public PublicBookingMutationTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName, _root)
            .Options;
        _db = new ApplicationDbContext(_options);
        Seed();
    }

    [Fact]
    public async Task CancelHold_EligiblePendingBooking_CancelsReleasesScheduleAndAudits()
    {
        var result = await Controller().CancelHold(1, CancelRequest());

        Assert.IsType<OkObjectResult>(result);

        await using var verify = NewContext();
        var booking = await verify.WeddingBookings.AsNoTracking()
            .Include(item => item.Status)
            .Include(item => item.HallSchedule).ThenInclude(item => item.Status)
            .SingleAsync(item => item.BookingId == 1);
        Assert.Equal(BookingStatusCodes.Cancelled, booking.Status.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(booking.CancellationReason));
        Assert.Equal(_bookedAt, booking.BookedAt);
        Assert.Equal(HallScheduleStatusCodes.Available, booking.HallSchedule.Status.StatusCode);

        var audit = Assert.Single(await verify.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal("BOOKING_CANCELLED", audit.Action);
        Assert.Null(audit.UserId);
        Assert.Equal(1L, audit.EntityId);
    }

    [Fact]
    public async Task CancelHold_RepeatedRequest_ReturnsConflictWithoutSecondMutation()
    {
        Assert.IsType<OkObjectResult>(await Controller().CancelHold(1, CancelRequest()));

        var repeated = await Controller().CancelHold(1, CancelRequest());

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(repeated).StatusCode);
        await using var verify = NewContext();
        Assert.Single(await verify.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(1, await verify.WeddingBookings.AsNoTracking().CountAsync(item =>
            item.BookingId == 1 && item.StatusId == 805));
    }

    [Fact]
    public async Task CancelHold_WithContract_ReturnsConflictAndKeepsBookingAndSchedule()
    {
        _db.Contracts.Add(new Contract
        {
            ContractId = 1,
            BookingId = 1,
            ContractCode = "HD-UC15-TEST",
            CreatedDate = DateTime.Now,
            TotalValue = 1000m,
            StatusId = 902
        });
        await _db.SaveChangesAsync();

        var result = await Controller().CancelHold(1, CancelRequest());

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        await using var verify = NewContext();
        var booking = await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 1);
        Assert.Equal(801, booking.StatusId);
        var schedule = await verify.HallSchedules.AsNoTracking().SingleAsync(item => item.HallScheduleId == 1);
        Assert.Equal(302, schedule.StatusId);
        Assert.Empty(await verify.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CancelHold_ExpiredHold_ReturnsConflictAndDoesNotReviveBooking()
    {
        var result = await Controller().CancelHold(2, CancelRequest(ExpiredBookingCode));

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        await using var verify = NewContext();
        Assert.Equal(801, (await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 2)).StatusId);
        Assert.Equal(302, (await verify.HallSchedules.AsNoTracking().SingleAsync(item => item.HallScheduleId == 2)).StatusId);
    }

    [Fact]
    public async Task CancelHold_WithWrongPhone_ReturnsNotFoundAndKeepsData()
    {
        var request = new PublicBookingCancelHoldRequestDto
        {
            BookingCode = BookingCode,
            PhoneNumber = "0900000000",
            Reason = "Thử hủy"
        };

        var result = await Controller().CancelHold(1, request);

        Assert.IsType<NotFoundObjectResult>(result);
        await using var verify = NewContext();
        Assert.Equal(801, (await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 1)).StatusId);
    }

    [Fact]
    public async Task Reschedule_EligibleBooking_MovesScheduleKeepsSnapshotAndRecordsApprovedHistory()
    {
        var targetDate = DateTime.Today.AddDays(60);

        var result = await Controller().Reschedule(1, RescheduleRequest(targetDate, HallShiftNames.Dinner));

        Assert.IsType<OkObjectResult>(result);
        await using var verify = NewContext();
        var booking = await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 1);
        Assert.NotEqual(1, booking.HallScheduleId);
        Assert.Equal(_bookedAt, booking.BookedAt);
        Assert.Equal(300m, booking.FinalMenuPrice!.Value);
        Assert.Equal(500m, booking.FinalHallPrice!.Value);
        Assert.Equal(1200m, booking.EstimatedTotal!.Value);

        var newSchedule = await verify.HallSchedules.AsNoTracking().SingleAsync(item => item.HallScheduleId == booking.HallScheduleId);
        Assert.Equal(targetDate, newSchedule.Date);
        Assert.Equal(HallShiftNames.Dinner, newSchedule.Shift);
        Assert.Equal(1, newSchedule.HallId);
        Assert.Equal(302, newSchedule.StatusId);

        var oldSchedule = await verify.HallSchedules.AsNoTracking().SingleAsync(item => item.HallScheduleId == 1);
        Assert.Equal(301, oldSchedule.StatusId);

        var change = Assert.Single(await verify.WeddingScheduleChanges.AsNoTracking().ToListAsync());
        Assert.Equal(1, change.OldHallScheduleId);
        Assert.Equal(booking.HallScheduleId, change.NewHallScheduleId);
        Assert.Equal(1302, change.StatusId);
        Assert.NotNull(change.ProcessedAt);

        var audit = Assert.Single(await verify.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal("BOOKING_RESCHEDULED", audit.Action);
        Assert.Null(audit.UserId);
    }

    [Fact]
    public async Task Reschedule_ToLockedSchedule_ReturnsConflictWithoutChanges()
    {
        var result = await Controller().Reschedule(1, RescheduleRequest(LockedTargetDate, HallShiftNames.Dinner));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        await using var verify = NewContext();
        Assert.Equal(1, (await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 1)).HallScheduleId);
        Assert.Equal(302, (await verify.HallSchedules.AsNoTracking().SingleAsync(item => item.HallScheduleId == 1)).StatusId);
        Assert.Empty(await verify.WeddingScheduleChanges.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Reschedule_ToOccupiedSchedule_ReturnsConflict()
    {
        // Booking 2 still occupies hall 1 on its own date/shift.
        var occupiedDate = DateTime.Today.AddDays(90);
        var schedule = await _db.HallSchedules.SingleAsync(item => item.HallScheduleId == 2);
        schedule.Date = occupiedDate;
        schedule.Shift = HallShiftNames.Lunch;
        await _db.SaveChangesAsync();

        var result = await Controller().Reschedule(1, RescheduleRequest(occupiedDate, HallShiftNames.Lunch));

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        await using var verify = NewContext();
        Assert.Equal(1, (await verify.WeddingBookings.AsNoTracking().SingleAsync(item => item.BookingId == 1)).HallScheduleId);
        Assert.Empty(await verify.WeddingScheduleChanges.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void LegacyCancelEndpoint_IsRetired()
    {
        var result = new MyBookingsController().CancelBooking(1);

        Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    private static DateTime LockedTargetDate => DateTime.Today.AddDays(61);

    private PublicBookingController Controller() => new(
        _db,
        new StatusService(_db),
        _cache,
        NullLogger<PublicBookingController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static PublicBookingCancelHoldRequestDto CancelRequest(string code = BookingCode) => new()
    {
        BookingCode = code,
        PhoneNumber = Phone,
        Reason = "Không sắp xếp được ngày tổ chức"
    };

    private static PublicBookingRescheduleRequestDto RescheduleRequest(DateTime newDate, string newShift) => new()
    {
        BookingCode = BookingCode,
        PhoneNumber = Phone,
        NewDate = newDate,
        NewShift = newShift,
        Reason = "Đổi ngày tổ chức"
    };

    private ApplicationDbContext NewContext() => new(_options);

    private void Seed()
    {
        var existing = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        var pending = new Status { StatusId = 801, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" };
        var cancelled = new Status { StatusId = 805, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" };
        var available = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
        var booked = new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" };
        var locked = new Status { StatusId = 303, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "Đã khóa" };
        var approved = new Status { StatusId = 1302, StatusGroup = StatusGroups.ScheduleChange, StatusCode = BusinessStatusCodes.Approved, StatusName = "Đã duyệt" };
        var hallActive = new Status { StatusId = 201, StatusGroup = StatusGroups.Hall, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" };
        _db.DataStatuses.Add(existing);
        _db.Statuses.AddRange(pending, cancelled, available, booked, locked, approved, hallActive);

        var hall = new Hall
        {
            HallId = 1,
            HallCode = "S-UC15",
            HallName = "Sảnh UC15",
            MaximumCapacity = 300,
            RentalPrice = 500m,
            Status = hallActive,
            DataStatusId = 1
        };

        var primarySchedule = new HallSchedule
        {
            HallScheduleId = 1,
            Hall = hall,
            Date = new DateTime(2027, 6, 1),
            Shift = HallShiftNames.Lunch,
            Status = booked,
            DataStatusId = 1
        };
        var expiredSchedule = new HallSchedule
        {
            HallScheduleId = 2,
            Hall = hall,
            Date = new DateTime(2027, 6, 2),
            Shift = HallShiftNames.Lunch,
            Status = booked,
            DataStatusId = 1
        };
        var lockedSchedule = new HallSchedule
        {
            HallScheduleId = 3,
            Hall = hall,
            Date = LockedTargetDate,
            Shift = HallShiftNames.Dinner,
            Status = locked,
            DataStatusId = 1
        };
        _db.HallSchedules.AddRange(primarySchedule, expiredSchedule, lockedSchedule);

        _db.WeddingBookings.AddRange(
            new WeddingBooking
            {
                BookingId = 1,
                BookingCode = BookingCode,
                GuestCount = 100,
                FinalMenuPrice = 300m,
                FinalDecorationPrice = 50m,
                FinalHallPrice = 500m,
                EstimatedTotal = 1200m,
                Status = pending,
                BookedAt = _bookedAt,
                Customer = new Customer
                {
                    CustomerId = 1,
                    CustomerCode = "KH-UC15-1",
                    FullName = "Khách UC15",
                    PhoneNumber = Phone,
                    DataStatusId = 1
                },
                HallSchedule = primarySchedule
            },
            new WeddingBooking
            {
                BookingId = 2,
                BookingCode = ExpiredBookingCode,
                GuestCount = 80,
                EstimatedTotal = 900m,
                Status = pending,
                BookedAt = DateTime.Now.AddHours(-(BookingHoldExpirationService.HoldHours + 1)),
                Customer = new Customer
                {
                    CustomerId = 2,
                    CustomerCode = "KH-UC15-2",
                    FullName = "Khách UC15 hết hạn",
                    PhoneNumber = Phone,
                    DataStatusId = 1
                },
                HallSchedule = expiredSchedule
            });

        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }
}

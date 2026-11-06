using System.Security.Claims;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ManagerBookingChangesTests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    private const string DatabaseName = "ManagerBookingChangesTests";
    [Theory]
    [InlineData("[Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("Ghi chú tùy ý; [Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: 1", "UNKNOWN", null)]
    [InlineData("Ghi chú Bàn chính: 18; bàn dự phòng: 1", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: x", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: 1; [Bàn dự phòng: 1]", "UNKNOWN", null)]
    [InlineData("Ghi chú tùy ý; [Bàn dự phòng: 2]; Ngân sách dự kiến 1.000 đ; [Dịch vụ đi kèm: DV1]", "UNKNOWN", null)]
    [InlineData("Ghi chú [Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("[Bàn dự phòng: 2]; [Bàn dự phòng: 3]", "UNKNOWN", null)]
    [InlineData("[Bàn dự phòng: x]", "UNKNOWN", null)]
    [InlineData(null, "UNKNOWN", null)]
    public async Task RescheduleQuote_OnlyTrustsSinglePersistedSpareTableMarker(string? specialRequests, string capacityCheck, int? expectedSpareTables)
    {
        await using var db = CreateDatabase();
        Seed(db, specialRequests);
        var controller = CreateController(db, RoleNames.Manager, 10);

        var result = await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2
        }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(capacityCheck, quote.CapacityCheck);
        Assert.Equal(expectedSpareTables, quote.SpareTableCount);
        Assert.Equal(500m, quote.SavedHallPrice);
        Assert.Equal(900m, quote.CurrentHallPrice);
        Assert.Equal(400m, quote.PriceDifference);
    }

    [Fact]
    public async Task Reschedule_WithContract_IsRejectedWithoutChangingBooking()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(2), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task PendingExpiredBooking_CannotBeRescheduledBeforeExpirationWorkerRuns()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 800;
        booking.BookedAt = DateTime.Now.AddHours(-49);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(1), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_BOOKING_HOLD_EXPIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task Reschedule_HallChangeWithUnknownSpareTablesFailsClosed()
    {
        await using var db = CreateDatabase();
        Seed(db, "Ghi chú bàn dự phòng: không rõ");

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_CAPACITY_UNKNOWN", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task SameHallQuote_UnknownSpareTablesIsNotReportedAsCapacityPass()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1
        }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("UNKNOWN", quote.CapacityCheck);
        Assert.Null(quote.SpareTableCount);
        Assert.Contains("giữ nguyên sảnh", quote.CapacityMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reschedule_CrossHallRejectsCustomerForgedSpareTableMarkerWithoutChangingBookingOrSchedules()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var bookingBefore = await db.WeddingBookings.AsNoTracking().SingleAsync();
        var scheduleBefore = await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => new { x.HallScheduleId, x.HallId, x.Date, x.Shift, x.StatusId }).ToListAsync();

        var quoteResult = await CreateController(db, RoleNames.Manager, 10).Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2
        }, CancellationToken.None);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(quoteResult).Value);
        Assert.Equal("UNKNOWN", quote.CapacityCheck);
        Assert.Null(quote.SpareTableCount);

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_CAPACITY_UNKNOWN", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Contains("cùng sảnh", conflict.Value.GetType().GetProperty("message")!.GetValue(conflict.Value)!.ToString(), StringComparison.OrdinalIgnoreCase);
        var bookingAfter = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(bookingBefore.HallScheduleId, bookingAfter.HallScheduleId);
        Assert.Equal(bookingBefore.FinalHallPrice, bookingAfter.FinalHallPrice);
        Assert.Equal(scheduleBefore, await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId)
            .Select(x => new { x.HallScheduleId, x.HallId, x.Date, x.Shift, x.StatusId }).ToListAsync());
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Empty(await db.Contracts.ToListAsync());
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_FinanciallyLinkedBookingWithForgedMarkerLeavesBookingSchedulesContractAndPaymentUnchanged()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        db.Payments.Add(new Payment { PaymentId = 1, ContractId = 1, PaymentType = "Đặt cọc", Amount = 100m, PaymentDate = DateTime.Today, StatusId = 1 });
        await db.SaveChangesAsync();
        var scheduleStatusBefore = await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => x.StatusId).ToListAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Equal(scheduleStatusBefore, await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => x.StatusId).ToListAsync());
        Assert.Equal("HD-1", (await db.Contracts.AsNoTracking().SingleAsync()).ContractCode);
        Assert.Equal(100m, (await db.Payments.AsNoTracking().SingleAsync()).Amount);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_TerminalBookingCannotBeMoved()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 805;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(1), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task PendingExpiredBooking_CannotBeConfirmedBeforeExpirationWorkerRuns()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 800;
        booking.BookedAt = DateTime.Now.AddHours(-49);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "10"), new Claim(ClaimTypes.Role, RoleNames.Manager)
        ], "Test"));
        var controller = new AdminBookingController(db, new StatusService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        var result = await controller.UpdateBookingStatus(1, new UpdateBookingStatusRequestDto { StatusCode = BookingStatusCodes.Confirmed });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_BOOKING_HOLD_EXPIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(BookingStatusCodes.Pending, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
    }

    [Fact]
    public async Task Reschedule_HallPriceConfirmationMustMatchFreshQuote()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");

        var controller = CreateController(db, RoleNames.Manager, 10);
        var quoted = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, QuoteRequest(2), CancellationToken.None)).Value);
        var hall = await db.Halls.SingleAsync(x => x.HallId == 2);
        hall.RentalPrice = 950m;
        await db.SaveChangesAsync();

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quoted.PriceDifference
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_PRICE_CONFIRMATION_REQUIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_ToLockedSchedule_RollsBackWithoutHistoryOrAudit()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 1, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 303, DataStatusId = 1 });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Equal(HallScheduleStatusCodes.Locked, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 2)).Status.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MovesSlotAndAuditsActorWithoutChangingFinancialSnapshot()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Statuses.Add(new Status { StatusId = 1302, StatusGroup = StatusGroups.ScheduleChange, StatusCode = BusinessStatusCodes.Approved, StatusName = "APPROVED" });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m, Reason = "Điều chỉnh lịch"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var booking = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.NotEqual(1, booking.HallScheduleId);
        Assert.Equal(500m, booking.FinalHallPrice);
        Assert.Equal(1000m, booking.EstimatedTotal);
        var change = Assert.Single(await db.WeddingScheduleChanges.AsNoTracking().ToListAsync());
        Assert.Equal(1, change.OldHallScheduleId);
        Assert.Equal(booking.HallScheduleId, change.NewHallScheduleId);
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(10, audit.UserId);
        Assert.Equal("BOOKING_RESCHEDULED_BY_STAFF", audit.Action);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 1)).Status.StatusCode);
    }

    [Fact]
    public async Task Cancel_RejectsContractAndTerminalBookings()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        await db.SaveChangesAsync();

        var withContract = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Yêu cầu hủy" }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(withContract).StatusCode);
        Assert.Equal(BookingStatusCodes.Confirmed, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);

        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 805;
        await db.SaveChangesAsync();
        var terminal = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Lặp lại" }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(terminal).StatusCode);
    }

    [Fact]
    public async Task Cancel_EligibleBookingReleasesOnlyBookedScheduleAndAuditsActor()
    {
        await using var db = CreateDatabase();
        Seed(db, null);

        var result = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Đổi kế hoạch" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var booking = await db.WeddingBookings.Include(x => x.Status).SingleAsync();
        Assert.Equal(BookingStatusCodes.Cancelled, booking.Status.StatusCode);
        Assert.Equal("Đổi kế hoạch", booking.CancellationReason);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(10, audit.UserId);
        Assert.Equal("BOOKING_CANCELLED_BY_STAFF", audit.Action);
    }

    [Fact]
    public async Task Reschedule_ToOccupiedScheduleRejectsWithoutPartialChanges()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Customers.Add(new Customer { CustomerId = 2, CustomerCode = "C2", FullName = "Khách hai", PhoneNumber = "0900000001", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 1, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 2, BookingCode = "DT-TEST-2", CustomerId = 2, HallScheduleId = 2, GuestCount = 100,
            StatusId = 801, BookedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync(x => x.BookingId == 1)).HallScheduleId);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 2)).Status.StatusCode);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task HallManager_CannotQuoteBookingOutsideActiveHallAssignment()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        AddHallManager(db, 20, 1);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Quote(1, QuoteRequest(2), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task HallManager_CanQuoteWithinActiveHallAssignment()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        AddHallManager(db, 20, 1);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Quote(1, QuoteRequest(1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Controller_RequiresManagerOrHallManagerRole()
    {
        var attribute = Assert.Single(typeof(ManagerBookingChangesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal($"{RoleNames.Manager},{RoleNames.HallManager}", attribute.Roles);
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(DatabaseName, DatabaseRoot).Options;
        using var reset = new ApplicationDbContext(options);
        reset.Database.EnsureDeleted();
        return new ApplicationDbContext(options);
    }

    private static ManagerBookingRescheduleQuoteRequestDto QuoteRequest(int hallId) => new()
    {
        NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = hallId
    };

    private static void Seed(ApplicationDbContext db, string? specialRequests)
    {
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Đang dùng" });
        db.Statuses.AddRange(
            new Status { StatusId = 201, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "AVAILABLE" },
            new Status { StatusId = 303, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "LOCKED" },
            new Status { StatusId = 1, StatusGroup = StatusGroups.Account, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 800, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" },
            new Status { StatusId = 801, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" },
            new Status { StatusId = 805, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" });
        db.Halls.AddRange(
            new Hall { HallId = 1, HallCode = "H1", HallName = "Sảnh cũ", MinimumCapacity = 100, MaximumCapacity = 400, RentalPrice = 500m, StatusId = 201, DataStatusId = 1 },
            new Hall { HallId = 2, HallCode = "H2", HallName = "Sảnh mới", MinimumCapacity = 100, MaximumCapacity = 210, RentalPrice = 900m, StatusId = 201, DataStatusId = 1 });
        db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "C1", FullName = "Khách", PhoneNumber = "0900000000", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 1, HallId = 1, Date = DateTime.Today.AddDays(60), Shift = "Ca trưa", StatusId = 301, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 1, BookingCode = "DT-TEST-1", CustomerId = 1, HallScheduleId = 1, GuestCount = 200,
            FinalHallPrice = 500m, FinalMenuPrice = 100m, EstimatedTotal = 1000m, SpecialRequests = specialRequests,
            StatusId = 801, BookedAt = DateTime.Now
        });
        db.SaveChanges();
    }

    private static void AddHallManager(ApplicationDbContext db, int userId, int hallId)
    {
        db.Roles.Add(new Role { RoleId = 3, RoleName = RoleNames.HallManager });
        db.Users.Add(new User { UserId = userId, RoleId = 3, Email = "hm@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = 30, UserId = userId, EmployeeCode = "HM030", FullName = "Hall Manager", PhoneNumber = "0910000000", DataStatusId = 1 });
        db.HallManagerAssignments.Add(new HallManagerAssignment
        {
            HallManagerAssignmentId = 1, HallId = hallId, HallManagerEmployeeId = 30,
            FromDate = DateTime.Today.AddDays(-1), ToDate = null
        });
    }

    private static ManagerBookingChangesController CreateController(ApplicationDbContext db, string role, int userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)
        ], "Test"));
        return new ManagerBookingChangesController(db, new StatusService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }
}

public sealed class ManagerBookingChangesAuthorizationTests : IClassFixture<EventCoordinationApiFactory>
{
    private readonly EventCoordinationApiFactory _factory;
    public ManagerBookingChangesAuthorizationTests(EventCoordinationApiFactory factory) => _factory = factory;

    [Fact]
    public void LegacyAdminBooking_CreatorRemainsAvailableToAdminWhileReadsAreManagerOnly()
    {
        var controllerType = typeof(AdminBookingController);
        var controllerPolicy = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal($"{RoleNames.Admin},{RoleNames.Manager}", controllerPolicy.Roles);
        Assert.Empty(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.CreateBooking))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal(RoleNames.Manager, Assert.Single(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.GetBookings))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal(RoleNames.Manager, Assert.Single(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.GetBookingDetail))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(RoleNames.Manager, 200)]
    [InlineData(RoleNames.HallManager, 401)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    [InlineData(RoleNames.Customer, 403)]
    public async Task BookingChangeApi_DeniesRolesOutsideManagerAndHallManager(string? role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.GetAsync("/api/manager/bookings");

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Manager, 400)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.HallManager, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    [InlineData(RoleNames.Customer, 403)]
    public async Task LegacyStatusMutation_CannotBeUsedByAdminOrNonManagerRoles(string role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.PutAsJsonAsync("/api/admin/bookings/1/status", new { statusCode = "CANCELLED" });

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/bookings", RoleNames.Admin, 403)]
    [InlineData("/api/admin/bookings/1", RoleNames.Admin, 403)]
    [InlineData("/api/admin/bookings", RoleNames.Manager, 200)]
    [InlineData("/api/admin/bookings/1", RoleNames.Manager, 0)]
    [InlineData("/api/admin/bookings", RoleNames.HallManager, 403)]
    [InlineData("/api/admin/bookings/1", RoleNames.HallManager, 403)]
    public async Task LegacyBookingReads_AreManagerOnly(string path, string role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.GetAsync(path);

        if (expectedStatus == 0)
            Assert.NotEqual(403, (int)response.StatusCode);
        else
            Assert.Equal(expectedStatus, (int)response.StatusCode);
    }
}

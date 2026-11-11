using System.Security.Claims;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc18LegacyStatusBoundaryTests
{
    private const string Uc18RequiredCode = "UC18_REQUIRED";
    private const string Uc18RequiredMessage =
        "Hủy booking phải được thực hiện qua quy trình UC18 (quote/commit).";

    [Theory]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.HallManager)]
    [InlineData(RoleNames.Coordinator)]
    [InlineData(RoleNames.Customer)]
    public async Task LegacyCancellation_ReturnsUc18Required_ForEveryRole_WithoutMutatingDraftBooking(string role)
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.LegacyController(role).UpdateBookingStatus(1,
            new UpdateBookingStatusRequestDto
            {
                StatusCode = "cancelled",
                CancellationReason = "Thử hủy qua legacy"
            });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(Uc18RequiredCode, ReadProperty(conflict.Value, "code"));
        Assert.Equal(Uc18RequiredMessage, ReadProperty(conflict.Value, "message"));

        await fixture.AssertUnchangedAsync();
    }

    [Fact]
    public void LegacyStatusMutation_IsManagerOnly_WhileReadsAndCreateKeepAdminAccess()
    {
        var updatePolicy = Assert.Single(typeof(AdminBookingController)
            .GetMethod(nameof(AdminBookingController.UpdateBookingStatus))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, updatePolicy.Roles);

        var controllerPolicy = Assert.Single(typeof(AdminBookingController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal($"{RoleNames.Admin},{RoleNames.Manager}", controllerPolicy.Roles);

        foreach (var methodName in new[]
                 {
                     nameof(AdminBookingController.GetBookings),
                     nameof(AdminBookingController.GetBookingDetail),
                     nameof(AdminBookingController.CreateBooking)
                 })
            Assert.Empty(typeof(AdminBookingController).GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public async Task ManagerCancellation_MustUseUc18QuoteThenCommit()
    {
        await using var fixture = await Fixture.CreateAsync();

        var legacy = await fixture.LegacyController(RoleNames.Manager).UpdateBookingStatus(1,
            new UpdateBookingStatusRequestDto { StatusCode = BookingStatusCodes.Cancelled });
        var conflict = Assert.IsType<ConflictObjectResult>(legacy);
        Assert.Equal(Uc18RequiredCode, ReadProperty(conflict.Value, "code"));
        await fixture.AssertUnchangedAsync();

        var manager = fixture.ManagerController(RoleNames.Manager);
        var quoteResult = await manager.CancelQuote(1,
            new ManagerBookingCancelQuoteRequestDto { Origin = "CUSTOMER" }, CancellationToken.None);
        var quote = Assert.IsType<ManagerBookingCancelQuoteDto>(
            Assert.IsType<OkObjectResult>(quoteResult).Value);

        var cancelResult = await manager.Cancel(1, new ManagerBookingCancelRequestDto
        {
            Reason = "Khách đổi kế hoạch",
            RequestId = Guid.NewGuid(),
            QuoteToken = quote.QuoteToken,
            Origin = "CUSTOMER"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(cancelResult);
        await fixture.AssertCancelledThroughUc18Async();
    }

    private static string? ReadProperty(object? value, string propertyName) =>
        value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly DateTime EventDate = DateTime.Today.AddDays(40);

        private readonly ApplicationDbContext _db;
        private readonly IStatusService _statuses;

        private Fixture(ApplicationDbContext db)
        {
            _db = db;
            _statuses = new StatusService(db);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings
                    .Ignore(InMemoryEventId.TransactionIgnoredWarning)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options;
            var fixture = new Fixture(new ApplicationDbContext(options));

            var statuses = StatusData();
            fixture._db.Statuses.AddRange(statuses);
            var existing = new DataStatus
            {
                DataStatusId = 1,
                DataStatusCode = DataStatusCodes.Existing,
                DataStatusName = "Tồn tại"
            };
            fixture._db.DataStatuses.Add(existing);

            var hallStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.Hall && item.StatusCode == BusinessStatusCodes.Active);
            var bookingStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.Booking && item.StatusCode == BookingStatusCodes.Confirmed);
            var scheduleStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.HallSchedule && item.StatusCode == HallScheduleStatusCodes.Booked);
            var contractStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.Contract && item.StatusCode == ContractStatusCodes.Draft);

            var hall = new Hall
            {
                HallId = 1,
                HallCode = "H01",
                HallName = "Riverside",
                MinimumCapacity = 100,
                MaximumCapacity = 500,
                RentalPrice = 1_000m,
                StatusId = hallStatus.StatusId,
                Status = hallStatus,
                DataStatusId = existing.DataStatusId,
                DataStatus = existing
            };
            var schedule = new HallSchedule
            {
                HallScheduleId = 1,
                HallId = 1,
                Hall = hall,
                Date = EventDate,
                Shift = "Ca tối",
                StatusId = scheduleStatus.StatusId,
                Status = scheduleStatus,
                DataStatusId = existing.DataStatusId,
                DataStatus = existing
            };
            var customer = new Customer
            {
                CustomerId = 1,
                CustomerCode = "KH-UC18-001",
                FullName = "Khách thử nghiệm",
                PhoneNumber = "0901000001",
                DataStatusId = existing.DataStatusId,
                DataStatus = existing
            };
            var booking = new WeddingBooking
            {
                BookingId = 1,
                BookingCode = "DT-UC18-001",
                CustomerId = 1,
                Customer = customer,
                HallScheduleId = 1,
                HallSchedule = schedule,
                GuestCount = 200,
                BilledTableCount = 20,
                SpareTableCount = 0,
                FinalHallPrice = 1_000m,
                EstimatedTotal = 20_000_000m,
                PaidAmount = 0m,
                StatusId = bookingStatus.StatusId,
                Status = bookingStatus,
                BookedAt = DateTime.Now.AddDays(-1)
            };
            var contract = new Contract
            {
                ContractId = 1,
                BookingId = 1,
                Booking = booking,
                ContractCode = "HD-DRAFT-001",
                CreatedDate = DateTime.Now.AddDays(-1),
                TotalValue = 20_000_000m,
                StatusId = contractStatus.StatusId,
                Status = contractStatus
            };
            booking.Contract = contract;

            fixture._db.Halls.Add(hall);
            fixture._db.HallSchedules.Add(schedule);
            fixture._db.Customers.Add(customer);
            fixture._db.WeddingBookings.Add(booking);
            fixture._db.Contracts.Add(contract);
            await fixture._db.SaveChangesAsync();
            fixture._db.ChangeTracker.Clear();
            return fixture;
        }

        public AdminBookingController LegacyController(string role) =>
            new(_db, _statuses,
                new CoordinationAssignmentService(_db, new NotificationService(_db)))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = Principal(role) }
                }
            };

        public ManagerBookingChangesController ManagerController(string role) =>
            new(_db, _statuses)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = Principal(role) }
                }
            };

        public async Task AssertUnchangedAsync()
        {
            _db.ChangeTracker.Clear();
            var booking = await _db.WeddingBookings.AsNoTracking()
                .Include(item => item.Status)
                .Include(item => item.HallSchedule).ThenInclude(schedule => schedule.Status)
                .Include(item => item.Contract).ThenInclude(contract => contract!.Status)
                .SingleAsync();

            Assert.Equal(BookingStatusCodes.Confirmed, booking.Status.StatusCode);
            Assert.Equal(HallScheduleStatusCodes.Booked, booking.HallSchedule.Status.StatusCode);
            Assert.Equal(ContractStatusCodes.Draft, booking.Contract!.Status.StatusCode);
            Assert.Null(booking.CancellationReason);
            Assert.Empty(await _db.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Empty(await _db.Notifications.AsNoTracking().ToListAsync());
            Assert.Empty(await _db.Set<Uc18BookingOperation>().AsNoTracking().ToListAsync());
        }

        public async Task AssertCancelledThroughUc18Async()
        {
            _db.ChangeTracker.Clear();
            var booking = await _db.WeddingBookings.AsNoTracking()
                .Include(item => item.Status)
                .Include(item => item.HallSchedule).ThenInclude(schedule => schedule.Status)
                .Include(item => item.Contract).ThenInclude(contract => contract!.Status)
                .SingleAsync();

            Assert.Equal(BookingStatusCodes.Cancelled, booking.Status.StatusCode);
            Assert.Equal(HallScheduleStatusCodes.Available, booking.HallSchedule.Status.StatusCode);
            Assert.Equal(ContractStatusCodes.Cancelled, booking.Contract!.Status.StatusCode);
            Assert.Equal("Khách đổi kế hoạch", booking.CancellationReason);
            Assert.Single(await _db.Set<Uc18BookingOperation>().AsNoTracking().ToListAsync());
            var audit = Assert.Single(await _db.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Equal("BOOKING_CANCELLED_FINANCIALLY", audit.Action);
        }

        public ValueTask DisposeAsync() => _db.DisposeAsync();

        private static ClaimsPrincipal Principal(string role) =>
            new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "10"),
                new Claim(ClaimTypes.Role, role)
            ], "Test"));

        private static Status[] StatusData() =>
        [
            new() { StatusId = 1, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ" },
            new() { StatusId = 2, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Xác nhận" },
            new() { StatusId = 3, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Preparing, StatusName = "Chuẩn bị" },
            new() { StatusId = 4, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Completed, StatusName = "Hoàn tất" },
            new() { StatusId = 5, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new() { StatusId = 11, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" },
            new() { StatusId = 12, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new() { StatusId = 13, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "Đã khóa" },
            new() { StatusId = 21, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Nháp" },
            new() { StatusId = 22, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Hiệu lực" },
            new() { StatusId = 23, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Completed, StatusName = "Hoàn tất" },
            new() { StatusId = 24, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new() { StatusId = 31, StatusGroup = StatusGroups.Hall, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" }
        ];
    }
}

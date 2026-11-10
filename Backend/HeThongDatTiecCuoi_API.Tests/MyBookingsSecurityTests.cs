using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class MyBookingsSecurityTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(ApplicationDbContext Db, MyBookingsController Controller)> SetupContextAsync(
        ClaimsPrincipal? user = null)
    {
        var options = CreateOptions();
        var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        var pendingStatus = new Status { StatusId = 101, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" };
        var cancelledStatus = new Status { StatusId = 102, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" };
        var hallAvailStatus = new Status { StatusId = 201, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };

        db.DataStatuses.Add(dataStatus);
        db.Statuses.AddRange(pendingStatus, cancelledStatus, hallAvailStatus);

        var hall = new Hall { HallId = 1, HallCode = "HALL-01", HallName = "Sảnh Kim Cương", MinimumCapacity = 100, MaximumCapacity = 300, RentalPrice = 50000000, StatusId = 201, DataStatusId = 1 };
        var schedule = new HallSchedule { HallScheduleId = 1, HallId = 1, Hall = hall, Date = DateTime.Today.AddDays(30), Shift = "Ca tối", StatusId = 201 };
        var customer1 = new Customer { CustomerId = 1, UserId = 10, FullName = "Nguyễn Văn A", PhoneNumber = "0901234567", DataStatusId = 1 };
        var customer2 = new Customer { CustomerId = 2, UserId = 20, FullName = "Trần Thị B", PhoneNumber = "0909999999", DataStatusId = 1 };

        var booking = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-20261220-C30C104E",
            CustomerId = 1,
            Customer = customer1,
            HallScheduleId = 1,
            HallSchedule = schedule,
            StatusId = 101,
            Status = pendingStatus,
            GuestCount = 150,
            EstimatedTotal = 100000000,
            BookedAt = DateTime.Now
        };

        db.Halls.Add(hall);
        db.HallSchedules.Add(schedule);
        db.Customers.AddRange(customer1, customer2);
        db.WeddingBookings.Add(booking);
        await db.SaveChangesAsync();

        var controller = new MyBookingsController(db, new StubStatusService(), new StubCoordinationAssignmentService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return (db, controller);
    }

    [Fact]
    public async Task GetMyBookings_Anonymous_PhoneOnly_ReturnsBadRequest()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.GetMyBookings(userId: null, bookingCode: null, phone: "0901234567");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetMyBookings_Anonymous_BookingCodeOnly_ReturnsBadRequest()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.GetMyBookings(userId: null, bookingCode: "DT-20261220-C30C104E", phone: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetMyBookings_Anonymous_UserIdOnly_ReturnsUnauthorized()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.GetMyBookings(userId: 10, bookingCode: null, phone: null);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_Anonymous_ValidCodeAndPhone_ReturnsOk()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.GetMyBookings(userId: null, bookingCode: "DT-20261220-C30C104E", phone: "0901234567");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CancelBooking_Anonymous_WithoutCredentials_ReturnsUnauthorized()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.CancelBooking(1, cancellationReason: "Test", bookingCode: null, phone: null);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, statusResult.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_Anonymous_MismatchedPhone_ReturnsForbidden()
    {
        var (_, controller) = await SetupContextAsync();
        var result = await controller.CancelBooking(1, cancellationReason: "Test", bookingCode: "DT-20261220-C30C104E", phone: "0909999999");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_Anonymous_MatchingCodeAndPhone_Succeeds()
    {
        var (db, controller) = await SetupContextAsync();
        var result = await controller.CancelBooking(1, cancellationReason: "Test", bookingCode: "DT-20261220-C30C104E", phone: "0901234567");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var booking = await db.WeddingBookings.FindAsync(1);
        Assert.Equal(102, booking!.StatusId);
    }

    [Fact]
    public async Task CancelBooking_AuthenticatedOtherCustomer_ReturnsForbidden()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "20"),
            new Claim(ClaimTypes.Role, RoleNames.Customer)
        }, "Bearer"));

        var (_, controller) = await SetupContextAsync(userClaims);
        var result = await controller.CancelBooking(1, cancellationReason: "Test");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_AuthenticatedOwner_Succeeds()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, RoleNames.Customer)
        }, "Bearer"));

        var (db, controller) = await SetupContextAsync(userClaims);
        var result = await controller.CancelBooking(1, cancellationReason: "Test");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var booking = await db.WeddingBookings.FindAsync(1);
        Assert.Equal(102, booking!.StatusId);
    }

    private sealed class StubStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(statusCode == BookingStatusCodes.Cancelled ? 102 : 201);

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(new Status { StatusId = 102, StatusGroup = statusGroup, StatusCode = statusCode });
    }

    private sealed class StubCoordinationAssignmentService : ICoordinationAssignmentService
    {
        public Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId, HallManagerCoordinationQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId, CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId, ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId, CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode, int? actorUserId, string? reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId, int? excludedBookingId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

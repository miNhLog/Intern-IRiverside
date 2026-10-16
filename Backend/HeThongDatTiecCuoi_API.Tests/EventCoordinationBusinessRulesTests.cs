using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class EventCoordinationBusinessRulesTests
{
    [Theory]
    [InlineData(BookingStatusCodes.Confirmed)]
    [InlineData(BookingStatusCodes.Preparing)]
    public async Task ConfirmedOrPreparingWithoutAssigned_IsUnassigned(string status)
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        AddBooking(db, 1, "TC-UNASSIGNED", DateTime.Today.AddDays(1), "Ca trưa", status);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.Equal("UNASSIGNED", Assert.Single(result.Items).CoordinationState);
        Assert.Equal(1, result.Kpi.UnassignedCount);
    }

    [Fact]
    public async Task ActiveAssignment_IsAssigned()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        AddBooking(db, 1, "TC-ASSIGNED", DateTime.Today.AddDays(1), "Ca trưa", BookingStatusCodes.Confirmed);
        AddCoordinator(db, 10, "DP010", "Điều phối 10");
        AddAssignment(db, 1, 10);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.Equal("ASSIGNED", Assert.Single(result.Items).CoordinationState);
        Assert.Equal(0, result.Kpi.UnassignedCount);
    }

    [Fact]
    public async Task SameCoordinatorDateShiftAndTwoActiveBookings_IsConflict()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        var date = DateTime.Today.AddDays(2);
        AddCoordinator(db, 10, "DP010", "Điều phối 10");
        AddBooking(db, 1, "TC-CF-1", date, "Ca tối", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-CF-2", date, "Ca tối", BookingStatusCodes.Preparing);
        AddAssignment(db, 1, 10); AddAssignment(db, 2, 10);
        await db.SaveChangesAsync();

        var result = await GetList(db, coordinationStatus: "CONFLICT");

        Assert.Equal(2, result.TotalItems);
        Assert.All(result.Items, item => Assert.Equal("CONFLICT", item.CoordinationState));
    }

    [Fact]
    public async Task SameDayButDifferentShift_IsNotConflict()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        var date = DateTime.Today.AddDays(2);
        AddCoordinator(db, 10, "DP010", "Điều phối 10");
        AddBooking(db, 1, "TC-SHIFT-1", date, "Ca trưa", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-SHIFT-2", date, "Ca tối", BookingStatusCodes.Confirmed);
        AddAssignment(db, 1, 10); AddAssignment(db, 2, 10);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.All(result.Items, item => Assert.False(item.HasConflict));
    }

    [Fact]
    public async Task DifferentDay_IsNotConflict()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        AddCoordinator(db, 10, "DP010", "Điều phối 10");
        AddBooking(db, 1, "TC-DATE-1", DateTime.Today.AddDays(2), "Ca trưa", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-DATE-2", DateTime.Today.AddDays(3), "Ca trưa", BookingStatusCodes.Confirmed);
        AddAssignment(db, 1, 10); AddAssignment(db, 2, 10);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.All(result.Items, item => Assert.False(item.HasConflict));
    }

    [Theory]
    [InlineData(BookingStatusCodes.Completed)]
    [InlineData(BookingStatusCodes.Cancelled)]
    public async Task CompletedOrCancelled_DoesNotCreateActiveConflict(string inactiveStatus)
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        var date = DateTime.Today.AddDays(2);
        AddCoordinator(db, 10, "DP010", "Điều phối 10");
        AddBooking(db, 1, "TC-ACTIVE", date, "Ca trưa", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-INACTIVE", date, "Ca trưa", inactiveStatus);
        AddAssignment(db, 1, 10); AddAssignment(db, 2, 10);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.All(result.Items, item => Assert.False(item.HasConflict));
        Assert.Equal("ASSIGNED", result.Items.Single(item => item.BookingId == 1).CoordinationState);
    }

    [Fact]
    public async Task HallManager_IsResolvedByEffectiveDateRange()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        var eventDate = DateTime.Today.AddDays(10);
        AddBooking(db, 1, "TC-HALL-MANAGER", eventDate, "Ca tối", BookingStatusCodes.Confirmed);
        AddEmployee(db, 20, "QLS020", "Quản lý cũ", RoleNames.HallManager);
        AddEmployee(db, 21, "QLS021", "Quản lý đúng", RoleNames.HallManager);
        db.HallManagerAssignments.AddRange(
            new HallManagerAssignment { HallManagerAssignmentId = 1, HallId = 1, HallManagerEmployeeId = 20, FromDate = eventDate.AddDays(-20), ToDate = eventDate.AddDays(-1) },
            new HallManagerAssignment { HallManagerAssignmentId = 2, HallId = 1, HallManagerEmployeeId = 21, FromDate = eventDate, ToDate = eventDate.AddDays(20) });
        await db.SaveChangesAsync();

        var detail = await GetDetail(db, 1);

        Assert.Equal("Quản lý đúng", detail.HallManagerName);
    }

    [Fact]
    public async Task IncidentKpi_CountsDistinctBookings()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        AddBooking(db, 1, "TC-INC-1", DateTime.Today.AddDays(1), "Ca trưa", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-INC-2", DateTime.Today.AddDays(2), "Ca tối", BookingStatusCodes.Confirmed);
        db.Incidents.AddRange(
            Incident(1, 1, "PENDING"), Incident(2, 1, "PROCESSING"), Incident(3, 2, "PENDING"));
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.Equal(2, result.Kpi.IncidentAttentionCount);
    }

    [Fact]
    public async Task UnassignedKpi_ExcludesCompletedAndCancelled()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        AddBooking(db, 1, "TC-CONFIRMED", DateTime.Today, "Ca trưa", BookingStatusCodes.Confirmed);
        AddBooking(db, 2, "TC-COMPLETED", DateTime.Today, "Ca tối", BookingStatusCodes.Completed);
        AddBooking(db, 3, "TC-CANCELLED", DateTime.Today.AddDays(1), "Ca trưa", BookingStatusCodes.Cancelled);
        await db.SaveChangesAsync();

        var result = await GetList(db);

        Assert.Equal(1, result.Kpi.UnassignedCount);
        Assert.Equal("NOT_APPLICABLE", result.Items.Single(x => x.BookingId == 2).CoordinationState);
        Assert.Equal("NOT_APPLICABLE", result.Items.Single(x => x.BookingId == 3).CoordinationState);
    }

    [Fact]
    public async Task PaginationAndFilter_ReturnCorrectTotalItems()
    {
        await using var db = CreateContext();
        SeedReferenceData(db);
        for (var i = 1; i <= 12; i++)
            AddBooking(db, i, $"FILTER-{i:00}", DateTime.Today.AddDays(i), i % 2 == 0 ? "Ca trưa" : "Ca tối", BookingStatusCodes.Confirmed);
        AddBooking(db, 20, "OTHER-01", DateTime.Today, "Ca trưa", BookingStatusCodes.Preparing);
        await db.SaveChangesAsync();

        var result = await GetList(db, search: "FILTER", page: 2, pageSize: 5);

        Assert.Equal(12, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, item => Assert.StartsWith("FILTER-", item.BookingCode));
    }

    private static Incident Incident(int id, int bookingId, string statusCode) => new()
    {
        IncidentId = id, BookingId = bookingId, IncidentType = "Vận hành", Description = "Kiểm thử",
        Severity = "Bình thường", StatusId = StatusId(StatusGroups.Incident, statusCode), OccurredAt = DateTime.Now
    };

    private static async Task<EventCoordinationListResponseDto> GetList(ApplicationDbContext db,
        string? search = null, string? coordinationStatus = null, int page = 1, int pageSize = 10)
    {
        var action = await new ManagerEventCoordinationController(db).GetList(search, null, coordinationStatus, null, null, page, pageSize);
        return Assert.IsType<EventCoordinationListResponseDto>(Assert.IsType<OkObjectResult>(action.Result).Value);
    }

    private static async Task<EventCoordinationDetailDto> GetDetail(ApplicationDbContext db, int id)
    {
        var action = await new ManagerEventCoordinationController(db).GetDetail(id, CancellationToken.None);
        return Assert.IsType<EventCoordinationDetailDto>(Assert.IsType<OkObjectResult>(action.Result).Value);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedReferenceData(ApplicationDbContext db)
    {
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
        db.Roles.AddRange(
            new Role { RoleId = 2, RoleName = RoleNames.Manager },
            new Role { RoleId = 3, RoleName = RoleNames.HallManager },
            new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
        db.Statuses.AddRange(
            BookingStatus(801, BookingStatusCodes.Pending), BookingStatus(802, BookingStatusCodes.Confirmed),
            BookingStatus(803, BookingStatusCodes.Preparing), BookingStatus(804, BookingStatusCodes.Completed),
            BookingStatus(805, BookingStatusCodes.Cancelled),
            new Status { StatusId = 1101, StatusCode = "ASSIGNED", StatusName = "Đã phân công", StatusGroup = StatusGroups.CoordinationAssignment },
            new Status { StatusId = 1201, StatusCode = "PENDING", StatusName = "Chờ xử lý", StatusGroup = StatusGroups.Incident },
            new Status { StatusId = 1202, StatusCode = "PROCESSING", StatusName = "Đang xử lý", StatusGroup = StatusGroups.Incident });
        db.Halls.Add(new Hall { HallId = 1, HallCode = "S01", HallName = "Sảnh Crystal", MaximumCapacity = 500, RentalPrice = 1, StatusId = 1, DataStatusId = 1 });
    }

    private static Status BookingStatus(int id, string code) => new() { StatusId = id, StatusCode = code, StatusName = code, StatusGroup = StatusGroups.Booking };
    private static int StatusId(string group, string code) => (group, code) switch
    {
        (StatusGroups.Booking, BookingStatusCodes.Pending) => 801,
        (StatusGroups.Booking, BookingStatusCodes.Confirmed) => 802,
        (StatusGroups.Booking, BookingStatusCodes.Preparing) => 803,
        (StatusGroups.Booking, BookingStatusCodes.Completed) => 804,
        (StatusGroups.Booking, BookingStatusCodes.Cancelled) => 805,
        (StatusGroups.Incident, "PENDING") => 1201,
        (StatusGroups.Incident, "PROCESSING") => 1202,
        _ => 1101
    };

    private static void AddBooking(ApplicationDbContext db, int id, string code, DateTime date, string shift, string status)
    {
        db.Customers.Add(new Customer { CustomerId = id, CustomerCode = $"KH{id:000}", FullName = $"Khách {id}", PhoneNumber = $"090000{id:0000}", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = id, HallId = 1, Date = date.Date, Shift = shift, StatusId = 1, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking { BookingId = id, BookingCode = code, CustomerId = id, HallScheduleId = id, GuestCount = 100, StatusId = StatusId(StatusGroups.Booking, status), BookedAt = DateTime.Now });
    }

    private static void AddCoordinator(ApplicationDbContext db, int id, string code, string name) => AddEmployee(db, id, code, name, RoleNames.Coordinator);
    private static void AddEmployee(ApplicationDbContext db, int id, string code, string name, string role)
    {
        var roleId = role == RoleNames.Coordinator ? (byte)4 : (byte)3;
        db.Users.Add(new User { UserId = 1000 + id, RoleId = roleId, Email = $"employee{id}@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = id, UserId = 1000 + id, EmployeeCode = code, FullName = name, PhoneNumber = $"091{id:0000000}", DataStatusId = 1 });
    }

    private static void AddAssignment(ApplicationDbContext db, int bookingId, int coordinatorId) =>
        db.CoordinationAssignments.Add(new CoordinationAssignment
        {
            CoordinationAssignmentId = bookingId, BookingId = bookingId, CoordinatorEmployeeId = coordinatorId,
            AssignedByHallManagerEmployeeId = coordinatorId, StatusId = 1101, AssignedAt = DateTime.Now
        });
}

public sealed class EventCoordinationAuthorizationTests
{
    [Fact]
    public void Controller_IsRestrictedToManagerOnly()
    {
        var attribute = Assert.Single(typeof(ManagerEventCoordinationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, attribute.Roles);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(RoleNames.Manager, 200)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.HallManager, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    public async Task ManagerPolicy_ReturnsExpectedResultForEachRole(string? role, int expectedStatus)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var evaluator = services.GetRequiredService<IPolicyEvaluator>();
        var policy = new AuthorizationPolicyBuilder().RequireRole(RoleNames.Manager).Build();
        var context = new DefaultHttpContext { RequestServices = services };
        AuthenticateResult authentication;
        if (role is null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            authentication = AuthenticateResult.NoResult();
        }
        else
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test");
            context.User = new ClaimsPrincipal(identity);
            authentication = AuthenticateResult.Success(new AuthenticationTicket(context.User, "Test"));
        }

        var result = await evaluator.AuthorizeAsync(policy, authentication, context, null);
        var actualStatus = result.Succeeded ? 200 : result.Challenged ? 401 : 403;
        Assert.Equal(expectedStatus, actualStatus);
    }
}

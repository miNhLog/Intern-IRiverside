using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class HallManagerDashboardServiceTests
{
    [Fact]
    public async Task Dashboard_UsesOnlyCurrentDistinctHalls_AndAppliesKpiBoundaries()
    {
        await using var fixture = await DashboardFixture.CreateAsync();

        var result = await fixture.Service.GetAsync(fixture.UserId, default);

        Assert.True(result.Succeeded, result.Error);
        var model = result.Value!;
        Assert.Equal(2, model.Kpi.ManagedHalls);
        Assert.Equal(2, model.Kpi.TodayBookings);
        Assert.Equal(3, model.Kpi.NextSevenDaysBookings);
        Assert.Equal(2, model.Kpi.UnassignedBookings);
        Assert.Equal(1, model.Kpi.CoordinationConflicts);
        Assert.Equal(2, model.Kpi.OpenIncidents);
        Assert.Equal(3, model.UpcomingBookings.Count);
        Assert.All(model.UpcomingBookings, item => Assert.DoesNotContain(item.BookingStatusCode,
            new[] { BookingStatusCodes.Pending, BookingStatusCodes.Completed, BookingStatusCodes.Cancelled }));
        Assert.DoesNotContain(model.UpcomingBookings, item => item.HallName == "Sảnh ngoài phạm vi");
    }

    [Fact]
    public async Task Schedule_ReportsBookingAndAvailability_WithoutWritingRows()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        var schedulesBefore = await fixture.Context.HallSchedules.CountAsync();
        var bookingsBefore = await fixture.Context.WeddingBookings.CountAsync();

        var today = await fixture.Service.GetScheduleAsync(fixture.UserId, DateTime.Today, default);
        var future = await fixture.Service.GetScheduleAsync(fixture.UserId, DateTime.Today.AddDays(30), default);

        Assert.True(today.Succeeded, today.Error);
        Assert.Equal(2, today.Value!.Halls.Count);
        var riverside = Assert.Single(today.Value.Halls, hall => hall.HallName == "Sảnh Riverside");
        Assert.Equal(BookingStatusCodes.Confirmed, riverside.Lunch.StateCode);
        Assert.Equal(HallScheduleStatusCodes.Available, riverside.Dinner.StateCode);
        var garden = Assert.Single(today.Value.Halls, hall => hall.HallName == "Sảnh Garden");
        Assert.Equal(BookingStatusCodes.Pending, garden.Lunch.StateCode);
        Assert.Equal(BookingStatusCodes.Completed, garden.Dinner.StateCode);
        Assert.All(future.Value!.Halls, hall =>
        {
            Assert.Equal("NO_SCHEDULE", hall.Lunch.StateCode);
            Assert.Equal("NO_SCHEDULE", hall.Dinner.StateCode);
        });
        Assert.Equal(schedulesBefore, await fixture.Context.HallSchedules.CountAsync());
        Assert.Equal(bookingsBefore, await fixture.Context.WeddingBookings.CountAsync());
    }

    [Fact]
    public async Task Dashboard_WithNoCurrentAssignment_ReturnsSuccessfulEmptyState()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        foreach (var assignment in fixture.Context.HallManagerAssignments
                     .Where(item => item.HallManagerEmployeeId == fixture.EmployeeId))
            assignment.ToDate = DateTime.Today.AddDays(-1);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(fixture.UserId, default);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0, result.Value!.Kpi.ManagedHalls);
        Assert.Empty(result.Value.Schedule.Halls);
        Assert.Empty(result.Value.UpcomingBookings);
        Assert.Empty(result.Value.Tasks);
    }

    private sealed class DashboardFixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; }
        public HallManagerDashboardService Service { get; }
        public int UserId => 20;
        public int EmployeeId => 200;

        private DashboardFixture(ApplicationDbContext context)
        {
            Context = context;
            Service = new HallManagerDashboardService(context);
        }

        public static async Task<DashboardFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"hall-manager-dashboard-{Guid.NewGuid()}").Options;
            var db = new ApplicationDbContext(options);
            var data = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
            var active = Status(1, AccountStatusCodes.Active, "Hoạt động", StatusGroups.Account);
            var available = Status(2, HallScheduleStatusCodes.Available, "Còn trống", StatusGroups.HallSchedule);
            var booked = Status(3, HallScheduleStatusCodes.Booked, "Đã đặt", StatusGroups.HallSchedule);
            var pending = Status(4, BookingStatusCodes.Pending, "Chờ xác nhận", StatusGroups.Booking);
            var confirmed = Status(5, BookingStatusCodes.Confirmed, "Đã xác nhận", StatusGroups.Booking);
            var preparing = Status(6, BookingStatusCodes.Preparing, "Đang chuẩn bị", StatusGroups.Booking);
            var completed = Status(7, BookingStatusCodes.Completed, "Đã hoàn thành", StatusGroups.Booking);
            var cancelled = Status(8, BookingStatusCodes.Cancelled, "Đã hủy", StatusGroups.Booking);
            var assigned = Status(9, CoordinationAssignmentStatusCodes.Assigned, "Đã phân công", StatusGroups.CoordinationAssignment);
            var incidentPending = Status(10, BusinessStatusCodes.Pending, "Chờ xử lý", StatusGroups.Incident);
            var incidentProcessing = Status(11, BusinessStatusCodes.Processing, "Đang xử lý", StatusGroups.Incident);
            var incidentResolved = Status(12, BusinessStatusCodes.Resolved, "Đã xử lý", StatusGroups.Incident);
            var hallRole = new Role { RoleId = 3, RoleName = RoleNames.HallManager };
            var coordinatorRole = new Role { RoleId = 4, RoleName = RoleNames.Coordinator };
            var user = User(20, hallRole, active, data, "hall@test.local");
            var otherUser = User(21, hallRole, active, data, "other@test.local");
            var coordinatorUser = User(30, coordinatorRole, active, data, "coordinator@test.local");
            var employee = Employee(200, user, data, "QLS200", "Quản lý Riverside");
            var otherEmployee = Employee(201, otherUser, data, "QLS201", "Quản lý khác");
            var coordinator = Employee(300, coordinatorUser, data, "DP300", "Điều phối A");
            var halls = new[]
            {
                Hall(1, "Sảnh Riverside", data), Hall(2, "Sảnh Garden", data), Hall(3, "Sảnh ngoài phạm vi", data)
            };
            db.AddRange(data, active, available, booked, pending, confirmed, preparing, completed, cancelled,
                assigned, incidentPending, incidentProcessing, incidentResolved, hallRole, coordinatorRole,
                user, otherUser, coordinatorUser, employee, otherEmployee, coordinator, halls[0], halls[1], halls[2]);
            db.HallManagerAssignments.AddRange(
                Assignment(1, halls[0], employee, DateTime.Today.AddDays(-30), null),
                Assignment(2, halls[0], employee, DateTime.Today.AddDays(-10), null),
                Assignment(3, halls[1], employee, DateTime.Today, DateTime.Today.AddDays(10)),
                Assignment(4, halls[2], employee, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(-1)),
                Assignment(5, halls[2], employee, DateTime.Today.AddDays(1), null),
                Assignment(6, halls[2], otherEmployee, DateTime.Today.AddDays(-30), null));

            var customer = new Customer { CustomerId = 1, CustomerCode = "KH001", FullName = "Khách hàng", PhoneNumber = "0900", CreatedAt = DateTime.Today, DataStatusId = 1, DataStatus = data };
            db.Customers.Add(customer);
            var scheduleId = 100;
            HallSchedule Schedule(Hall hall, int days, string shift, Status status) => new()
            {
                HallScheduleId = scheduleId++, HallId = hall.HallId, Hall = hall, Date = DateTime.Today.AddDays(days),
                Shift = shift, StatusId = status.StatusId, Status = status, DataStatusId = 1, DataStatus = data
            };
            var sTodayLunch = Schedule(halls[0], 0, "Ca trưa", booked);
            var sTodayDinner = Schedule(halls[0], 0, "Ca tối", available);
            var sPending = Schedule(halls[1], 0, "Ca trưa", booked);
            var sCompletedToday = Schedule(halls[1], 0, "Ca tối", booked);
            var sTomorrow = Schedule(halls[0], 1, "Ca tối", booked);
            var sDay2 = Schedule(halls[1], 2, "Ca trưa", booked);
            var sDay7 = Schedule(halls[1], 7, "Ca tối", booked);
            var sDay8 = Schedule(halls[1], 8, "Ca tối", booked);
            var sOutsideConflict = Schedule(halls[2], 1, "Ca tối", booked);
            var sCancelled = Schedule(halls[0], 3, "Ca trưa", available);
            db.HallSchedules.AddRange(sTodayLunch, sTodayDinner, sPending, sCompletedToday, sTomorrow, sDay2,
                sDay7, sDay8, sOutsideConflict, sCancelled);
            var bookingId = 1000;
            WeddingBooking Booking(HallSchedule schedule, Status status, string code) => new()
            {
                BookingId = bookingId++, BookingCode = code, CustomerId = 1, Customer = customer,
                HallScheduleId = schedule.HallScheduleId, HallSchedule = schedule, GuestCount = 105,
                StatusId = status.StatusId, Status = status, BookedAt = DateTime.Today
            };
            var bToday = Booking(sTodayLunch, confirmed, "BK-TODAY");
            var bPending = Booking(sPending, pending, "BK-PENDING");
            var bCompletedToday = Booking(sCompletedToday, completed, "BK-COMPLETE-TODAY");
            var bTomorrow = Booking(sTomorrow, confirmed, "BK-CONFLICT");
            var bDay2 = Booking(sDay2, preparing, "BK-UNASSIGNED");
            var bDay7 = Booking(sDay7, completed, "BK-DAY7");
            var bDay8 = Booking(sDay8, confirmed, "BK-DAY8");
            var bOutside = Booking(sOutsideConflict, confirmed, "BK-OUTSIDE");
            var bCancelled = Booking(sCancelled, cancelled, "BK-CANCELLED");
            db.WeddingBookings.AddRange(bToday, bPending, bCompletedToday, bTomorrow, bDay2, bDay7, bDay8, bOutside, bCancelled);
            db.CoordinationAssignments.AddRange(
                Coordination(1, bTomorrow, coordinator, employee, assigned),
                Coordination(2, bOutside, coordinator, otherEmployee, assigned),
                Coordination(3, bDay8, coordinator, employee, assigned));
            db.Incidents.AddRange(
                Incident(1, bToday, incidentPending, IncidentSeverityCodes.Critical),
                Incident(2, bDay2, incidentProcessing, IncidentSeverityCodes.Normal),
                Incident(3, bTomorrow, incidentResolved, IncidentSeverityCodes.Normal),
                Incident(4, bOutside, incidentPending, IncidentSeverityCodes.Critical));
            await db.SaveChangesAsync();
            return new DashboardFixture(db);
        }

        private static Status Status(int id, string code, string name, string group) => new()
            { StatusId = id, StatusCode = code, StatusName = name, StatusGroup = group };
        private static User User(int id, Role role, Status status, DataStatus data, string email) => new()
            { UserId = id, RoleId = role.RoleId, Role = role, Email = email, PasswordHash = "x", StatusId = status.StatusId, Status = status, DataStatusId = 1, DataStatus = data };
        private static Employee Employee(int id, User user, DataStatus data, string code, string name) => new()
            { EmployeeId = id, UserId = user.UserId, User = user, EmployeeCode = code, FullName = name, PhoneNumber = "0900", DataStatusId = 1, DataStatus = data };
        private static Hall Hall(int id, string name, DataStatus data) => new()
            { HallId = id, HallCode = $"H{id}", HallName = name, MaximumCapacity = 500, RentalPrice = 1, StatusId = 99, DataStatusId = 1, DataStatus = data };
        private static HallManagerAssignment Assignment(int id, Hall hall, Employee employee, DateTime from, DateTime? to) => new()
            { HallManagerAssignmentId = id, HallId = hall.HallId, Hall = hall, HallManagerEmployeeId = employee.EmployeeId, HallManager = employee, FromDate = from, ToDate = to };
        private static CoordinationAssignment Coordination(int id, WeddingBooking booking, Employee coordinator, Employee manager, Status status) => new()
            { CoordinationAssignmentId = id, BookingId = booking.BookingId, Booking = booking, CoordinatorEmployeeId = coordinator.EmployeeId, Coordinator = coordinator, AssignedByHallManagerEmployeeId = manager.EmployeeId, AssignedByHallManager = manager, StatusId = status.StatusId, Status = status, AssignedAt = DateTime.Now };
        private static Incident Incident(int id, WeddingBooking booking, Status status, string severity) => new()
            { IncidentId = id, BookingId = booking.BookingId, Booking = booking, IncidentType = "Vận hành", Description = "Sự cố cần xử lý", Severity = severity, StatusId = status.StatusId, Status = status, OccurredAt = DateTime.Now.AddMinutes(-id) };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}

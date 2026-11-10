using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.DTOs.Notification;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class CoordinationAssignmentSqlServerTests
{
    [Fact]
    public async Task ConcurrentRequests_OnSqlServer_OnlyOneSameDateShiftAssignmentSucceeds()
    {
        var databaseName = $"RiversideCoordinationTests_{Guid.NewGuid():N}";
        var server = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? ".\\SQLEXPRESS";
        var master = $"Server={server};Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
        var testConnection = $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
        await using var masterConnection = new SqlConnection(master);
        await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand())
        {
            create.CommandTimeout = 60;
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(testConnection).Options;
            int managerUserId;
            int coordinatorEmployeeId;
            int firstBookingId;
            int secondBookingId;
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                var seed = Seed(setup);
                await setup.SaveChangesAsync();
                managerUserId = seed.Manager.UserId;
                coordinatorEmployeeId = seed.Coordinator.EmployeeId;
                firstBookingId = seed.FirstBooking.BookingId;
                secondBookingId = seed.SecondBooking.BookingId;
            }

            static async Task<ServiceResult<CoordinationMutationResultDto>> Assign(
                DbContextOptions<ApplicationDbContext> options, int userId, int bookingId, int coordinatorId)
            {
                await using var db = new ApplicationDbContext(options);
                var service = new CoordinationAssignmentService(db, new NotificationService(db));
                return await service.AssignAsync(userId, bookingId, new() { CoordinatorEmployeeId = coordinatorId }, default);
            }

            var results = await Task.WhenAll(
                Assign(options, managerUserId, firstBookingId, coordinatorEmployeeId),
                Assign(options, managerUserId, secondBookingId, coordinatorEmployeeId));
            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => !result.Succeeded && result.StatusCode == StatusCodes.Status409Conflict);
            await using var verify = new ApplicationDbContext(options);
            Assert.Equal(1, await verify.CoordinationAssignments.CountAsync());
            // Exercise checklist writes against SQL Server too (transaction locks and version checks).
            var assigned = await verify.CoordinationAssignments.Include(a => a.Coordinator).SingleAsync();
            var checklist = await verify.PartyChecklists.Include(c => c.Tasks).SingleAsync();
            Assert.Equal(8, checklist.Tasks.Count);
            var checklistService = new ChecklistService(verify, new ChecklistTestClock(DateTime.Today.AddDays(2).AddHours(14)));
            var initialVersion = checklist.Version;
            var coordinatorUserId = assigned.Coordinator.UserId;
            Assert.Equal(8, (await checklistService.IncompleteCountAsync(coordinatorUserId, RoleNames.Coordinator, default)).Value);
            var workspace = await checklistService.GetAsync(coordinatorUserId, RoleNames.Coordinator, default);
            Assert.Equal("10:30–14:00", Assert.Single(workspace.Value!.Parties).ShiftHours);
            Assert.Equal(409, (await checklistService.ExecuteAsync(coordinatorUserId, RoleNames.Coordinator,
                assigned.BookingId, "submit", new() { Version = checklist.Version }, default)).StatusCode);
            foreach (var task in checklist.Tasks)
                Assert.True((await checklistService.ExecuteAsync(coordinatorUserId, RoleNames.Coordinator,
                    assigned.BookingId, "task", new() { Version = checklist.Version, TaskId = task.Id, Status = "done" }, default)).Succeeded);
            Assert.Equal(409, (await checklistService.ExecuteAsync(coordinatorUserId, RoleNames.Coordinator,
                assigned.BookingId, "submit", new() { Version = initialVersion }, default)).StatusCode);
            Assert.True((await checklistService.ExecuteAsync(coordinatorUserId, RoleNames.Coordinator,
                assigned.BookingId, "submit", new() { Version = checklist.Version }, default)).Succeeded);
            Assert.True((await checklistService.ExecuteAsync(managerUserId, RoleNames.HallManager,
                assigned.BookingId, "approve", new() { Version = checklist.Version }, default)).Succeeded);
            Assert.Equal(0, (await checklistService.IncompleteCountAsync(coordinatorUserId, RoleNames.Coordinator, default)).Value);
            verify.ChangeTracker.Clear();
            Assert.Equal("approved", (await verify.PartyChecklists.SingleAsync()).State);
            Assert.Equal(8, await verify.PartyChecklistTasks.CountAsync(t => t.Status == "done"));
            Assert.Equal(BookingStatusCodes.Confirmed, (await verify.WeddingBookings.Include(b => b.Status)
                .SingleAsync(b => b.BookingId == assigned.BookingId)).Status.StatusCode);
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ChangeFailure_AfterOldAssignmentUpdate_RollsBackWholeSqlServerTransaction()
    {
        var databaseName = $"RiversideCoordinationRollback_{Guid.NewGuid():N}";
        var server = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? ".\\SQLEXPRESS";
        var master = $"Server={server};Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
        var testConnection = $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
        await using var masterConnection = new SqlConnection(master); await masterConnection.OpenAsync();
        await using (var create = masterConnection.CreateCommand()) { create.CommandTimeout = 60; create.CommandText = $"CREATE DATABASE [{databaseName}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(testConnection).Options;
            int managerId, bookingId, oldCoordinatorId, newCoordinatorId;
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync(); var seed = Seed(setup); await setup.SaveChangesAsync();
                managerId = seed.Manager.UserId; bookingId = seed.FirstBooking.BookingId;
                oldCoordinatorId = seed.Coordinator.EmployeeId; newCoordinatorId = seed.SecondCoordinator.EmployeeId;
                var service = new CoordinationAssignmentService(setup, new NotificationService(setup));
                Assert.True((await service.AssignAsync(managerId, bookingId, new() { CoordinatorEmployeeId = oldCoordinatorId }, default)).Succeeded);
            }
            await using (var failingContext = new ApplicationDbContext(options))
            {
                var service = new CoordinationAssignmentService(failingContext, new ThrowingNotificationService());
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeAsync(managerId, bookingId,
                    new() { CoordinatorEmployeeId = newCoordinatorId, Reason = "Kiểm tra rollback" }, default));
            }
            await using var verify = new ApplicationDbContext(options);
            var rows = await verify.CoordinationAssignments.Include(a => a.Status).ToListAsync();
            Assert.Single(rows); Assert.Equal(oldCoordinatorId, rows[0].CoordinatorEmployeeId);
            Assert.Equal(CoordinationAssignmentStatusCodes.Assigned, rows[0].Status.StatusCode);
        }
        finally
        {
            await using var drop = masterConnection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static SqlSeed Seed(ApplicationDbContext db)
    {
        db.DataStatuses.Add(new() { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
        db.Roles.AddRange(new Role { RoleId = 3, RoleName = RoleNames.HallManager }, new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
        db.Statuses.AddRange(
            Status(101, StatusGroups.Account, AccountStatusCodes.Active), Status(201, StatusGroups.Hall, "ACTIVE"), Status(301, StatusGroups.HallSchedule, "BOOKED"),
            Status(802, StatusGroups.Booking, BookingStatusCodes.Confirmed), Status(804, StatusGroups.Booking, BookingStatusCodes.Completed), Status(805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
            Status(1101, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Assigned), Status(1102, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Completed), Status(1103, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Cancelled));
        var hall1 = Hall("Sảnh 1"); var hall2 = Hall("Sảnh 2"); db.Halls.AddRange(hall1, hall2);
        var manager = Employee("QLS010", "Quản lý sảnh", 3); var coordinator = Employee("DP020", "Điều phối 20", 4);
        var secondCoordinator = Employee("DP021", "Điều phối 21", 4);
        db.Employees.AddRange(manager, coordinator, secondCoordinator);
        db.HallManagerAssignments.AddRange(
            new HallManagerAssignment { Hall = hall1, HallManager = manager, FromDate = DateTime.Today.AddDays(-1) },
            new HallManagerAssignment { Hall = hall2, HallManager = manager, FromDate = DateTime.Today.AddDays(-1) });
        var first = Booking("DT-1", "KH1", hall1); var second = Booking("DT-2", "KH2", hall2);
        db.WeddingBookings.AddRange(first, second);
        return new SqlSeed(manager.User, coordinator, secondCoordinator, first, second);
    }
    private static Status Status(int id, string group, string code) => new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };
    private static Hall Hall(string name) => new() { HallCode = name.Replace("ảnh", string.Empty).Replace(" ", string.Empty), HallName = name, MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1 };
    private static Employee Employee(string code, string name, byte roleId) => new()
    {
        EmployeeCode = code, FullName = name, PhoneNumber = $"09{Guid.NewGuid():N}"[..10], DataStatusId = 1,
        User = new User { RoleId = roleId, Email = $"{code}@test.local", PasswordHash = "test", StatusId = 101, DataStatusId = 1 }
    };
    private static WeddingBooking Booking(string bookingCode, string customerCode, Hall hall)
    {
        var customer = new Customer { CustomerCode = customerCode, FullName = customerCode, PhoneNumber = $"09{Guid.NewGuid():N}"[..10], DataStatusId = 1 };
        var schedule = new HallSchedule { Hall = hall, Date = DateTime.Today.AddDays(2), Shift = "Ca trưa", StatusId = 301, DataStatusId = 1 };
        return new WeddingBooking { BookingCode = bookingCode, Customer = customer, HallSchedule = schedule, GuestCount = 100, StatusId = 802, BookedAt = DateTime.Now };
    }
    private sealed record SqlSeed(User Manager, Employee Coordinator, Employee SecondCoordinator,
        WeddingBooking FirstBooking, WeddingBooking SecondBooking);

    private sealed class ChecklistTestClock(DateTime localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(localNow, TimeSpan.FromHours(7)).ToUniversalTime();
    }

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task<bool> AddAsync(int recipientUserId, int? actorUserId, string type, string title, string message,
            string? relatedEntityType, int? relatedEntityId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Lỗi giả lập sau khi cập nhật phân công cũ.");
        public Task<int?> FindActiveAdminUserIdAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<NotificationDto>> GetRecentAsync(int userId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationPageDto> GetPageAsync(int userId, int page, int pageSize, bool? isRead, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

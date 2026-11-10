using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Incident;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class IncidentBusinessRulesTests
{
    [Fact]
    public async Task Coordinator_CanCreateForAssignedBooking_AndActorComesFromAccount()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var result = await fixture.Service.CreateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            ValidCreate(fixture.AssignedBookingId), default);

        Assert.True(result.Succeeded, result.Error);
        var incident = await fixture.Context.Incidents.SingleAsync(item => item.IncidentId == result.Value!.IncidentId);
        Assert.Equal(fixture.CoordinatorEmployeeId, incident.ReportedByEmployeeId);
        Assert.Equal(BusinessStatusCodes.Pending, (await fixture.Context.Statuses.FindAsync(incident.StatusId))!.StatusCode);
        Assert.Contains(await fixture.Context.AuditLogs.ToListAsync(), log => log.Action == AuditActions.IncidentCreated);
    }

    [Fact]
    public async Task Coordinator_CannotCreateOrViewOutsideAssignedBookings()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var create = await fixture.Service.CreateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            ValidCreate(fixture.OtherBookingId), default);
        var detail = await fixture.Service.GetDetailAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            fixture.OtherIncidentId, default);

        Assert.False(create.Succeeded);
        Assert.Equal(403, create.StatusCode);
        Assert.False(detail.Succeeded);
        Assert.Equal(404, detail.StatusCode);
    }

    [Fact]
    public async Task Coordinator_CannotEditAnotherReport_OrResolvedReport()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var update = new UpdateIncidentRequest
        {
            IncidentType = "Thiết bị", Description = "Mô tả sự cố đã được cập nhật hợp lệ.",
            Severity = IncidentSeverityCodes.Normal, OccurredAt = DateTime.Now.AddMinutes(-5)
        };
        var other = await fixture.Service.UpdateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            fixture.OtherReporterIncidentId, update, default);
        var resolved = await fixture.Service.UpdateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            fixture.ResolvedIncidentId, update, default);

        Assert.Equal(403, other.StatusCode);
        Assert.Equal(409, resolved.StatusCode);
    }

    [Fact]
    public async Task Coordinator_CannotChangeProcessingStatus()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var result = await fixture.Service.UpdateStatusAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            fixture.ResolvedIncidentId, new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task CreatingIncident_NotifiesResponsibleHallManagerAndManager_WithoutSelfNotification()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var result = await fixture.Service.CreateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            ValidCreate(fixture.AssignedBookingId), default);

        Assert.True(result.Succeeded, result.Error);
        var recipients = await fixture.Context.Notifications.Select(item => item.RecipientUserId).OrderBy(id => id).ToListAsync();
        Assert.Equal([20, 30], recipients);
        Assert.DoesNotContain(fixture.CoordinatorUserId, recipients);
        var hallManagerNotifications = await new NotificationService(fixture.Context).GetRecentAsync(20, 5, default);
        Assert.Single(hallManagerNotifications);
        Assert.Equal(result.Value!.IncidentId, hallManagerNotifications[0].RelatedEntityId);
    }

    [Fact]
    public async Task HallManager_CanOnlyCreateAndViewInsideAssignedHalls()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var allowed = await fixture.Service.CreateAsync(20, RoleNames.HallManager,
            ValidCreate(fixture.AssignedBookingId), default);
        var deniedCreate = await fixture.Service.CreateAsync(20, RoleNames.HallManager,
            ValidCreate(fixture.OtherBookingId), default);
        var deniedDetail = await fixture.Service.GetDetailAsync(20, RoleNames.HallManager,
            fixture.OtherIncidentId, default);
        var deniedStatus = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.OtherIncidentId, new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);

        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(403, deniedCreate.StatusCode);
        Assert.Equal(404, deniedDetail.StatusCode);
        Assert.Equal(404, deniedStatus.StatusCode);
    }

    [Fact]
    public async Task HallTransfer_GivesCurrentManagerExistingIncidentAccess_AndRevokesPreviousManager()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var oldAssignment = await fixture.Context.HallManagerAssignments.SingleAsync();
        oldAssignment.ToDate = DateTime.Today.AddDays(-1);
        var activeStatus = await fixture.Context.Statuses.SingleAsync(status =>
            status.StatusGroup == StatusGroups.Account && status.StatusCode == AccountStatusCodes.Active);
        var dataStatus = await fixture.Context.DataStatuses.SingleAsync();
        var role = await fixture.Context.Roles.SingleAsync(item => item.RoleName == RoleNames.HallManager);
        var newUser = new User { UserId = 21, RoleId = role.RoleId, Role = role, Email = "new-manager@test.local", PasswordHash = "x", StatusId = activeStatus.StatusId, Status = activeStatus, DataStatusId = 1, DataStatus = dataStatus };
        var newEmployee = new Employee { EmployeeId = 201, UserId = 21, User = newUser, EmployeeCode = "QLS002", FullName = "Quản lý mới", PhoneNumber = "0900000021", DataStatusId = 1, DataStatus = dataStatus };
        fixture.Context.AddRange(newUser, newEmployee, new HallManagerAssignment
        {
            HallManagerAssignmentId = 2, HallId = 1, HallManagerEmployeeId = 201,
            HallManager = newEmployee, FromDate = DateTime.Today
        });
        await fixture.Context.SaveChangesAsync();

        var newManagerDetail = await fixture.Service.GetDetailAsync(21, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, default);
        var history = await fixture.Service.GetHistoryAsync(21, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, default);
        var accepted = await fixture.Service.UpdateStatusAsync(21, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId,
            new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);
        var oldManagerDetail = await fixture.Service.GetDetailAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, default);

        Assert.True(newManagerDetail.Succeeded, newManagerDetail.Error);
        Assert.True(history.Succeeded, history.Error);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(404, oldManagerDetail.StatusCode);
        Assert.Contains(await fixture.Context.AuditLogs.ToListAsync(), log =>
            log.EntityId == fixture.PendingCoordinatorIncidentId && log.UserId == 21 &&
            log.Action == AuditActions.IncidentStatusChanged);
    }

    [Fact]
    public async Task HallManager_ProcessesIncidentInOrder_AndWritesHistoryAndNotifications()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var accept = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);
        var resolution = await fixture.Service.UpdateResolutionAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, new UpdateIncidentResolutionRequest { Resolution = "Đã thay micro dự phòng và kiểm tra tín hiệu ổn định." }, default);
        var resolve = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, new UpdateIncidentStatusRequest
            { Status = BusinessStatusCodes.Resolved, Resolution = "Đã thay micro dự phòng và kiểm tra tín hiệu ổn định." }, default);

        Assert.True(accept.Succeeded, accept.Error);
        Assert.True(resolution.Succeeded, resolution.Error);
        Assert.True(resolve.Succeeded, resolve.Error);
        Assert.Equal(BusinessStatusCodes.Resolved, resolve.Value!.StatusCode);
        Assert.NotNull(resolve.Value.ResolvedAt);
        var history = await fixture.Service.GetHistoryAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, default);
        Assert.True(history.Succeeded);
        Assert.Contains(history.Value!, item => item.Action == AuditActions.IncidentStatusChanged);
        Assert.Contains(history.Value!, item => item.Action == AuditActions.IncidentResolutionUpdated);
        var recipients = await fixture.Context.Notifications.Select(item => item.RecipientUserId).ToListAsync();
        Assert.Contains(10, recipients);
        Assert.Contains(30, recipients);
    }

    [Fact]
    public async Task HallManager_CannotSkipStateOrResolveWithoutResolution()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var skipped = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId,
            new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Resolved, Resolution = "Đã xử lý" }, default);
        var missingResolution = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.ProcessingIncidentId,
            new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Resolved, Resolution = " " }, default);

        Assert.Equal(409, skipped.StatusCode);
        Assert.Equal(400, missingResolution.StatusCode);
    }

    [Fact]
    public async Task HallManager_CanEditPendingReport_ButNotAfterAcceptingIt()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var update = new UpdateIncidentRequest
        {
            IncidentType = "Âm thanh sân khấu", Description = "Micro chính chập chờn trong quá trình tổng duyệt.",
            Severity = IncidentSeverityCodes.Critical, OccurredAt = DateTime.Now.AddMinutes(-10)
        };
        var before = await fixture.Service.UpdateAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, update, default);
        var accept = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);
        var after = await fixture.Service.UpdateAsync(20, RoleNames.HallManager,
            fixture.PendingCoordinatorIncidentId, update, default);

        Assert.True(before.Succeeded, before.Error);
        Assert.True(accept.Succeeded, accept.Error);
        Assert.Equal(409, after.StatusCode);
    }

    [Fact]
    public async Task Manager_CanViewAllHalls_CreateAndEditPendingIncident()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var list = await fixture.Service.GetListAsync(30, RoleNames.Manager, null, null, null,
            null, null, null, 1, 20, default);
        var detail = await fixture.Service.GetDetailAsync(30, RoleNames.Manager,
            fixture.PendingCoordinatorIncidentId, default);
        var created = await fixture.Service.CreateAsync(30, RoleNames.Manager,
            ValidCreate(fixture.AssignedBookingId), default);
        var update = await fixture.Service.UpdateAsync(30, RoleNames.Manager,
            fixture.OtherIncidentId, new UpdateIncidentRequest
            {
                IncidentType = "Nguồn điện", Description = "Manager cập nhật nội dung sự cố ngoài sảnh phụ trách.",
                Severity = IncidentSeverityCodes.Critical, OccurredAt = DateTime.Now.AddMinutes(-5)
            }, default);

        Assert.True(list.Succeeded, list.Error);
        Assert.Equal(fixture.OtherIncidentId, list.Value!.Items[0].IncidentId);
        Assert.Contains(list.Value.Items, item => item.IncidentId == fixture.OtherIncidentId);
        Assert.True(detail.Succeeded, detail.Error);
        Assert.Contains("Nhân viên B", detail.Value!.HallManagerNames);
        Assert.True(created.Succeeded, created.Error);
        Assert.True(update.Succeeded, update.Error);
    }

    [Fact]
    public async Task Manager_Intervention_WritesAuditAndNotifiesReporterAndHallManagerWithoutSelfNotification()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var resolution = await fixture.Service.UpdateResolutionAsync(30, RoleNames.Manager,
            fixture.ProcessingIncidentId, new UpdateIncidentResolutionRequest
            { Resolution = "Manager điều phối thay bộ nguồn và xác nhận thiết bị hoạt động ổn định." }, default);
        var resolved = await fixture.Service.UpdateStatusAsync(30, RoleNames.Manager,
            fixture.ProcessingIncidentId, new UpdateIncidentStatusRequest
            {
                Status = BusinessStatusCodes.Resolved,
                Resolution = "Manager điều phối thay bộ nguồn và xác nhận thiết bị hoạt động ổn định."
            }, default);

        Assert.True(resolution.Succeeded, resolution.Error);
        Assert.True(resolved.Succeeded, resolved.Error);
        var interventionNotifications = await fixture.Context.Notifications
            .Where(item => item.Title == "Quản lý cập nhật hướng xử lý")
            .Select(item => item.RecipientUserId).OrderBy(id => id).ToListAsync();
        Assert.Equal([10, 20], interventionNotifications);
        Assert.DoesNotContain(30, await fixture.Context.Notifications.Select(item => item.RecipientUserId).ToListAsync());
        Assert.Contains(await fixture.Context.AuditLogs.ToListAsync(),
            item => item.Action == AuditActions.IncidentResolutionUpdated && item.UserId == 30);
        Assert.NotNull(resolved.Value!.ResolvedAt);
    }

    [Fact]
    public async Task AdminAndCustomer_HaveNoIncidentScope_AndApiExposesNoDeleteEndpoint()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var admin = await fixture.Service.GetListAsync(30, RoleNames.Admin, null, null, null,
            null, null, null, 1, 10, default);
        var customer = await fixture.Service.GetListAsync(30, RoleNames.Customer, null, null, null,
            null, null, null, 1, 10, default);
        var roles = typeof(IncidentController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles;
        var deleteActions = typeof(IncidentController).GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0).ToList();

        Assert.Equal(403, admin.StatusCode);
        Assert.Equal(403, customer.StatusCode);
        Assert.DoesNotContain(RoleNames.Admin, roles ?? string.Empty);
        Assert.DoesNotContain(RoleNames.Customer, roles ?? string.Empty);
        Assert.Empty(deleteActions);
    }

    [Fact]
    public async Task ThreeRoleWorkflow_UsesOneIncidentRecordFromReportToResolution()
    {
        await using var fixture = await IncidentFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.CoordinatorUserId, RoleNames.Coordinator,
            ValidCreate(fixture.AssignedBookingId), default);
        Assert.True(created.Succeeded, created.Error);
        var incidentId = created.Value!.IncidentId;

        var accepted = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager, incidentId,
            new UpdateIncidentStatusRequest { Status = BusinessStatusCodes.Processing }, default);
        var progress = await fixture.Service.UpdateResolutionAsync(20, RoleNames.HallManager, incidentId,
            new UpdateIncidentResolutionRequest { Resolution = "Đã chuyển sang thiết bị dự phòng và kiểm tra lại." }, default);
        var completed = await fixture.Service.UpdateStatusAsync(20, RoleNames.HallManager, incidentId,
            new UpdateIncidentStatusRequest
            {
                Status = BusinessStatusCodes.Resolved,
                Resolution = "Đã chuyển sang thiết bị dự phòng và kiểm tra lại."
            }, default);
        var managerView = await fixture.Service.GetDetailAsync(30, RoleNames.Manager, incidentId, default);
        var coordinatorView = await fixture.Service.GetDetailAsync(fixture.CoordinatorUserId,
            RoleNames.Coordinator, incidentId, default);

        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.True(progress.Succeeded, progress.Error);
        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal(BusinessStatusCodes.Resolved, managerView.Value!.StatusCode);
        Assert.Equal(BusinessStatusCodes.Resolved, coordinatorView.Value!.StatusCode);
        Assert.Equal(incidentId, (await fixture.Context.Incidents.SingleAsync(i => i.IncidentId == incidentId)).IncidentId);
        Assert.Contains(await fixture.Context.Notifications.ToListAsync(), item =>
            item.RecipientUserId == fixture.CoordinatorUserId && item.RelatedEntityId == incidentId &&
            item.Title == "Sự cố đã được xử lý");
    }

    private static CreateIncidentRequest ValidCreate(int bookingId) => new()
    {
        BookingId = bookingId, IncidentType = "Âm thanh",
        Description = "Micro sân khấu mất tín hiệu trong lúc kiểm tra.",
        Severity = IncidentSeverityCodes.Normal, OccurredAt = DateTime.Now.AddMinutes(-2)
    };

    private sealed class IncidentFixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; }
        public IncidentService Service { get; }
        public int CoordinatorUserId => 10;
        public int CoordinatorEmployeeId => 100;
        public int AssignedBookingId => 1000;
        public int OtherBookingId => 2000;
        public int OtherIncidentId => 3000;
        public int OtherReporterIncidentId => 3001;
        public int ResolvedIncidentId => 3002;
        public int ProcessingIncidentId => 3003;
        public int PendingCoordinatorIncidentId => 3004;

        private IncidentFixture(ApplicationDbContext context)
        {
            Context = context;
            Service = new IncidentService(context, new NotificationService(context));
        }

        public static async Task<IncidentFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"incident-rules-{Guid.NewGuid()}").Options;
            var context = new ApplicationDbContext(options);
            var active = new Status { StatusId = 101, StatusCode = AccountStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.Account };
            var assigned = new Status { StatusId = 1101, StatusCode = BusinessStatusCodes.Assigned, StatusName = "Đã phân công", StatusGroup = StatusGroups.CoordinationAssignment };
            var pending = new Status { StatusId = 1201, StatusCode = BusinessStatusCodes.Pending, StatusName = "Chờ xử lý", StatusGroup = StatusGroups.Incident };
            var processing = new Status { StatusId = 1202, StatusCode = BusinessStatusCodes.Processing, StatusName = "Đang xử lý", StatusGroup = StatusGroups.Incident };
            var resolved = new Status { StatusId = 1203, StatusCode = BusinessStatusCodes.Resolved, StatusName = "Đã xử lý", StatusGroup = StatusGroups.Incident };
            var bookingStatus = new Status { StatusId = 802, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận", StatusGroup = StatusGroups.Booking };
            var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
            var coordinatorRole = new Role { RoleId = 4, RoleName = RoleNames.Coordinator };
            var otherRole = new Role { RoleId = 3, RoleName = RoleNames.HallManager };
            var managerRole = new Role { RoleId = 2, RoleName = RoleNames.Manager };
            var user = new User { UserId = 10, RoleId = 4, Role = coordinatorRole, Email = "coordinator@test.local", PasswordHash = "x", StatusId = 101, Status = active, DataStatusId = 1, DataStatus = dataStatus };
            var otherUser = new User { UserId = 20, RoleId = 3, Role = otherRole, Email = "other@test.local", PasswordHash = "x", StatusId = 101, Status = active, DataStatusId = 1, DataStatus = dataStatus };
            var managerUser = new User { UserId = 30, RoleId = 2, Role = managerRole, Email = "manager@test.local", PasswordHash = "x", StatusId = 101, Status = active, DataStatusId = 1, DataStatus = dataStatus };
            var employee = new Employee { EmployeeId = 100, UserId = 10, User = user, EmployeeCode = "DP001", FullName = "Điều phối A", PhoneNumber = "0900000000", DataStatusId = 1, DataStatus = dataStatus };
            var otherEmployee = new Employee { EmployeeId = 200, UserId = 20, User = otherUser, EmployeeCode = "QLS001", FullName = "Nhân viên B", PhoneNumber = "0900000001", DataStatusId = 1, DataStatus = dataStatus };
            var managerEmployee = new Employee { EmployeeId = 300, UserId = 30, User = managerUser, EmployeeCode = "QL001", FullName = "Quản lý C", PhoneNumber = "0900000002", DataStatusId = 1, DataStatus = dataStatus };
            var hall = new Hall { HallId = 1, HallCode = "H01", HallName = "Sảnh Riverside", MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1, DataStatus = dataStatus };
            var otherHall = new Hall { HallId = 2, HallCode = "H02", HallName = "Sảnh Ngoài Phạm Vi", MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1, DataStatus = dataStatus };
            var schedule1 = new HallSchedule { HallScheduleId = 11, HallId = 1, Hall = hall, Date = DateTime.Today.AddDays(1), Shift = "Ca tối", StatusId = 301, DataStatusId = 1, DataStatus = dataStatus };
            var schedule2 = new HallSchedule { HallScheduleId = 12, HallId = 2, Hall = otherHall, Date = DateTime.Today.AddDays(2), Shift = "Ca tối", StatusId = 301, DataStatusId = 1, DataStatus = dataStatus };
            var booking1 = new WeddingBooking { BookingId = 1000, BookingCode = "DT-1000", CustomerId = 1, HallScheduleId = 11, HallSchedule = schedule1, GuestCount = 100, StatusId = 802, Status = bookingStatus };
            var booking2 = new WeddingBooking { BookingId = 2000, BookingCode = "DT-2000", CustomerId = 1, HallScheduleId = 12, HallSchedule = schedule2, GuestCount = 100, StatusId = 802, Status = bookingStatus };
            var assignment = new CoordinationAssignment { CoordinationAssignmentId = 1, BookingId = 1000, Booking = booking1, CoordinatorEmployeeId = 100, Coordinator = employee, AssignedByHallManagerEmployeeId = 200, AssignedByHallManager = otherEmployee, StatusId = 1101, Status = assigned, AssignedAt = DateTime.Now };
            var hallAssignment = new HallManagerAssignment { HallManagerAssignmentId = 1, HallId = 1, Hall = hall, HallManagerEmployeeId = 200, HallManager = otherEmployee, FromDate = DateTime.Today.AddDays(-10) };
            var outside = new Incident { IncidentId = 3000, BookingId = 2000, Booking = booking2, ReportedByEmployeeId = 200, ReportedByEmployee = otherEmployee, IncidentType = "Điện", Description = "Sự cố ngoài phạm vi", Severity = IncidentSeverityCodes.Critical, StatusId = 1201, Status = pending, OccurredAt = DateTime.Now };
            var otherReporter = new Incident { IncidentId = 3001, BookingId = 1000, Booking = booking1, ReportedByEmployeeId = 200, ReportedByEmployee = otherEmployee, IncidentType = "Điện", Description = "Người khác báo cáo", Severity = IncidentSeverityCodes.Normal, StatusId = 1201, Status = pending, OccurredAt = DateTime.Now };
            var resolvedOwn = new Incident { IncidentId = 3002, BookingId = 1000, Booking = booking1, ReportedByEmployeeId = 100, ReportedByEmployee = employee, IncidentType = "Âm thanh", Description = "Sự cố đã xử lý", Severity = IncidentSeverityCodes.Normal, StatusId = 1203, Status = resolved, OccurredAt = DateTime.Now.AddHours(-1), ResolvedAt = DateTime.Now };
            var processingIncident = new Incident { IncidentId = 3003, BookingId = 1000, Booking = booking1, ReportedByEmployeeId = 100, ReportedByEmployee = employee, IncidentType = "Ánh sáng", Description = "Sự cố đang xử lý", Severity = IncidentSeverityCodes.Normal, StatusId = 1202, Status = processing, OccurredAt = DateTime.Now.AddMinutes(-30) };
            var pendingCoordinator = new Incident { IncidentId = 3004, BookingId = 1000, Booking = booking1, ReportedByEmployeeId = 100, ReportedByEmployee = employee, IncidentType = "Micro", Description = "Micro chính mất tín hiệu", Severity = IncidentSeverityCodes.Normal, StatusId = 1201, Status = pending, OccurredAt = DateTime.Now.AddMinutes(-20) };
            context.AddRange(active, assigned, pending, processing, resolved, bookingStatus, dataStatus, coordinatorRole, otherRole, managerRole,
                user, otherUser, managerUser, employee, otherEmployee, managerEmployee, hall, otherHall, schedule1, schedule2, booking1, booking2, assignment, hallAssignment,
                outside, otherReporter, resolvedOwn, processingIncident, pendingCoordinator);
            await context.SaveChangesAsync();
            return new IncidentFixture(context);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}

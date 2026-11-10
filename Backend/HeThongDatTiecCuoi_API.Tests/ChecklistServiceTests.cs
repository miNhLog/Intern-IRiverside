using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Checklist;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ChecklistServiceTests
{
    [Fact]
    public async Task Assignment_CreatesOneChecklist_AndReassignmentPreservesTasksAndScope()
    {
        await using var f = await Fixture.Create();
        var first = await f.Db.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        Assert.Equal(8, first.Tasks.Count);
        var task = first.Tasks.First(); task.Status = "done"; task.Note = "Đã kiểm tra";
        await f.Db.SaveChangesAsync();
        var result = await f.Assignments.ChangeAsync(100, 1, new() { CoordinatorEmployeeId = 21, Reason = "Bàn giao ca" }, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, await f.Db.PartyChecklists.CountAsync());
        Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
        Assert.Equal("Đã kiểm tra", (await f.Db.PartyChecklistTasks.SingleAsync(t => t.Id == task.Id)).Note);
        Assert.Empty((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.True(Assert.Single((await f.Service.GetAsync(201, RoleNames.Coordinator, default)).Value!.Parties).CanEdit);
        Assert.Equal(404, (await f.Service.ExecuteAsync(200, RoleNames.Coordinator, 1, "task", new() { Version = first.Version, TaskId = task.Id, Status = "todo" }, default)).StatusCode);
    }

    [Fact]
    public async Task Handover_RequiresMandatoryWork_AndManagerReviewDoesNotCompleteBooking()
    {
        await using var f = await Fixture.Create();
        var list = await f.Db.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        Assert.Equal(409, (await f.Run(200, "submit", new() { Version = list.Version })).StatusCode);
        foreach (var t in list.Tasks) { var result = await f.Run(200, "task", new() { Version = list.Version, TaskId = t.Id, Status = "done" }); Assert.True(result.Succeeded, result.Error); }
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.True((await f.Run(200, "submit", new() { Version = list.Version, Note = "Đã hoàn tất" })).Succeeded);
        Assert.Equal("review", list.State);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = list.Version, TaskId = list.Tasks.First().Id, Status = "todo" })).StatusCode);
        Assert.Equal(403, (await f.Run(200, "approve", new() { Version = list.Version })).StatusCode);
        Assert.True((await f.Run(100, "approve", new() { Version = list.Version })).Succeeded);
        Assert.Equal("approved", list.State);
        Assert.Equal(BookingStatusCodes.Confirmed, (await f.Db.WeddingBookings.Include(b => b.Status).SingleAsync()).Status.StatusCode);
        Assert.Contains(await f.Db.PartyChecklistHistory.ToListAsync(), h => h.Action == "APPROVE" && h.ActorUserId == 100);
    }

    [Fact]
    public async Task Return_ReopensOnlySelectedTask_AndRecordsReason()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        foreach (var t in c.Tasks) { t.Status = "done"; ChecklistLifecycle.AddHistory(c, 200, "Điều phối", $"TASK:{t.Id}", $"Công việc “{t.Name}”: Chưa làm → Hoàn thành.", f.Clock.LocalNow); }
        c.State = "review"; await f.Db.SaveChangesAsync();
        var result = await f.Run(100, "return", new() { Version = c.Version, TaskId = c.Tasks.First().Id, Note = "Kiểm tra lại thiết bị" });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("active", c.State); Assert.Equal("Kiểm tra lại thiết bị", c.ReturnReason);
        Assert.Single(c.Tasks, t => t.Status == "todo");
        Assert.Equal(1, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
    }

    [Fact]
    public async Task ScopeAndVersion_RejectForeignManagerForeignTaskAndStaleRequest()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        Assert.Empty((await f.Service.GetAsync(101, RoleNames.HallManager, default)).Value!.Parties);
        Assert.Equal(404, (await f.Service.ExecuteAsync(101, RoleNames.HallManager, 1, "add", new() { Version = c.Version }, default)).StatusCode);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version, TaskId = 9999, Status = "done" })).StatusCode);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version - 1, TaskId = c.Tasks.First().Id, Status = "done" })).StatusCode);
        Assert.Equal(403, (await f.Service.GetAsync(200, RoleNames.Manager, default)).StatusCode);
        Assert.All(c.Tasks, t => Assert.Equal("todo", t.Status));
    }

    [Fact]
    public async Task Reschedule_RebasesDeadlines_WithoutResettingProgress()
    {
        await using var f = await Fixture.Create();
        var before = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); c.Tasks.First().Status = "done";
        var schedule = await f.Db.HallSchedules.SingleAsync(); schedule.Date = schedule.Date.AddDays(4);
        await f.Db.SaveChangesAsync();
        var after = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.Equal(DateTime.Parse(before.Tasks[0].Due).AddDays(4).ToString("yyyy-MM-dd"), after.Tasks[0].Due);
        Assert.Single(after.Tasks, t => t.Status == "done");
    }

    [Fact]
    public async Task Cancellation_KeepsHistory_ButLocksChecklistAndRemovesOutstandingCount()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        Assert.Equal(8, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        var booking = await f.Db.WeddingBookings.SingleAsync(); booking.StatusId = 805;
        await f.Assignments.SynchronizeBookingAssignmentsAsync(1, BookingStatusCodes.Cancelled, 100, "Khách hủy tiệc", default);
        await f.Db.SaveChangesAsync();
        Assert.Equal("cancelled", c.State); Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version, TaskId = c.Tasks.First().Id, Status = "done" })).StatusCode);
    }

    [Fact]
    public async Task AddingWorkDuringReview_ReopensChecklist_AndOptionalWorkDoesNotBlockHandover()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        foreach (var t in c.Tasks) { t.Status = "done"; ChecklistLifecycle.AddHistory(c, 200, "Điều phối", $"TASK:{t.Id}", $"Công việc “{t.Name}”: Chưa làm → Hoàn thành.", f.Clock.LocalNow); }
        c.State = "review"; await f.Db.SaveChangesAsync();
        var due = RiversideBusinessClock.Today.AddDays(3);
        var added = await f.Run(100, "add", new() { Version = c.Version, Name = "Chuẩn bị thêm bàn chay", Group = "Trước tiệc", DueDate = due, Required = false });
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal("active", c.State); Assert.Equal(9, c.Tasks.Count);
        var dto = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.Equal(due.ToString("yyyy-MM-dd"), dto.Tasks.Single(t => t.Name == "Chuẩn bị thêm bàn chay").Due);
        Assert.True((await f.Run(200, "submit", new() { Version = c.Version })).Succeeded);
        Assert.Equal("review", c.State);
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
    }

    [Fact]
    public async Task ConcurrentTrackedUpdates_DoNotOverwriteFirstWriter()
    {
        await using var f = await Fixture.Create();
        await using var second = new ApplicationDbContext(f.Options);
        var stale = await second.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        var taskId = stale.Tasks.First().Id;
        Assert.True((await f.Run(200, "task", new() { Version = stale.Version, TaskId = taskId, Status = "done" })).Succeeded);
        var result = await new ChecklistService(second).ExecuteAsync(200, RoleNames.Coordinator, 1, "task", new() { Version = stale.Version, TaskId = taskId, Status = "doing" }, default);
        Assert.Equal(409, result.StatusCode);
        await using var verify = new ApplicationDbContext(f.Options);
        Assert.Equal("done", (await verify.PartyChecklistTasks.SingleAsync(t => t.Id == taskId)).Status);
    }

    [Theory]
    [InlineData("Ca trưa", "Trong tiệc", 10, 30)]
    [InlineData("Ca tối", "Trong tiệc", 17, 30)]
    [InlineData("Ca trưa", "Sau tiệc", 14, 0)]
    [InlineData("Ca tối", "Sau tiệc", 21, 30)]
    public async Task TaskTiming_RejectsEarlyRequests_AndOpensAtExactVietnamTime(string shift, string group, int hour, int minute)
    {
        await using var f = await Fixture.Create();
        var schedule = await f.Db.HallSchedules.SingleAsync(); schedule.Shift = shift;
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        var task = c.Tasks.First(t => t.Group == group);
        f.Clock.LocalNow = schedule.Date.Date.AddHours(hour).AddMinutes(minute).AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        var version = c.Version; var histories = await f.Db.PartyChecklistHistory.CountAsync();
        foreach (var status in new[] { "doing", "done" })
            Assert.Equal(409, (await f.Run(200, "task", new() { Version = version, TaskId = task.Id, Status = status })).StatusCode);
        Assert.Equal(version, c.Version); Assert.Equal(histories, await f.Db.PartyChecklistHistory.CountAsync());
        var dto = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.False(dto.Tasks.Single(t => t.Id == task.Id).CanComplete);
        Assert.False(dto.CanHandover); Assert.NotNull(dto.ShiftHours);
        f.Clock.LocalNow = f.Clock.LocalNow.AddSeconds(1);
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "doing" })).Succeeded);
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        Assert.Equal(f.Clock.LocalNow, task.CompletedAt);
    }

    [Fact]
    public async Task Preparation_ConfirmationMayFinishEarly_PhysicalChecksOpenPreviousDay()
    {
        await using var f = await Fixture.Create();
        var schedule = await f.Db.HallSchedules.SingleAsync();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        f.Clock.LocalNow = schedule.Date.AddMonths(-1);
        foreach (var task in c.Tasks.Where(t => t.Name is "Xác nhận thực đơn và số bàn với bếp" or "Đối chiếu yêu cầu đặc biệt của khách"))
            Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        var physical = c.Tasks.First(t => t.Name == "Kiểm tra bố trí bàn và sơ đồ sảnh");
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version, TaskId = physical.Id, Status = "done" })).StatusCode);
        f.Clock.LocalNow = schedule.Date.Date.AddDays(-1);
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = physical.Id, Status = "done" })).Succeeded);
    }

    [Fact]
    public async Task Handover_BlocksPrematureSubmitAndApproval_EvenWithAllTasksDone()
    {
        await using var f = await Fixture.Create();
        var schedule = await f.Db.HallSchedules.SingleAsync();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        f.Clock.LocalNow = schedule.Date.Date.AddHours(21.5).AddSeconds(-1);
        foreach (var task in c.Tasks) { task.Status = "done"; ChecklistLifecycle.AddHistory(c, 200, "Điều phối", $"TASK:{task.Id}", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", f.Clock.LocalNow); }
        await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await f.Run(200, "submit", new() { Version = c.Version })).StatusCode);
        c.State = "review"; await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await f.Run(100, "approve", new() { Version = c.Version })).StatusCode);
        f.Clock.LocalNow = schedule.Date.Date.AddHours(22);
        // Elapsed time alone must not make the previous invalid completions legitimate.
        Assert.Equal(409, (await f.Run(100, "approve", new() { Version = c.Version })).StatusCode);
    }

    [Fact]
    public async Task TimingRepair_PreservesHistoryAndValidWork_RestoresKpi_AndIsManagerScoped()
    {
        await using var f = await Fixture.Create();
        var schedule = await f.Db.HallSchedules.SingleAsync();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).Include(x => x.History).SingleAsync();
        f.Clock.LocalNow = schedule.Date.AddMonths(-1);
        foreach (var t in c.Tasks) { t.Status = "done"; ChecklistLifecycle.AddHistory(c, 200, "Điều phối", $"TASK:{t.Id}", $"Công việc “{t.Name}”: Chưa làm → Hoàn thành.", f.Clock.LocalNow); }
        c.State = "approved";
        ChecklistLifecycle.AddHistory(c, 100, "Quản lý", "APPROVE", "Bàn giao cũ", f.Clock.LocalNow);
        await f.Db.SaveChangesAsync(); var historyCount = c.History.Count;
        Assert.Equal(6, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.Equal(403, (await f.Run(200, "repair-timing", new() { Version = c.Version, Note = "L01" })).StatusCode);
        Assert.Equal(404, (await f.Service.ExecuteAsync(101, RoleNames.HallManager, 1, "repair-timing", new() { Version = c.Version, Note = "L01" }, default)).StatusCode);
        Assert.True((await f.Run(100, "repair-timing", new() { Version = c.Version, Note = "L01: mở lại kết quả sớm" })).Succeeded);
        Assert.Equal("active", c.State); Assert.Equal(2, c.Tasks.Count(t => t.Status == "done"));
        Assert.Equal(historyCount + 7, c.History.Count); Assert.Contains(c.History, h => h.Text == "Bàn giao cũ");
        Assert.Equal(6, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        f.Clock.LocalNow = schedule.Date.AddHours(22);
        foreach (var task in c.Tasks.Where(t => t.Status != "done"))
            Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        Assert.True((await f.Run(200, "submit", new() { Version = c.Version })).Succeeded);
        Assert.True((await f.Run(100, "approve", new() { Version = c.Version })).Succeeded);
        Assert.False(Assert.Single((await f.Service.GetAsync(100, RoleNames.HallManager, default)).Value!.Parties).CanRepairTiming);
    }

    [Fact]
    public async Task NotesAndRepeatedDone_DoNotRewriteCompletionEvidence()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); var task = c.Tasks.First();
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        var completed = task.CompletedAt; f.Clock.LocalNow = f.Clock.LocalNow.AddDays(1);
        Assert.True((await f.Run(200, "note", new() { Version = c.Version, TaskId = task.Id, Note = "Ghi chú bổ sung" })).Succeeded);
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        Assert.Equal(completed, task.CompletedAt);
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "todo" })).Succeeded);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public async Task CompletionEvidence_SurvivesFreshContext_WithoutCompletionColumn()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); var task = c.Tasks.First();
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        var completed = f.Clock.LocalNow;
        f.Clock.LocalNow = completed.AddDays(1);
        Assert.True((await f.Run(200, "note", new() { Version = c.Version, TaskId = task.Id, Note = "Ghi chú sau khi hoàn thành" })).Succeeded);
        await using var fresh = new ApplicationDbContext(f.Options);
        var service = new ChecklistService(fresh, f.Clock);
        var dto = Assert.Single((await service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.Equal(completed, dto.Tasks.Single(t => t.Id == task.Id).CompletedAt);
        Assert.True(dto.Tasks.Single(t => t.Id == task.Id).CompletionValid);
        Assert.Equal(7, (await service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
    }

    [Fact]
    public async Task Delete_RemovesTask_RecalculatesProgress_AndKeepsAuditAndBookingState()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).Include(x => x.History).SingleAsync();
        var task = c.Tasks.First();
        Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = task.Id, Status = "done" })).Succeeded);
        Assert.True((await f.Run(200, "note", new() { Version = c.Version, TaskId = task.Id, Note = "Ghi chú cần giữ trong lịch sử" })).Succeeded);
        var id = task.Id; var name = task.Name; var oldVersion = c.Version; var histories = c.History.Select(h => h.Id).ToArray();
        Assert.True((await f.Run(100, "delete", new() { Version = oldVersion, TaskId = id, Note = "Công việc trùng, bỏ khỏi checklist" })).Succeeded);
        Assert.False(await f.Db.PartyChecklistTasks.AnyAsync(t => t.Id == id));
        Assert.Equal(7, c.Tasks.Count); Assert.Equal(oldVersion + 1, c.Version);
        var deletion = Assert.Single(c.History, h => h.Action == "DELETE");
        Assert.Contains(name, deletion.Text); Assert.Contains("Ghi chú cần giữ", deletion.Text);
        Assert.Contains("Công việc trùng", deletion.Text); Assert.Contains("Hoàn thành", deletion.Text);
        Assert.All(histories, id => Assert.Contains(c.History, h => h.Id == id));
        var dto = Assert.Single((await f.Service.GetAsync(100, RoleNames.HallManager, default)).Value!.Parties);
        Assert.Equal(7, dto.Tasks.Count); Assert.DoesNotContain(dto.Tasks, t => t.Id == id);
        Assert.Equal(BookingStatusCodes.Confirmed, (await f.Db.WeddingBookings.Include(b => b.Status).SingleAsync()).Status.StatusCode);
    }

    [Fact]
    public async Task Delete_PendingWork_ReducesOutstandingCount_AndReopensReview()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); var task = c.Tasks.First();
        Assert.Equal(8, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.True((await f.Run(100, "delete", new() { Version = c.Version, TaskId = task.Id, Note = "Không áp dụng cho tiệc này" })).Succeeded);
        Assert.Equal(7, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        foreach (var t in c.Tasks)
            Assert.True((await f.Run(200, "task", new() { Version = c.Version, TaskId = t.Id, Status = "done" })).Succeeded);
        Assert.True((await f.Run(200, "submit", new() { Version = c.Version })).Succeeded);
        var deleted = c.Tasks.First().Id;
        Assert.True((await f.Run(100, "delete", new() { Version = c.Version, TaskId = deleted, Note = "Loại bỏ công việc đã gộp" })).Succeeded);
        Assert.Equal("active", c.State); Assert.Equal(6, c.Tasks.Count);
        Assert.True((await f.Run(200, "submit", new() { Version = c.Version })).Succeeded);
    }

    [Fact]
    public async Task Delete_RequiresManagerScopeReasonTaskAndCurrentVersion()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); var id = c.Tasks.First().Id;
        Assert.Equal(403, (await f.Run(200, "delete", new() { Version = c.Version, TaskId = id, Note = "Không được xóa" })).StatusCode);
        Assert.Equal(404, (await f.Service.ExecuteAsync(101, RoleNames.HallManager, 1, "delete", new() { Version = c.Version, TaskId = id, Note = "Ngoài phạm vi" }, default)).StatusCode);
        Assert.Equal(400, (await f.Run(100, "delete", new() { Version = c.Version, TaskId = id, Note = " " })).StatusCode);
        Assert.Equal(409, (await f.Run(100, "delete", new() { Version = c.Version - 1, TaskId = id, Note = "Dữ liệu cũ" })).StatusCode);
        Assert.Equal(409, (await f.Run(100, "delete", new() { Version = c.Version, TaskId = int.MaxValue, Note = "Công việc khác" })).StatusCode);
        Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
        Assert.DoesNotContain(await f.Db.PartyChecklistHistory.ToListAsync(), h => h.Action == "DELETE");
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("cancelled")]
    public async Task Delete_CannotChangeLockedChecklist(string state)
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); c.State = state;
        await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await f.Run(100, "delete", new() { Version = c.Version, TaskId = c.Tasks.First().Id, Note = "Thử xóa checklist đã khóa" })).StatusCode);
        Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
    }

    [Fact]
    public async Task Delete_LastTask_DoesNotPermitEmptyHandover()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        foreach (var task in c.Tasks.ToList())
            Assert.True((await f.Run(100, "delete", new() { Version = c.Version, TaskId = task.Id, Note = "Không áp dụng" })).Succeeded);
        Assert.Empty(c.Tasks);
        Assert.Equal(409, (await f.Run(200, "submit", new() { Version = c.Version })).StatusCode);
    }

    private sealed class TestClock(DateTime localNow) : TimeProvider
    {
        public DateTime LocalNow { get; set; } = localNow;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(LocalNow, TimeSpan.FromHours(7)).ToUniversalTime();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<ApplicationDbContext> Options { get; } = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ApplicationDbContext Db { get; }
        public ChecklistService Service { get; }
        public TestClock Clock { get; } = new(RiversideBusinessClock.Today.AddDays(3).AddHours(22));
        public CoordinationAssignmentService Assignments { get; }
        private Fixture() { Db = new(Options); Service = new(Db, Clock); Assignments = new(Db, new NotificationService(Db)); }
        public Task<ServiceResult<string>> Run(int user, string action, ChecklistCommand command) => Service.ExecuteAsync(user, user == 100 ? RoleNames.HallManager : RoleNames.Coordinator, 1, action, command, default);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var db = f.Db;
            db.DataStatuses.Add(new() { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
            db.Roles.AddRange(new Role { RoleId = 3, RoleName = RoleNames.HallManager }, new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
            db.Statuses.AddRange(S(101, StatusGroups.Account, AccountStatusCodes.Active), S(201, StatusGroups.Hall, "ACTIVE"), S(301, StatusGroups.HallSchedule, "BOOKED"),
                S(802, StatusGroups.Booking, BookingStatusCodes.Confirmed), S(804, StatusGroups.Booking, BookingStatusCodes.Completed), S(805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
                S(1101, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Assigned), S(1102, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Completed), S(1103, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Cancelled));
            foreach (var (employee, user, role) in new[] { (10, 100, 3), (11, 101, 3), (20, 200, 4), (21, 201, 4) })
            {
                db.Users.Add(new User { UserId = user, RoleId = (byte)role, Email = $"u{user}@test.local", PasswordHash = "test", StatusId = 101, DataStatusId = 1 });
                db.Employees.Add(new Employee { EmployeeId = employee, UserId = user, EmployeeCode = $"NV{employee}", FullName = $"Nhân viên {employee}", PhoneNumber = $"090{user:0000000}", DataStatusId = 1 });
            }
            db.Halls.Add(new Hall { HallId = 1, HallCode = "S1", HallName = "Sảnh 1", MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1 });
            db.HallManagerAssignments.Add(new() { HallId = 1, HallManagerEmployeeId = 10, FromDate = RiversideBusinessClock.Today.AddDays(-10) });
            db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "KH1", FullName = "Khách 1", PhoneNumber = "0900000001", DataStatusId = 1 });
            db.HallSchedules.Add(new HallSchedule { HallScheduleId = 1, HallId = 1, Date = RiversideBusinessClock.Today.AddDays(2), Shift = "Ca tối", StatusId = 301, DataStatusId = 1 });
            db.WeddingBookings.Add(new WeddingBooking { BookingId = 1, BookingCode = "DT1", CustomerId = 1, HallScheduleId = 1, GuestCount = 100, StatusId = 802, BookedAt = RiversideBusinessClock.Now });
            await db.SaveChangesAsync();
            var assigned = await f.Assignments.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
            Assert.True(assigned.Succeeded, assigned.Error); return f;
        }
        private static Status S(int id, string group, string code) => new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

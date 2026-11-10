using System.Data;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Checklist;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class ChecklistService(ApplicationDbContext db, TimeProvider? timeProvider = null)
{
    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime;
    private async Task<Employee?> ActorAsync(int userId, string role, CancellationToken ct) =>
        await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId &&
            e.User.Role.RoleName == role && e.User.Status.StatusCode == AccountStatusCodes.Active &&
            e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing && e.DataStatus.DataStatusCode == DataStatusCodes.Existing, ct);

    private IQueryable<WeddingBooking> Scope(int userId, string role)
    {
        var query = db.WeddingBookings.AsQueryable();
        if (role == RoleNames.HallManager)
        {
            var halls = HallManagerQueryScope.CurrentHallIds(db, userId, RiversideBusinessClock.Today);
            return query.Where(b => halls.Contains(b.HallSchedule.HallId));
        }
        if (role == RoleNames.Coordinator)
            return query.Where(b => b.CoordinationAssignments.Any(a => a.Coordinator.UserId == userId &&
                (a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned ||
                 ((b.Status.StatusCode == BookingStatusCodes.Cancelled || b.Status.StatusCode == BookingStatusCodes.Completed) &&
                  !b.CoordinationAssignments.Any(current => current.CoordinationAssignmentId > a.CoordinationAssignmentId)))));
        return query.Where(_ => false);
    }

    public async Task<ServiceResult<int>> IncompleteCountAsync(int userId, string role, CancellationToken ct)
    {
        if (await ActorAsync(userId, role, ct) is null) return ServiceResult<int>.Failure("Không có quyền truy cập checklist.", 403);
        var ids = Scope(userId, role).Where(b => b.Status.StatusCode != BookingStatusCodes.Completed &&
            b.Status.StatusCode != BookingStatusCodes.Cancelled && b.CoordinationAssignments.Any(a =>
                a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)).Select(b => b.BookingId);
        var now = Now;
        var lists = await db.PartyChecklists.AsNoTracking().Where(c => ids.Contains(c.BookingId))
            .Include(c => c.Tasks).Include(c => c.History).Include(c => c.Booking).ThenInclude(b => b.HallSchedule)
            .AsSplitQuery().ToListAsync(ct);
        var count = lists.Where(c => c.State == "active" || ChecklistTimingRules.NeedsRepair(c, c.Booking.HallSchedule, now))
            .Sum(c => c.Tasks.Count(t => t.Status != "done" || !ChecklistTimingRules.ValidCompletion(t, c.Booking.HallSchedule, now)));
        return ServiceResult<int>.Success(count);
    }

    public async Task<ServiceResult<ChecklistWorkspaceDto>> GetAsync(int userId, string role, CancellationToken ct)
    {
        if (await ActorAsync(userId, role, ct) is null)
            return ServiceResult<ChecklistWorkspaceDto>.Failure("Không có quyền truy cập checklist.", 403);
        var bookings = await Scope(userId, role).AsNoTracking()
            .Where(b => b.Status.StatusCode == BookingStatusCodes.Confirmed || b.Status.StatusCode == BookingStatusCodes.Preparing ||
                db.PartyChecklists.Any(c => c.BookingId == b.BookingId))
            .Include(b => b.Status).Include(b => b.Customer).Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Status)
            .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Coordinator)
            .OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.BookingId).AsSplitQuery().ToListAsync(ct);
        var ids = bookings.Select(b => b.BookingId).ToArray();
        var checklists = await db.PartyChecklists.AsNoTracking().Where(c => ids.Contains(c.BookingId))
            .Include(c => c.Tasks).Include(c => c.History).AsSplitQuery().ToDictionaryAsync(c => c.BookingId, ct);
        var incidents = await db.Incidents.AsNoTracking().Where(i => ids.Contains(i.BookingId)).Include(i => i.Status).ToListAsync(ct);
        var dto = new ChecklistWorkspaceDto { Role = role == RoleNames.HallManager ? "manager" : "coordinator", Today = RiversideBusinessClock.Today.ToString("yyyy-MM-dd") };
        var now = Now;
        foreach (var booking in bookings)
        {
            checklists.TryGetValue(booking.BookingId, out var checklist);
            var assigned = booking.CoordinationAssignments.FirstOrDefault(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned);
            var state = booking.Status.StatusCode == BookingStatusCodes.Cancelled ? "cancelled" :
                booking.Status.StatusCode == BookingStatusCodes.Completed ? (checklist?.State == "approved" ? "approved" : "closed") :
                assigned is null ? "unassigned" : checklist?.State ?? "missing";
            var window = ChecklistTimingRules.Window(booking.HallSchedule);
            var timingRepair = checklist is not null && ChecklistTimingRules.NeedsRepair(checklist, booking.HallSchedule, now);
            var handoverReady = window.HasValue && now >= window.Value.End && !timingRepair;
            dto.Parties.Add(new ChecklistPartyDto
            {
                Id = booking.BookingId, Code = booking.BookingCode, Customer = booking.Customer.FullName,
                Date = booking.HallSchedule.Date.ToString("yyyy-MM-dd"), Shift = booking.HallSchedule.Shift,
                Hall = booking.HallSchedule.Hall.HallName, Tables = (int)Math.Ceiling(booking.GuestCount / 10m),
                Coordinator = assigned?.Coordinator.FullName, State = state, BookingState = booking.Status.StatusName,
                Version = checklist?.Version ?? 0, ReturnReason = checklist?.ReturnReason,
                CanEdit = role == RoleNames.Coordinator && state == "active" && assigned?.Coordinator.UserId == userId,
                CanManage = role == RoleNames.HallManager && state is not ("approved" or "closed" or "cancelled"),
                CanRepairTiming = role == RoleNames.HallManager && assigned is not null && (state is "active" or "review" or "approved") && timingRepair,
                NeedsTimingRepair = timingRepair,
                CanHandover = handoverReady,
                ShiftHours = window.HasValue ? $"{window.Value.Start:HH:mm}–{window.Value.End:HH:mm}" : null,
                TimingMessage = timingRepair ? "Checklist có kết quả hoặc bàn giao sai thời điểm; quản lý cần mở lại để kiểm tra." :
                    !window.HasValue ? "Chưa xác định được giờ ca; không thể hoàn tất công việc trong/sau tiệc hoặc bàn giao." :
                    now < window.Value.End ? $"Chỉ gửi và duyệt bàn giao từ {window.Value.End:HH:mm dd/MM/yyyy} (giờ Việt Nam)." : null,
                Tasks = checklist?.Tasks.OrderBy(t => t.Id).Select(t => new ChecklistTaskDto(t.Id, t.Name, t.Group,
                    booking.HallSchedule.Date.Date.AddDays(t.DueOffsetDays).ToString("yyyy-MM-dd"), t.Required,
                    t.Status, t.Note, t.UpdatedBy, t.UpdatedAt,
                    t.Group == "Trước tiệc" || ChecklistTimingRules.OpensAt(t, booking.HallSchedule) is { } start && now >= start,
                    ChecklistTimingRules.OpensAt(t, booking.HallSchedule) is { } opens && now >= opens,
                    t.Status == "done" && !ChecklistTimingRules.ValidCompletion(t, booking.HallSchedule, now) ?
                        "Kết quả hoàn thành chưa hợp lệ về thời gian; cần mở lại và thực hiện đúng thời điểm." :
                        TaskTimingMessage(t, booking.HallSchedule, now), t.CompletedAt,
                    ChecklistTimingRules.ValidCompletion(t, booking.HallSchedule, now))).ToList() ?? [],
                History = checklist?.History.OrderByDescending(h => h.Id).Select(h =>
                    new ChecklistHistoryDto(h.Id, h.Text, h.ActorName, h.Timestamp)).ToList() ?? [],
                Issues = incidents.Where(i => i.BookingId == booking.BookingId).Select(i =>
                    new ChecklistIssueDto(i.IncidentId, $"SC-{i.IncidentId}", i.IncidentType, i.Description,
                        i.Status.StatusCode == BusinessStatusCodes.Resolved)).ToList()
            });
        }
        return ServiceResult<ChecklistWorkspaceDto>.Success(dto);
    }

    public async Task<ServiceResult<string>> ExecuteAsync(int userId, string role, int bookingId, string action,
        ChecklistCommand command, CancellationToken ct)
    {
        // The same resource is locked by assignment changes. Authorization is rechecked under the lock.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            if (db.Database.IsSqlServer())
            {
                var resource = $"coordination:booking:{bookingId}";
                await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51001, 'Checklist is busy', 1;", ct);
            }
            var actor = await ActorAsync(userId, role, ct);
            var booking = actor is null ? null : await Scope(userId, role).Include(b => b.Status)
                .Include(b => b.HallSchedule).Include(b => b.CoordinationAssignments).ThenInclude(a => a.Status)
                .SingleOrDefaultAsync(b => b.BookingId == bookingId, ct);
            if (booking is null) return Fail("Không tìm thấy tiệc hoặc bạn không còn quyền truy cập.", 404);
            if (booking.Status.StatusCode is BookingStatusCodes.Cancelled or BookingStatusCodes.Completed)
                return Fail("Tiệc đã kết thúc hoặc đã hủy; checklist chỉ được xem lại.", 409);
            if (!booking.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned))
                return Fail("Tiệc chưa có điều phối viên đang phụ trách.", 409);
            var checklist = await db.PartyChecklists.Include(c => c.Tasks).Include(c => c.History).SingleOrDefaultAsync(c => c.BookingId == bookingId, ct);
            var now = Now;
            if (action == "initialize")
            {
                if (role != RoleNames.HallManager) return Fail("Chỉ quản lý sảnh được tạo checklist.", 403);
                if (checklist is not null) return Fail("Checklist đã tồn tại. Vui lòng tải lại.", 409);
                await ChecklistLifecycle.EnsureAsync(db, bookingId, userId, actor!.FullName, ct);
            }
            else
            {
                if (checklist is null) return Fail("Checklist chưa được tạo.", 409);
                if (command.Version != checklist.Version) return Fail("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trước khi thực hiện.", 409);
                if (checklist.State == "cancelled" || checklist.State == "approved" && action != "repair-timing") return Fail("Checklist đã khóa.", 409);
                var managerAction = action is "add" or "delete" or "approve" or "return" or "repair-timing";
                if (managerAction && role != RoleNames.HallManager || !managerAction && role != RoleNames.Coordinator)
                    return Fail("Bạn không có quyền thực hiện thao tác này.", 403);
                var task = checklist.Tasks.FirstOrDefault(t => t.Id == command.TaskId);
                string text;
                switch (action)
                {
                    case "task":
                        if (checklist.State != "active" || task is null) return Fail("Không thể cập nhật công việc này.", 409);
                        if (command.Status is not ("todo" or "doing" or "done")) return Fail("Trạng thái công việc không hợp lệ.");
                        if ((command.Status == "done" || command.Status == "doing" && task.Group != "Trước tiệc") &&
                            (ChecklistTimingRules.OpensAt(task, booking.HallSchedule) is not { } opens || now < opens))
                            return Fail(TaskTimingMessage(task, booking.HallSchedule, now) ?? "Chưa đến thời điểm thực hiện công việc.", 409);
                        text = $"Công việc “{task.Name}”: {TaskLabel(task.Status)} → {TaskLabel(command.Status)}.";
                        task.Status = command.Status; task.UpdatedBy = actor!.FullName; task.UpdatedAt = now;
                        break;
                    case "note":
                        if (checklist.State != "active" || task is null) return Fail("Không thể ghi chú công việc này.", 409);
                        task.Note = command.Note?.Trim(); task.UpdatedBy = actor!.FullName; task.UpdatedAt = RiversideBusinessClock.Now;
                        text = $"Cập nhật ghi chú “{task.Name}”: {task.Note ?? "(trống)"}";
                        break;
                    case "add":
                        if (string.IsNullOrWhiteSpace(command.Name) || command.Group is not ("Trước tiệc" or "Trong tiệc" or "Sau tiệc") || !command.DueDate.HasValue)
                            return Fail("Vui lòng nhập tên, giai đoạn và hạn công việc.");
                        var offset = (command.DueDate.Value.Date - booking.HallSchedule.Date.Date).Days;
                        if (Math.Abs(offset) > 365) return Fail("Hạn công việc phải trong vòng 365 ngày so với ngày tiệc.");
                        checklist.Tasks.Add(new PartyChecklistTask { Name = command.Name.Trim(), Group = command.Group,
                            DueOffsetDays = offset, Required = command.Required });
                        checklist.State = "active";
                        text = $"Thêm công việc “{command.Name.Trim()}”, hạn {command.DueDate:dd/MM/yyyy}. Checklist cần được gửi bàn giao lại.";
                        break;
                    case "delete":
                        if (checklist.State is not ("active" or "review") || task is null)
                            return Fail("Không thể xóa công việc này. Vui lòng tải lại checklist.", 409);
                        if ((command.Note?.Trim().Length ?? 0) < 3) return Fail("Vui lòng nhập lý do xóa công việc (ít nhất 3 ký tự).");
                        text = $"Xóa công việc #{task.Id} “{task.Name}” ({task.Group}; hạn {booking.HallSchedule.Date.Date.AddDays(task.DueOffsetDays):dd/MM/yyyy}; " +
                            $"{(task.Required ? "bắt buộc" : "không bắt buộc")}; trạng thái: {TaskLabel(task.Status)}; hoàn thành: {task.CompletedAt:HH:mm:ss dd/MM/yyyy}). " +
                            $"Ghi chú cũ: {task.Note ?? "(trống)"}. Lý do: {command.Note!.Trim()}. Checklist cần được gửi bàn giao lại.";
                        db.PartyChecklistTasks.Remove(task);
                        checklist.Tasks.Remove(task);
                        checklist.State = "active"; checklist.ReturnReason = null;
                        break;
                    case "submit":
                        if (HandoverError(checklist, booking.HallSchedule, now) is { } submitError) return Fail(submitError, 409);
                        if (checklist.State != "active" || checklist.Tasks.Count == 0 || checklist.Tasks.Any(t => t.Required && t.Status != "done"))
                            return Fail("Phải hoàn thành tất cả công việc bắt buộc trước khi gửi bàn giao.", 409);
                        checklist.State = "review"; checklist.ReturnReason = null;
                        text = $"Gửi bàn giao, chờ quản lý kiểm tra. {command.Note?.Trim()}";
                        break;
                    case "approve":
                        if (HandoverError(checklist, booking.HallSchedule, now) is { } approveError) return Fail(approveError, 409);
                        if (checklist.State != "review" || checklist.Tasks.Any(t => t.Required && t.Status != "done")) return Fail("Checklist chưa đủ điều kiện duyệt bàn giao.", 409);
                        checklist.State = "approved";
                        text = $"Xác nhận bàn giao checklist, giữ nguyên trạng thái tiệc. {command.Note?.Trim()}";
                        break;
                    case "return":
                        if (checklist.State != "review" || task is null || (command.Note?.Trim().Length ?? 0) < 3) return Fail("Vui lòng chọn công việc và nhập lý do yêu cầu làm lại.");
                        checklist.State = "active"; checklist.ReturnReason = command.Note!.Trim();
                        task.Status = "todo"; task.UpdatedBy = actor!.FullName; task.UpdatedAt = now;
                        text = $"Yêu cầu làm lại “{task.Name}”: {checklist.ReturnReason}";
                        break;
                    case "repair-timing":
                        if (!ChecklistTimingRules.NeedsRepair(checklist, booking.HallSchedule, now)) return Fail("Checklist không có kết quả sai thời điểm cần mở lại.", 409);
                        if ((command.Note?.Trim().Length ?? 0) < 3) return Fail("Vui lòng nhập lý do mở lại checklist.");
                        foreach (var invalid in checklist.Tasks.Where(t => t.Status == "done" && !ChecklistTimingRules.ValidCompletion(t, booking.HallSchedule, now)))
                        {
                            ChecklistLifecycle.AddHistory(checklist, userId, actor!.FullName, "TIMING_RESET",
                                $"Mở lại công việc “{invalid.Name}” do hoàn thành sai thời điểm hoặc thiếu thời điểm hoàn thành; thời điểm cũ: {invalid.CompletedAt:HH:mm:ss dd/MM/yyyy}. {command.Note!.Trim()}", now);
                            invalid.Status = "todo"; invalid.UpdatedBy = actor.FullName; invalid.UpdatedAt = now;
                        }
                        checklist.State = "active"; checklist.ReturnReason = command.Note!.Trim();
                        text = $"Mở lại checklist do sai thời điểm. Giữ nguyên lịch sử và các công việc hoàn thành hợp lệ. {checklist.ReturnReason}";
                        break;
                    default: return Fail("Thao tác không hợp lệ.");
                }
                checklist.Version++;
                ChecklistLifecycle.AddHistory(checklist, userId, actor!.FullName,
                    action == "task" ? $"TASK:{task!.Id}" : action.ToUpperInvariant(), text, now);
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return ServiceResult<string>.Success("Đã lưu thay đổi checklist.");
        }
        catch (DbUpdateConcurrencyException) { return Fail("Checklist vừa thay đổi. Vui lòng tải lại.", 409); }
        catch (DbUpdateException) { return Fail("Không lưu được do dữ liệu vừa thay đổi. Vui lòng tải lại.", 409); }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 1205 or 51001)
        { return Fail("Tiệc đang được cập nhật bởi yêu cầu khác. Vui lòng thử lại.", 409); }
    }
    private static string? TaskTimingMessage(PartyChecklistTask task, HallSchedule schedule, DateTime now)
    {
        var opens = ChecklistTimingRules.OpensAt(task, schedule);
        if (!opens.HasValue) return "Chưa xác định được giờ ca để mở công việc.";
        if (now >= opens.Value) return null;
        return $"{(task.Group == "Trước tiệc" ? "Được hoàn thành" : "Được thực hiện và hoàn thành")} từ {opens.Value:HH:mm dd/MM/yyyy} (giờ Việt Nam).";
    }
    private static string? HandoverError(PartyChecklist checklist, HallSchedule schedule, DateTime now)
    {
        var window = ChecklistTimingRules.Window(schedule);
        if (!window.HasValue) return "Chưa xác định được giờ kết thúc ca để bàn giao.";
        if (now < window.Value.End) return $"Chỉ gửi và duyệt bàn giao từ {window.Value.End:HH:mm dd/MM/yyyy} (giờ Việt Nam).";
        if (checklist.Tasks.Any(t => t.Status == "done" && !ChecklistTimingRules.ValidCompletion(t, schedule, now)))
            return "Có công việc hoàn thành sai thời điểm; cần mở lại và thực hiện đúng thời gian trước khi bàn giao.";
        return null;
    }
    private static string TaskLabel(string value) => value switch { "done" => "Hoàn thành", "doing" => "Đang thực hiện", _ => "Chưa làm" };
    private static ServiceResult<string> Fail(string message, int status = 400) => ServiceResult<string>.Failure(message, status);
}

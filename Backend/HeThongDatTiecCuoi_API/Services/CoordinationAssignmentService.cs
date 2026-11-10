using System.Data;
using System.Data.Common;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class CoordinationAssignmentService : ICoordinationAssignmentService
{
    private const string StateAssigned = "ASSIGNED";
    private const string StateUnassigned = "UNASSIGNED";
    private const string StateConflict = "CONFLICT";
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<CoordinationAssignmentService>? _logger;

    public CoordinationAssignmentService(ApplicationDbContext context, INotificationService notifications,
        ILogger<CoordinationAssignmentService>? logger = null)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId,
        HallManagerCoordinationQuery filter, CancellationToken cancellationToken)
    {
        var employee = await ActiveHallManagerAsync(userId, cancellationToken);
        if (employee is null)
            return ServiceResult<HallManagerCoordinationListDto>.Failure("Không tìm thấy hồ sơ Quản lý sảnh hợp lệ.", StatusCodes.Status403Forbidden);

        var today = DateTime.Today;
        var managedHallIds = CurrentManagedHallIds(employee.EmployeeId, today);
        var query = _context.WeddingBookings.AsNoTracking()
            .Where(b => managedHallIds.Contains(b.HallSchedule.HallId) && b.HallSchedule.Date >= today);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(b => b.BookingCode.Contains(term) || b.Customer.FullName.Contains(term) ||
                b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
                    a.Coordinator.FullName.Contains(term)));
        }
        if (filter.HallId is > 0) query = query.Where(b => b.HallSchedule.HallId == filter.HallId);
        if (filter.EventDate.HasValue) query = query.Where(b => b.HallSchedule.Date == filter.EventDate.Value.Date);

        var conflicts = HallManagerQueryScope.ConflictBookingIds(_context);
        var state = filter.CoordinationStatus?.Trim().ToUpperInvariant();
        if (state is StateAssigned or StateUnassigned or StateConflict)
            query = query.Where(b => b.Status.StatusCode == BookingStatusCodes.Confirmed || b.Status.StatusCode == BookingStatusCodes.Preparing);
        if (state == StateAssigned)
            query = query.Where(b => b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned) &&
                !conflicts.Contains(b.BookingId));
        else if (state == StateUnassigned)
            query = query.Where(b => !b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned));
        else if (state == StateConflict)
            query = query.Where(b => conflicts.Contains(b.BookingId));
        else if (state == "CANCELLED") query = query.Where(b => b.Status.StatusCode == BookingStatusCodes.Cancelled);
        else if (state == "COMPLETED") query = query.Where(b => b.Status.StatusCode == BookingStatusCodes.Completed);
        else if (state == "INELIGIBLE") query = query.Where(b => b.Status.StatusCode != BookingStatusCodes.Confirmed &&
            b.Status.StatusCode != BookingStatusCodes.Preparing && b.Status.StatusCode != BookingStatusCodes.Cancelled && b.Status.StatusCode != BookingStatusCodes.Completed);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var total = await query.CountAsync(cancellationToken);
        var raw = await query.OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.HallSchedule.Shift)
            .ThenBy(b => b.BookingCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.BookingId, b.BookingCode, EventDate = b.HallSchedule.Date, b.HallSchedule.Shift,
                b.HallSchedule.HallId, b.HallSchedule.Hall.HallName, CustomerName = b.Customer.FullName,
                b.GuestCount, BookingStatusCode = b.Status.StatusCode, BookingStatusName = b.Status.StatusName,
                Coordinator = b.CoordinationAssignments
                    .Where(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)
                    .Select(a => new { Id = (int?)a.CoordinatorEmployeeId, Name = a.Coordinator.FullName }).FirstOrDefault(),
                HasConflict = conflicts.Contains(b.BookingId)
            }).ToListAsync(cancellationToken);

        var eligible = ScopedEligibleBookings(managedHallIds, today);
        var assignedEligible = eligible.Where(b => b.CoordinationAssignments.Any(a =>
            a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned));
        var dto = new HallManagerCoordinationListDto
        {
            Items = raw.Select(x => new HallManagerCoordinationItemDto
            {
                BookingId = x.BookingId, BookingCode = x.BookingCode, EventDate = x.EventDate, Shift = x.Shift,
                HallId = x.HallId, HallName = x.HallName, CustomerName = x.CustomerName,
                TableCount = (int)Math.Ceiling(x.GuestCount / 10m), BookingStatusCode = x.BookingStatusCode,
                BookingStatusName = x.BookingStatusName, CoordinatorEmployeeId = x.Coordinator?.Id,
                CoordinatorName = x.Coordinator?.Name, HasConflict = IsEligible(x.BookingStatusCode, x.EventDate) && x.HasConflict,
                CoordinationState = DisplayState(x.BookingStatusCode, x.EventDate, x.Coordinator?.Id, x.HasConflict),
                IneligibilityReason = IneligibilityReason(x.BookingStatusCode, x.EventDate),
                CanAssign = IsEligible(x.BookingStatusCode, x.EventDate) && x.Coordinator is null,
                CanChangeOrCancel = IsEligible(x.BookingStatusCode, x.EventDate) && x.Coordinator is not null
            }).ToList(),
            Page = page, PageSize = pageSize, TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Halls = await _context.Halls.AsNoTracking().Where(h => managedHallIds.Contains(h.HallId))
                .OrderBy(h => h.HallName).Select(h => new EventCoordinationOptionDto
                { Value = h.HallId.ToString(), Label = h.HallName }).ToListAsync(cancellationToken),
            Kpi = new HallManagerCoordinationKpiDto
            {
                TotalRequired = await eligible.CountAsync(cancellationToken),
                Assigned = await assignedEligible.CountAsync(cancellationToken),
                Unassigned = await eligible.CountAsync(b => !b.CoordinationAssignments.Any(a =>
                    a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned), cancellationToken),
                Conflicts = await eligible.CountAsync(b => conflicts.Contains(b.BookingId), cancellationToken)
            }
        };
        return ServiceResult<HallManagerCoordinationListDto>.Success(dto);
    }

    public async Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId,
        int bookingId, CancellationToken cancellationToken)
    {
        var scoped = await ScopedBookingSnapshotAsync(userId, bookingId, cancellationToken);
        if (scoped is null)
            return ServiceResult<List<CoordinatorAvailabilityDto>>.Failure("Không tìm thấy tiệc hoặc tiệc không thuộc sảnh bạn đang phụ trách.", StatusCodes.Status404NotFound);

        var coordinators = await _context.Employees.AsNoTracking()
            .Where(e => e.User.Role.RoleName == RoleNames.Coordinator &&
                e.User.Status.StatusCode == AccountStatusCodes.Active &&
                e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                e.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(e => e.FullName).Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName }).ToListAsync(cancellationToken);
        var busyIds = await _context.CoordinationAssignments.AsNoTracking().Where(a =>
                a.BookingId != bookingId && a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
                a.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
                a.Booking.Status.StatusCode != BookingStatusCodes.Completed &&
                a.Booking.HallSchedule.Date == scoped.EventDate && a.Booking.HallSchedule.Shift == scoped.Shift)
            .Select(a => a.CoordinatorEmployeeId).Distinct().ToListAsync(cancellationToken);
        return ServiceResult<List<CoordinatorAvailabilityDto>>.Success(coordinators.Select(e => new CoordinatorAvailabilityDto
        {
            EmployeeId = e.EmployeeId, EmployeeCode = e.EmployeeCode, FullName = e.FullName,
            IsAvailable = !busyIds.Contains(e.EmployeeId),
            AvailabilityMessage = busyIds.Contains(e.EmployeeId) ? $"Bận {scoped.Shift} ngày {scoped.EventDate:dd/MM/yyyy}" : "Rảnh"
        }).ToList());
    }

    public async Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId,
        int bookingId, CancellationToken cancellationToken)
    {
        var snapshot = await ScopedBookingSnapshotAsync(userId, bookingId, cancellationToken);
        if (snapshot is null)
            return ServiceResult<HallManagerCoordinationDetailDto>.Failure("Không tìm thấy tiệc hoặc tiệc không thuộc sảnh bạn đang phụ trách.", StatusCodes.Status404NotFound);
        var history = await _context.CoordinationAssignments.AsNoTracking().Where(a => a.BookingId == bookingId)
            .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.CoordinationAssignmentId)
            .Select(a => new CoordinationAssignmentHistoryDto
            {
                CoordinationAssignmentId = a.CoordinationAssignmentId, CoordinatorEmployeeId = a.CoordinatorEmployeeId,
                CoordinatorCode = a.Coordinator.EmployeeCode, CoordinatorName = a.Coordinator.FullName,
                AssignedByName = a.AssignedByHallManager.FullName, StatusCode = a.Status.StatusCode,
                StatusName = a.Status.StatusName, AssignedAt = a.AssignedAt
            }).ToListAsync(cancellationToken);
        var audits = await _context.AuditLogs.AsNoTracking().Where(a =>
                a.EntityName == AuditEntityNames.CoordinationAssignment && a.EntityId == bookingId)
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.AuditLogId)
            .Select(a => new CoordinationAuditDto
            {
                Action = a.Action, ActorName = a.User != null && a.User.Employee != null ? a.User.Employee.FullName : "Hệ thống",
                Timestamp = a.Timestamp, Reason = a.Notes, OldData = a.OldData, NewData = a.NewData
            }).ToListAsync(cancellationToken);
        var active = history.FirstOrDefault(a => a.StatusCode == CoordinationAssignmentStatusCodes.Assigned);
        return ServiceResult<HallManagerCoordinationDetailDto>.Success(new HallManagerCoordinationDetailDto
        {
            Booking = ToItem(snapshot, active?.CoordinatorEmployeeId, active?.CoordinatorName), History = history, AuditTrail = audits
        });
    }

    public Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId,
        CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken) =>
        MutateAsync(userId, bookingId, request.CoordinatorEmployeeId, null, false, cancellationToken);

    public Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId,
        ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken) =>
        MutateAsync(userId, bookingId, request.CoordinatorEmployeeId, request.Reason, true, cancellationToken);

    public async Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId,
        CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason.Trim();
        if (reason.Length < 3)
            return ServiceResult<CoordinationMutationResultDto>.Failure("Vui lòng nhập lý do hủy phân công.", StatusCodes.Status400BadRequest);
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        try
        {
            if (transaction is not null) await AcquireApplicationLockAsync($"coordination:booking:{bookingId}", cancellationToken);
            var actor = await ActiveHallManagerAsync(userId, cancellationToken);
            var booking = actor is null ? null : await ScopedBookingAsync(actor.EmployeeId, bookingId, tracking: true, cancellationToken);
            if (booking is null)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Không tìm thấy tiệc hoặc bạn không còn phụ trách sảnh này.", StatusCodes.Status404NotFound);
            if (!IsEligible(booking.Status.StatusCode, booking.HallSchedule.Date))
                return ServiceResult<CoordinationMutationResultDto>.Failure("Trạng thái hoặc ngày tổ chức của tiệc không cho phép hủy phân công.", StatusCodes.Status409Conflict);
            var active = booking.CoordinationAssignments.SingleOrDefault(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned);
            if (active is null)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Tiệc chưa có phân công đang hiệu lực.", StatusCodes.Status409Conflict);
            var cancelledId = await StatusIdAsync(CoordinationAssignmentStatusCodes.Cancelled, cancellationToken);
            active.StatusId = cancelledId;
            await ChecklistLifecycle.RecordAsync(_context, bookingId, userId, actor!.FullName,
                "ASSIGNMENT_CANCELLED", $"Hủy phân công điều phối. Giữ lại checklist và lịch sử. Lý do: {reason}", null, cancellationToken);
            var oldEmployeeId = active.CoordinatorEmployeeId;
            AddAudit(userId, bookingId, AuditActions.CoordinationCancelled,
                new { CoordinatorEmployeeId = oldEmployeeId, Status = CoordinationAssignmentStatusCodes.Assigned },
                new { CoordinatorEmployeeId = oldEmployeeId, Status = CoordinationAssignmentStatusCodes.Cancelled }, reason);
            await _notifications.AddAsync(active.Coordinator.UserId, userId, NotificationTypeCodes.CoordinationCancelled,
                "Phân công đã bị hủy", $"Phân công tiệc {booking.BookingCode} đã bị hủy. Lý do: {reason}",
                AuditEntityNames.CoordinationAssignment, bookingId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ServiceResult<CoordinationMutationResultDto>.Success(new CoordinationMutationResultDto
            { BookingId = bookingId, AssignmentId = active.CoordinationAssignmentId, Message = "Đã hủy phân công và lưu lịch sử." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<CoordinationMutationResultDto>.Failure("Phân công vừa được thay đổi bởi yêu cầu khác.", StatusCodes.Status409Conflict);
        }
    }

    public async Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees.AsNoTracking().Where(e => e.UserId == userId &&
                e.User.Role.RoleName == RoleNames.Coordinator && e.User.Status.StatusCode == AccountStatusCodes.Active &&
                e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing && e.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(e => new { e.EmployeeId }).SingleOrDefaultAsync(cancellationToken);
        if (employee is null)
            return ServiceResult<List<CoordinatorAssignedPartyDto>>.Failure("Không tìm thấy hồ sơ Điều phối viên hợp lệ.", StatusCodes.Status403Forbidden);
        var items = await _context.WeddingBookings.AsNoTracking().Where(b =>
                b.Status.StatusCode != BookingStatusCodes.Cancelled && b.Status.StatusCode != BookingStatusCodes.Completed &&
                b.CoordinationAssignments.Any(a => a.CoordinatorEmployeeId == employee.EmployeeId &&
                    a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned))
            .OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.HallSchedule.Shift)
            .Select(b => new CoordinatorAssignedPartyDto
            {
                BookingId = b.BookingId, BookingCode = b.BookingCode, CustomerName = b.Customer.FullName,
                EventDate = b.HallSchedule.Date, Shift = b.HallSchedule.Shift, HallName = b.HallSchedule.Hall.HallName,
                TableCount = (int)Math.Ceiling(b.GuestCount / 10m), MenuName = b.Menu != null ? b.Menu.MenuName : null,
                DecorationName = b.DecorationPackage != null ? b.DecorationPackage.TenGoi : null,
                Services = b.BookingServices.OrderBy(s => s.ServiceItem.TenDichVu).Select(s => s.ServiceItem.TenDichVu).ToList(),
                SpecialRequests = b.SpecialRequests, BookingStatusCode = b.Status.StatusCode, BookingStatusName = b.Status.StatusName
            }).ToListAsync(cancellationToken);
        return ServiceResult<List<CoordinatorAssignedPartyDto>>.Success(items);
    }

    public async Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode, int? actorUserId,
        string? reason, CancellationToken cancellationToken)
    {
        var target = bookingStatusCode == BookingStatusCodes.Cancelled
            ? CoordinationAssignmentStatusCodes.Cancelled
            : bookingStatusCode == BookingStatusCodes.Completed ? CoordinationAssignmentStatusCodes.Completed : null;
        if (target is null) return;
        await ChecklistLifecycle.RecordAsync(_context, bookingId, actorUserId, "Hệ thống",
            "BOOKING_STATUS", $"Đồng bộ trạng thái tiệc: {bookingStatusCode}. {reason}",
            bookingStatusCode == BookingStatusCodes.Cancelled ? "cancelled" : null, cancellationToken);
        var assignments = await _context.CoordinationAssignments
            .Include(a => a.Coordinator).Include(a => a.Status)
            .Include(a => a.Booking).ThenInclude(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Where(a => a.BookingId == bookingId && a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0) return;
        var targetId = await StatusIdAsync(target, cancellationToken);
        foreach (var assignment in assignments)
        {
            assignment.StatusId = targetId;
            AddAudit(actorUserId, bookingId,
                target == CoordinationAssignmentStatusCodes.Completed ? AuditActions.CoordinationCompleted : AuditActions.CoordinationCancelled,
                new { assignment.CoordinatorEmployeeId, Status = CoordinationAssignmentStatusCodes.Assigned },
                new { assignment.CoordinatorEmployeeId, Status = target }, reason ?? $"Đồng bộ theo trạng thái tiệc {bookingStatusCode}.");
            var booking = assignment.Booking;
            var partyDescription = $"{booking.BookingCode} · {booking.HallSchedule.Date:dd/MM/yyyy} · {booking.HallSchedule.Shift} · {booking.HallSchedule.Hall.HallName}";
            var outcome = target == CoordinationAssignmentStatusCodes.Completed
                ? "Nhiệm vụ điều phối đã hoàn tất." : "Dừng thực hiện nhiệm vụ điều phối do tiệc đã bị hủy.";
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason.Trim()}";
            await _notifications.AddAsync(assignment.Coordinator.UserId, actorUserId,
                NotificationTypeCodes.CoordinationCancelled,
                target == CoordinationAssignmentStatusCodes.Completed ? "Tiệc đã hoàn tất" : "Tiệc đã bị hủy",
                $"{partyDescription}. {outcome}{reasonText} Mở checklist để xem lịch sử.",
                AuditEntityNames.CoordinationAssignment, bookingId, cancellationToken);
        }
    }

    public async Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId,
        int? excludedBookingId, CancellationToken cancellationToken)
    {
        var schedule = await _context.HallSchedules.AsNoTracking().Where(s => s.HallScheduleId == hallScheduleId)
            .Select(s => new { s.Date, s.Shift }).SingleOrDefaultAsync(cancellationToken);
        return schedule is not null && await HasConflictAsync(coordinatorEmployeeId, schedule.Date, schedule.Shift,
            excludedBookingId, cancellationToken);
    }

    private async Task<ServiceResult<CoordinationMutationResultDto>> MutateAsync(int userId, int bookingId,
        int coordinatorEmployeeId, string? reason, bool changing, CancellationToken cancellationToken)
    {
        reason = reason?.Trim();
        if (changing && (reason?.Length ?? 0) < 3)
            return ServiceResult<CoordinationMutationResultDto>.Failure("Vui lòng nhập lý do thay đổi điều phối viên.", StatusCodes.Status400BadRequest);
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        try
        {
            if (transaction is not null) await AcquireApplicationLockAsync($"coordination:booking:{bookingId}", cancellationToken);
            var actor = await ActiveHallManagerAsync(userId, cancellationToken);
            var booking = actor is null ? null : await ScopedBookingAsync(actor.EmployeeId, bookingId, tracking: true, cancellationToken);
            if (booking is null)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Không tìm thấy tiệc hoặc bạn không còn phụ trách sảnh này.", StatusCodes.Status404NotFound);
            if (!IsEligible(booking.Status.StatusCode, booking.HallSchedule.Date))
                return ServiceResult<CoordinationMutationResultDto>.Failure("Chỉ được phân công tiệc CONFIRMED/PREPARING chưa qua ngày tổ chức.", StatusCodes.Status409Conflict);
            var coordinator = await _context.Employees.Include(e => e.User).ThenInclude(u => u.Status)
                .Include(e => e.User).ThenInclude(u => u.Role).Include(e => e.User).ThenInclude(u => u.DataStatus)
                .Include(e => e.DataStatus).SingleOrDefaultAsync(e => e.EmployeeId == coordinatorEmployeeId, cancellationToken);
            if (coordinator is null || coordinator.User.Role.RoleName != RoleNames.Coordinator ||
                coordinator.User.Status.StatusCode != AccountStatusCodes.Active ||
                coordinator.User.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
                coordinator.DataStatus.DataStatusCode != DataStatusCodes.Existing)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Nhân viên điều phối không tồn tại hoặc tài khoản không hoạt động.", StatusCodes.Status400BadRequest);
            var active = booking.CoordinationAssignments.SingleOrDefault(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned);
            if (active is not null && active.CoordinatorEmployeeId == coordinatorEmployeeId)
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return ServiceResult<CoordinationMutationResultDto>.Success(new CoordinationMutationResultDto
                { BookingId = bookingId, AssignmentId = active.CoordinationAssignmentId, Message = "Điều phối viên này đang được phân công; không tạo bản ghi trùng." });
            }
            if (!changing && active is not null)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Tiệc đã có điều phối viên chính.", StatusCodes.Status409Conflict);
            if (changing && active is null)
                return ServiceResult<CoordinationMutationResultDto>.Failure("Tiệc chưa có phân công đang hiệu lực để thay đổi.", StatusCodes.Status409Conflict);

            if (transaction is not null)
                await AcquireApplicationLockAsync($"coordination:schedule:{coordinatorEmployeeId}:{booking.HallSchedule.Date:yyyyMMdd}:{booking.HallSchedule.Shift}", cancellationToken);
            if (await HasConflictAsync(coordinatorEmployeeId, booking.HallSchedule.Date, booking.HallSchedule.Shift, bookingId, cancellationToken))
                return ServiceResult<CoordinationMutationResultDto>.Failure(
                    $"{coordinator.FullName} đã được phân công một tiệc khác vào {booking.HallSchedule.Shift} ngày {booking.HallSchedule.Date:dd/MM/yyyy}.",
                    StatusCodes.Status409Conflict);

            int? oldCoordinatorId = null;
            string? oldCoordinatorName = null;
            if (active is not null)
            {
                oldCoordinatorId = active.CoordinatorEmployeeId;
                oldCoordinatorName = active.Coordinator.FullName;
                active.StatusId = await StatusIdAsync(CoordinationAssignmentStatusCodes.Cancelled, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await _notifications.AddAsync(active.Coordinator.UserId, userId, NotificationTypeCodes.CoordinationChanged,
                    "Thay đổi phân công", $"Bạn không còn phụ trách tiệc {booking.BookingCode}. Lý do: {reason}",
                    AuditEntityNames.CoordinationAssignment, bookingId, cancellationToken);
            }
            var assignment = new CoordinationAssignment
            {
                BookingId = bookingId, CoordinatorEmployeeId = coordinatorEmployeeId,
                AssignedByHallManagerEmployeeId = actor!.EmployeeId,
                StatusId = await StatusIdAsync(CoordinationAssignmentStatusCodes.Assigned, cancellationToken),
                AssignedAt = DateTime.Now
            };
            _context.CoordinationAssignments.Add(assignment);
            await ChecklistLifecycle.EnsureAsync(_context, bookingId, userId, actor!.FullName, cancellationToken);
            await ChecklistLifecycle.RecordAsync(_context, bookingId, userId, actor.FullName,
                active is null ? "ASSIGNED" : "REASSIGNED",
                active is null ? $"Phân công {coordinator.FullName} phụ trách tiệc." :
                    $"Đổi điều phối viên {oldCoordinatorName} → {coordinator.FullName}. Giữ nguyên checklist và tiến độ. Lý do: {reason}",
                null, cancellationToken);
            AddAudit(userId, bookingId, active is null ? AuditActions.CoordinationAssigned : AuditActions.CoordinationChanged,
                active is null ? null : new { CoordinatorEmployeeId = oldCoordinatorId, CoordinatorName = oldCoordinatorName, Status = CoordinationAssignmentStatusCodes.Assigned },
                new { CoordinatorEmployeeId = coordinatorEmployeeId, CoordinatorName = coordinator.FullName, Status = CoordinationAssignmentStatusCodes.Assigned },
                active is null ? "Phân công điều phối viên chính." : reason);
            await _notifications.AddAsync(coordinator.UserId, userId,
                active is null ? NotificationTypeCodes.CoordinationAssigned : NotificationTypeCodes.CoordinationChanged,
                active is null ? "Bạn được phân công tiệc mới" : "Bạn được nhận phân công thay thế",
                $"Bạn được phân công tiệc {booking.BookingCode}, {booking.HallSchedule.Shift} ngày {booking.HallSchedule.Date:dd/MM/yyyy} tại {booking.HallSchedule.Hall.HallName}.",
                AuditEntityNames.CoordinationAssignment, bookingId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ServiceResult<CoordinationMutationResultDto>.Success(new CoordinationMutationResultDto
            { BookingId = bookingId, AssignmentId = assignment.CoordinationAssignmentId,
                Message = active is null ? "Phân công điều phối viên thành công." : "Thay đổi điều phối viên thành công." },
                active is null ? StatusCodes.Status201Created : StatusCodes.Status200OK);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<CoordinationMutationResultDto>.Failure("Phân công vừa được người khác cập nhật. Vui lòng tải lại.", StatusCodes.Status409Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return ServiceResult<CoordinationMutationResultDto>.Failure("Phân công bị xung đột hoặc dữ liệu vừa được thay đổi. Vui lòng tải lại.", StatusCodes.Status409Conflict);
        }
        catch (DbUpdateException ex)
        {
            _logger?.LogError(ex, "Không lưu được phân công cho tiệc {BookingId}.", bookingId);
            return ServiceResult<CoordinationMutationResultDto>.Failure("Không thể lưu phân công do lỗi dữ liệu hệ thống. Vui lòng liên hệ quản trị viên để kiểm tra nhật ký lỗi.", StatusCodes.Status500InternalServerError);
        }
        catch (DBConcurrencyException)
        {
            return ServiceResult<CoordinationMutationResultDto>.Failure("Có yêu cầu phân công đồng thời. Vui lòng tải lại và thử lại.", StatusCodes.Status409Conflict);
        }
    }

    private async Task<Employee?> ActiveHallManagerAsync(int userId, CancellationToken cancellationToken) =>
        await _context.Employees.AsNoTracking().Where(e => e.UserId == userId && e.User.Role.RoleName == RoleNames.HallManager &&
            e.User.Status.StatusCode == AccountStatusCodes.Active && e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            e.DataStatus.DataStatusCode == DataStatusCodes.Existing).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<int> CurrentManagedHallIds(int employeeId, DateTime today) =>
        _context.HallManagerAssignments.Where(a => a.HallManagerEmployeeId == employeeId && a.FromDate <= today &&
            (!a.ToDate.HasValue || a.ToDate.Value >= today)).Select(a => a.HallId);

    private IQueryable<WeddingBooking> ScopedEligibleBookings(IQueryable<int> hallIds, DateTime today) =>
        _context.WeddingBookings.AsNoTracking().Where(b => hallIds.Contains(b.HallSchedule.HallId) &&
            b.HallSchedule.Date >= today && (b.Status.StatusCode == BookingStatusCodes.Confirmed ||
            b.Status.StatusCode == BookingStatusCodes.Preparing));

    private async Task<WeddingBooking?> ScopedBookingAsync(int hallManagerEmployeeId, int bookingId, bool tracking,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var query = tracking ? _context.WeddingBookings.AsQueryable() : _context.WeddingBookings.AsNoTracking();
        return await query.Include(b => b.Status).Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Status)
            .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Coordinator).ThenInclude(e => e.User)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId && b.HallSchedule.Hall.HallManagerAssignments.Any(a =>
                a.HallManagerEmployeeId == hallManagerEmployeeId && a.FromDate <= today &&
                (!a.ToDate.HasValue || a.ToDate.Value >= today)), cancellationToken);
    }

    private async Task<BookingSnapshot?> ScopedBookingSnapshotAsync(int userId, int bookingId, CancellationToken cancellationToken)
    {
        var actor = await ActiveHallManagerAsync(userId, cancellationToken);
        if (actor is null) return null;
        var today = DateTime.Today;
        return await _context.WeddingBookings.AsNoTracking().Where(b => b.BookingId == bookingId &&
                b.HallSchedule.Hall.HallManagerAssignments.Any(a => a.HallManagerEmployeeId == actor.EmployeeId &&
                    a.FromDate <= today && (!a.ToDate.HasValue || a.ToDate.Value >= today)))
            .Select(b => new BookingSnapshot(b.BookingId, b.BookingCode, b.Customer.FullName, b.HallSchedule.Date,
                b.HallSchedule.Shift, b.HallSchedule.HallId, b.HallSchedule.Hall.HallName, b.GuestCount,
                b.Status.StatusCode, b.Status.StatusName,
                HallManagerQueryScope.ConflictBookingIds(_context).Contains(b.BookingId))).SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> HasConflictAsync(int employeeId, DateTime date, string shift, int? excludedBookingId,
        CancellationToken cancellationToken) => _context.CoordinationAssignments.AsNoTracking().AnyAsync(a =>
            a.CoordinatorEmployeeId == employeeId && (!excludedBookingId.HasValue || a.BookingId != excludedBookingId.Value) &&
            a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
            a.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
            a.Booking.Status.StatusCode != BookingStatusCodes.Completed &&
            a.Booking.HallSchedule.Date == date.Date && a.Booking.HallSchedule.Shift == shift, cancellationToken);

    private Task<int> StatusIdAsync(string code, CancellationToken cancellationToken) =>
        _context.Statuses.Where(s => s.StatusGroup == StatusGroups.CoordinationAssignment && s.StatusCode == code)
            .Select(s => s.StatusId).SingleAsync(cancellationToken);

    private void AddAudit(int? userId, int bookingId, string action, object? oldData, object? newData, string? notes) =>
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, EntityName = AuditEntityNames.CoordinationAssignment,
            EntityId = bookingId, OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData is null ? null : JsonSerializer.Serialize(newData), Timestamp = DateTime.Now, Notes = notes
        });

    private async Task AcquireApplicationLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsSqlServer()) return;
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; SELECT @result;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0) throw new DBConcurrencyException("Không thể lấy khóa phân công trong thời gian cho phép.");
    }

    private static bool IsEligible(string status, DateTime eventDate) => eventDate.Date >= DateTime.Today &&
        status is BookingStatusCodes.Confirmed or BookingStatusCodes.Preparing;

    private static string DisplayState(string status, DateTime date, int? coordinatorId, bool conflict) =>
        !IsEligible(status, date) ? status switch
        {
            BookingStatusCodes.Cancelled => "CANCELLED",
            BookingStatusCodes.Completed => "COMPLETED",
            _ => "INELIGIBLE"
        } : conflict ? StateConflict : coordinatorId.HasValue ? StateAssigned : StateUnassigned;

    private static string? IneligibilityReason(string status, DateTime date) => status switch
    {
        BookingStatusCodes.Cancelled => "Tiệc đã hủy, không cần phân công và không tính vào KPI điều phối.",
        BookingStatusCodes.Completed => "Tiệc đã hoàn tất, không cần phân công và không tính vào KPI điều phối.",
        _ when date.Date < DateTime.Today => "Đã qua ngày tổ chức, chỉ xem lịch sử và không tính vào KPI điều phối.",
        _ when !IsEligible(status, date) => "Tiệc chưa đủ điều kiện phân công. Chỉ tiệc đã xác nhận hoặc đang chuẩn bị mới được tính vào KPI điều phối.",
        _ => null
    };

    private static HallManagerCoordinationItemDto ToItem(BookingSnapshot x, int? coordinatorId, string? coordinatorName) => new()
    {
        BookingId = x.BookingId, BookingCode = x.BookingCode, CustomerName = x.CustomerName,
        EventDate = x.EventDate, Shift = x.Shift, HallId = x.HallId, HallName = x.HallName,
        TableCount = (int)Math.Ceiling(x.GuestCount / 10m), BookingStatusCode = x.BookingStatusCode,
        BookingStatusName = x.BookingStatusName, CoordinatorEmployeeId = coordinatorId,
        CoordinatorName = coordinatorName, HasConflict = IsEligible(x.BookingStatusCode, x.EventDate) && x.HasConflict,
        CoordinationState = DisplayState(x.BookingStatusCode, x.EventDate, coordinatorId, x.HasConflict),
        IneligibilityReason = IneligibilityReason(x.BookingStatusCode, x.EventDate),
        CanAssign = IsEligible(x.BookingStatusCode, x.EventDate) && !coordinatorId.HasValue,
        CanChangeOrCancel = IsEligible(x.BookingStatusCode, x.EventDate) && coordinatorId.HasValue
    };

    private sealed record BookingSnapshot(int BookingId, string BookingCode, string CustomerName, DateTime EventDate,
        string Shift, int HallId, string HallName, int GuestCount, string BookingStatusCode,
        string BookingStatusName, bool HasConflict);
}

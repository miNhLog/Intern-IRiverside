using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Incident;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class IncidentService : IIncidentService
{
    private const string CoordinationAssigned = BusinessStatusCodes.Assigned;
    private static readonly HashSet<string> Severities =
    [
        IncidentSeverityCodes.Low,
        IncidentSeverityCodes.Normal,
        IncidentSeverityCodes.Critical
    ];
    private static readonly HashSet<string> IncidentStatuses =
    [
        BusinessStatusCodes.Pending,
        BusinessStatusCodes.Processing,
        BusinessStatusCodes.Resolved
    ];

    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;

    public IncidentService(ApplicationDbContext context, INotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<ServiceResult<IncidentListResponseDto>> GetListAsync(int userId, string role, string? search,
        string? status, string? severity, int? hallId, DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        if (!string.IsNullOrWhiteSpace(status) && !IncidentStatuses.Contains(status.Trim().ToUpperInvariant()))
            return ServiceResult<IncidentListResponseDto>.Failure("Trạng thái sự cố không hợp lệ.", StatusCodes.Status400BadRequest);
        if (!string.IsNullOrWhiteSpace(severity) && !Severities.Contains(severity.Trim()))
            return ServiceResult<IncidentListResponseDto>.Failure("Mức độ sự cố không hợp lệ.", StatusCodes.Status400BadRequest);
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            return ServiceResult<IncidentListResponseDto>.Failure("Ngày bắt đầu không được sau ngày kết thúc.", StatusCodes.Status400BadRequest);

        var scoped = Scope(role, userId, tracking: false);
        if (scoped is null)
            return ServiceResult<IncidentListResponseDto>.Failure("Bạn không có quyền truy cập sự cố.", StatusCodes.Status403Forbidden);

        var baseQuery = scoped;
        var kpi = new IncidentKpiDto
        {
            Total = await baseQuery.CountAsync(cancellationToken),
            Pending = await baseQuery.CountAsync(i => i.Status.StatusCode == BusinessStatusCodes.Pending, cancellationToken),
            Processing = await baseQuery.CountAsync(i => i.Status.StatusCode == BusinessStatusCodes.Processing, cancellationToken),
            Resolved = await baseQuery.CountAsync(i => i.Status.StatusCode == BusinessStatusCodes.Resolved, cancellationToken),
            Critical = await baseQuery.CountAsync(i => i.Severity == IncidentSeverityCodes.Critical &&
                i.Status.StatusCode != BusinessStatusCodes.Resolved, cancellationToken)
        };
        var halls = await baseQuery.Select(i => new IncidentHallOptionDto
            { HallId = i.Booking.HallSchedule.HallId, HallName = i.Booking.HallSchedule.Hall.HallName })
            .Distinct().OrderBy(h => h.HallName).ToListAsync(cancellationToken);

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i => i.Booking.BookingCode.Contains(term) ||
                i.Booking.HallSchedule.Hall.HallName.Contains(term) || i.IncidentType.Contains(term) ||
                i.Description.Contains(term) ||
                i.ReportedByEmployee != null && i.ReportedByEmployee.FullName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(i => i.Status.StatusCode == normalized);
        }
        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalized = severity.Trim();
            query = query.Where(i => i.Severity == normalized);
        }
        if (hallId.HasValue) query = query.Where(i => i.Booking.HallSchedule.HallId == hallId.Value);
        if (fromDate.HasValue) query = query.Where(i => i.OccurredAt >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(i => i.OccurredAt < toDate.Value.Date.AddDays(1));

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        var items = await query.OrderBy(i => i.Status.StatusCode == BusinessStatusCodes.Resolved)
            .ThenByDescending(i => i.Severity == IncidentSeverityCodes.Critical)
            .ThenByDescending(i => i.OccurredAt).ThenByDescending(i => i.IncidentId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new IncidentListItemDto
            {
                IncidentId = i.IncidentId,
                BookingId = i.BookingId,
                BookingCode = i.Booking.BookingCode,
                HallName = i.Booking.HallSchedule.Hall.HallName,
                IncidentType = i.IncidentType,
                Severity = i.Severity,
                StatusCode = i.Status.StatusCode,
                StatusName = i.Status.StatusName,
                OccurredAt = i.OccurredAt,
                ReporterName = i.ReportedByEmployee != null ? i.ReportedByEmployee.FullName : "Không xác định",
                CanEdit = i.ReportedByEmployee != null && i.ReportedByEmployee.UserId == userId &&
                    i.Status.StatusCode == BusinessStatusCodes.Pending ||
                    role != RoleNames.Coordinator && i.Status.StatusCode == BusinessStatusCodes.Pending
            }).ToListAsync(cancellationToken);

        return ServiceResult<IncidentListResponseDto>.Success(new IncidentListResponseDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalItems = total, TotalPages = totalPages, Kpi = kpi, Halls = halls
        });
    }

    public async Task<ServiceResult<List<IncidentBookingOptionDto>>> GetBookingOptionsAsync(int userId, string role,
        CancellationToken cancellationToken)
    {
        var query = ScopeBookings(role, userId);
        if (query is null)
            return ServiceResult<List<IncidentBookingOptionDto>>.Failure("Bạn không có quyền truy cập tiệc.", StatusCodes.Status403Forbidden);

        var items = await query.AsNoTracking()
            .Where(b => b.Status.StatusCode != BookingStatusCodes.Cancelled)
            .OrderByDescending(b => b.HallSchedule.Date).ThenBy(b => b.BookingCode)
            .Select(b => new IncidentBookingOptionDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                HallName = b.HallSchedule.Hall.HallName,
                EventDate = b.HallSchedule.Date,
                Shift = b.HallSchedule.Shift
            }).ToListAsync(cancellationToken);
        return ServiceResult<List<IncidentBookingOptionDto>>.Success(items);
    }

    public async Task<ServiceResult<IncidentDetailDto>> GetDetailAsync(int userId, string role, int incidentId,
        CancellationToken cancellationToken)
    {
        var query = Scope(role, userId, tracking: false);
        if (query is null)
            return ServiceResult<IncidentDetailDto>.Failure("Bạn không có quyền truy cập sự cố.", StatusCodes.Status403Forbidden);
        var item = await ProjectDetail(query.Where(i => i.IncidentId == incidentId), userId, role)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? ServiceResult<IncidentDetailDto>.Failure("Không tìm thấy sự cố hoặc bạn không có quyền truy cập.", StatusCodes.Status404NotFound)
            : ServiceResult<IncidentDetailDto>.Success(item);
    }

    public async Task<ServiceResult<IncidentDetailDto>> CreateAsync(int userId, string role, CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateContent(request.Severity, request.OccurredAt);
        if (validation is not null) return ServiceResult<IncidentDetailDto>.Failure(validation, StatusCodes.Status400BadRequest);
        var employee = await ActiveEmployeeAsync(userId, cancellationToken);
        if (employee is null)
            return ServiceResult<IncidentDetailDto>.Failure("Tài khoản chưa được liên kết với hồ sơ nhân viên đang hoạt động.", StatusCodes.Status403Forbidden);
        var bookings = ScopeBookings(role, userId);
        if (bookings is null || !await bookings.AnyAsync(b => b.BookingId == request.BookingId, cancellationToken))
            return ServiceResult<IncidentDetailDto>.Failure("Bạn không được phân công phụ trách tiệc đã chọn.", StatusCodes.Status403Forbidden);

        var pendingStatusId = await IncidentStatusIdAsync(BusinessStatusCodes.Pending, cancellationToken);
        if (pendingStatusId is null)
            return ServiceResult<IncidentDetailDto>.Failure("Hệ thống chưa cấu hình trạng thái PENDING cho sự cố.", StatusCodes.Status500InternalServerError);

        var incident = new Incident
        {
            BookingId = request.BookingId,
            ReportedByEmployeeId = employee.EmployeeId,
            IncidentType = request.IncidentType.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity.Trim(),
            StatusId = pendingStatusId.Value,
            OccurredAt = request.OccurredAt
        };
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditActions.IncidentCreated,
            EntityName = AuditEntityNames.Incident,
            EntityId = incident.IncidentId,
            NewData = JsonSerializer.Serialize(new { incident.BookingId, incident.IncidentType, incident.Description, incident.Severity, Status = BusinessStatusCodes.Pending, incident.OccurredAt }),
            Timestamp = DateTime.Now,
            Notes = "Tạo báo cáo sự cố."
        });
        await AddCreatedNotificationsAsync(incident, userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(userId, role, incident.IncidentId, cancellationToken);
    }

    public async Task<ServiceResult<IncidentDetailDto>> UpdateAsync(int userId, string role, int incidentId,
        UpdateIncidentRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateContent(request.Severity, request.OccurredAt);
        if (validation is not null) return ServiceResult<IncidentDetailDto>.Failure(validation, StatusCodes.Status400BadRequest);
        var query = Scope(role, userId, tracking: false);
        if (query is null)
            return ServiceResult<IncidentDetailDto>.Failure("Bạn không có quyền truy cập sự cố.", StatusCodes.Status403Forbidden);
        var snapshot = await query.Where(i => i.IncidentId == incidentId).Select(i => new
        {
            i.IncidentId, i.IncidentType, i.Description, i.Severity, i.OccurredAt,
            StatusCode = i.Status.StatusCode,
            ReporterUserId = i.ReportedByEmployee != null ? (int?)i.ReportedByEmployee.UserId : null
        }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return ServiceResult<IncidentDetailDto>.Failure("Không tìm thấy sự cố hoặc bạn không có quyền truy cập.", StatusCodes.Status404NotFound);
        if (role == RoleNames.Coordinator && snapshot.ReporterUserId != userId)
            return ServiceResult<IncidentDetailDto>.Failure("Bạn chỉ được chỉnh sửa báo cáo do chính mình tạo.", StatusCodes.Status403Forbidden);
        if (snapshot.StatusCode != BusinessStatusCodes.Pending)
            return ServiceResult<IncidentDetailDto>.Failure("Chỉ có thể chỉnh sửa sự cố đang chờ xử lý.", StatusCodes.Status409Conflict);

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        var affected = await UpdatePendingContentAtomicallyAsync(role, userId, incidentId, request, cancellationToken);
        if (affected == 0)
            return ServiceResult<IncidentDetailDto>.Failure("Sự cố vừa được người khác tiếp nhận. Nội dung báo cáo không còn được phép sửa.", StatusCodes.Status409Conflict);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = AuditActions.IncidentUpdated, EntityName = AuditEntityNames.Incident,
            EntityId = incidentId,
            OldData = JsonSerializer.Serialize(new { snapshot.IncidentType, snapshot.Description, snapshot.Severity, snapshot.OccurredAt }),
            NewData = JsonSerializer.Serialize(new { IncidentType = request.IncidentType.Trim(), Description = request.Description.Trim(), Severity = request.Severity.Trim(), request.OccurredAt }),
            Timestamp = DateTime.Now, Notes = "Cập nhật nội dung báo cáo sự cố."
        });
        if (role == RoleNames.Manager)
            await NotifyManagerInterventionAsync(incidentId, userId, snapshot.ReporterUserId,
                "Quản lý cập nhật báo cáo sự cố",
                $"Quản lý đã cập nhật nội dung sự cố #{incidentId}.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(userId, role, incidentId, cancellationToken);
    }

    public async Task<ServiceResult<IncidentDetailDto>> UpdateStatusAsync(int userId, string role, int incidentId,
        UpdateIncidentStatusRequest request, CancellationToken cancellationToken)
    {
        if (role is not RoleNames.Manager and not RoleNames.HallManager)
            return ServiceResult<IncidentDetailDto>.Failure("Nhân viên điều phối không được thay đổi trạng thái xử lý.", StatusCodes.Status403Forbidden);
        var nextCode = request.Status.Trim().ToUpperInvariant();
        if (nextCode is not BusinessStatusCodes.Processing and not BusinessStatusCodes.Resolved)
            return ServiceResult<IncidentDetailDto>.Failure("Trạng thái xử lý không hợp lệ.", StatusCodes.Status400BadRequest);
        var query = Scope(role, userId, tracking: false)!;
        var snapshot = await query.Where(i => i.IncidentId == incidentId).Select(i => new
        {
            i.IncidentId, i.StatusId, StatusCode = i.Status.StatusCode, i.Resolution, i.Severity,
            ReporterUserId = i.ReportedByEmployee != null ? (int?)i.ReportedByEmployee.UserId : null
        }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return ServiceResult<IncidentDetailDto>.Failure("Không tìm thấy sự cố hoặc bạn không có quyền xử lý.", StatusCodes.Status404NotFound);
        var expectedNextStatus = snapshot.StatusCode == BusinessStatusCodes.Pending
            ? BusinessStatusCodes.Processing
            : snapshot.StatusCode == BusinessStatusCodes.Processing ? BusinessStatusCodes.Resolved : null;
        if (nextCode != expectedNextStatus)
            return ServiceResult<IncidentDetailDto>.Failure("Trạng thái phải chuyển tuần tự: Chờ xử lý → Đang xử lý → Đã xử lý.", StatusCodes.Status409Conflict);
        if (nextCode == BusinessStatusCodes.Resolved && string.IsNullOrWhiteSpace(request.Resolution))
            return ServiceResult<IncidentDetailDto>.Failure("Vui lòng nhập hướng giải quyết trước khi hoàn tất.", StatusCodes.Status400BadRequest);
        var statusId = await IncidentStatusIdAsync(nextCode, cancellationToken);
        if (statusId is null)
            return ServiceResult<IncidentDetailDto>.Failure($"Hệ thống chưa cấu hình trạng thái {nextCode} cho sự cố.", StatusCodes.Status500InternalServerError);

        var resolvedAt = nextCode == BusinessStatusCodes.Resolved ? DateTime.Now : (DateTime?)null;
        var resolution = string.IsNullOrWhiteSpace(request.Resolution) ? snapshot.Resolution : request.Resolution.Trim();
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        var affected = await UpdateStatusAtomicallyAsync(role, userId, incidentId, snapshot.StatusId, statusId.Value,
            nextCode, resolution, resolvedAt, cancellationToken);
        if (affected == 0)
            return ServiceResult<IncidentDetailDto>.Failure("Sự cố vừa được người khác cập nhật. Vui lòng tải lại trạng thái mới nhất.", StatusCodes.Status409Conflict);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = AuditActions.IncidentStatusChanged, EntityName = AuditEntityNames.Incident,
            EntityId = incidentId, OldData = JsonSerializer.Serialize(new { Status = snapshot.StatusCode }),
            NewData = JsonSerializer.Serialize(new { Status = nextCode, Resolution = resolution, ResolvedAt = resolvedAt }),
            Timestamp = DateTime.Now, Notes = "Cập nhật tiến trình xử lý sự cố."
        });
        if (role == RoleNames.Manager)
            await NotifyManagerInterventionAsync(incidentId, userId, snapshot.ReporterUserId,
                nextCode == BusinessStatusCodes.Resolved ? "Quản lý đã hoàn tất sự cố" : "Quản lý tiếp nhận sự cố",
                $"Quản lý đã chuyển sự cố #{incidentId} sang trạng thái {nextCode}.", cancellationToken);
        else if (snapshot.ReporterUserId.HasValue)
            await _notifications.AddAsync(snapshot.ReporterUserId.Value, userId, NotificationTypeCodes.IncidentUpdated,
                nextCode == BusinessStatusCodes.Resolved ? "Sự cố đã được xử lý" : "Sự cố đã được tiếp nhận",
                $"Sự cố #{incidentId} đã chuyển sang trạng thái {nextCode}.",
                AuditEntityNames.Incident, incidentId, cancellationToken);
        if (nextCode == BusinessStatusCodes.Resolved)
            await NotifyManagersResolvedAsync(incidentId, userId, snapshot.ReporterUserId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(userId, role, incidentId, cancellationToken);
    }

    public async Task<ServiceResult<IncidentDetailDto>> UpdateResolutionAsync(int userId, string role, int incidentId,
        UpdateIncidentResolutionRequest request, CancellationToken cancellationToken)
    {
        if (role is not RoleNames.Manager and not RoleNames.HallManager)
            return ServiceResult<IncidentDetailDto>.Failure("Nhân viên điều phối không được cập nhật hướng xử lý.", StatusCodes.Status403Forbidden);
        var resolution = request.Resolution.Trim();
        if (resolution.Length < 3)
            return ServiceResult<IncidentDetailDto>.Failure("Vui lòng nhập hướng giải quyết hợp lệ.", StatusCodes.Status400BadRequest);
        var query = Scope(role, userId, tracking: false)!;
        var snapshot = await query.Where(i => i.IncidentId == incidentId)
            .Select(i => new
            {
                i.Resolution, StatusCode = i.Status.StatusCode,
                ReporterUserId = i.ReportedByEmployee != null ? (int?)i.ReportedByEmployee.UserId : null
            }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return ServiceResult<IncidentDetailDto>.Failure("Không tìm thấy sự cố hoặc bạn không có quyền xử lý.", StatusCodes.Status404NotFound);
        if (snapshot.StatusCode != BusinessStatusCodes.Processing)
            return ServiceResult<IncidentDetailDto>.Failure("Chỉ được cập nhật hướng xử lý khi sự cố đang được xử lý.", StatusCodes.Status409Conflict);

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        var affected = await UpdateResolutionAtomicallyAsync(role, userId, incidentId, resolution, cancellationToken);
        if (affected == 0)
            return ServiceResult<IncidentDetailDto>.Failure("Trạng thái sự cố vừa thay đổi. Vui lòng tải lại dữ liệu.", StatusCodes.Status409Conflict);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = AuditActions.IncidentResolutionUpdated, EntityName = AuditEntityNames.Incident,
            EntityId = incidentId, OldData = JsonSerializer.Serialize(new { snapshot.Resolution }),
            NewData = JsonSerializer.Serialize(new { Resolution = resolution }), Timestamp = DateTime.Now,
            Notes = "Cập nhật hướng giải quyết sự cố."
        });
        if (role == RoleNames.Manager)
            await NotifyManagerInterventionAsync(incidentId, userId, snapshot.ReporterUserId,
                "Quản lý cập nhật hướng xử lý",
                $"Quản lý đã cập nhật hướng xử lý cho sự cố #{incidentId}.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(userId, role, incidentId, cancellationToken);
    }

    public async Task<ServiceResult<List<IncidentHistoryItemDto>>> GetHistoryAsync(int userId, string role,
        int incidentId, CancellationToken cancellationToken)
    {
        var scoped = Scope(role, userId, tracking: false);
        if (scoped is null || !await scoped.AnyAsync(i => i.IncidentId == incidentId, cancellationToken))
            return ServiceResult<List<IncidentHistoryItemDto>>.Failure("Không tìm thấy sự cố hoặc bạn không có quyền xem lịch sử.", StatusCodes.Status404NotFound);
        var items = await _context.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == AuditEntityNames.Incident && log.EntityId == incidentId)
            .OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.AuditLogId)
            .Select(log => new IncidentHistoryItemDto
            {
                AuditLogId = log.AuditLogId, Action = log.Action,
                ActionName = log.Action == AuditActions.IncidentCreated ? "Tạo báo cáo" :
                    log.Action == AuditActions.IncidentUpdated ? "Cập nhật báo cáo" :
                    log.Action == AuditActions.IncidentResolutionUpdated ? "Cập nhật hướng xử lý" : "Chuyển trạng thái",
                ActorName = log.User != null && log.User.Employee != null ? log.User.Employee.FullName :
                    log.User != null ? log.User.Email : "Hệ thống",
                Timestamp = log.Timestamp, Notes = log.Notes, OldData = log.OldData, NewData = log.NewData
            }).ToListAsync(cancellationToken);
        return ServiceResult<List<IncidentHistoryItemDto>>.Success(items);
    }

    private async Task<int> UpdatePendingContentAtomicallyAsync(string role, int userId, int incidentId,
        UpdateIncidentRequest request, CancellationToken cancellationToken)
    {
        var scoped = Scope(role, userId, tracking: !_context.Database.IsRelational())!
            .Where(i => i.IncidentId == incidentId && i.Status.StatusCode == BusinessStatusCodes.Pending);
        if (_context.Database.IsRelational())
            return await scoped.ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.IncidentType, request.IncidentType.Trim())
                .SetProperty(i => i.Description, request.Description.Trim())
                .SetProperty(i => i.Severity, request.Severity.Trim())
                .SetProperty(i => i.OccurredAt, request.OccurredAt), cancellationToken);
        var incident = await scoped.SingleOrDefaultAsync(cancellationToken);
        if (incident is null) return 0;
        incident.IncidentType = request.IncidentType.Trim();
        incident.Description = request.Description.Trim();
        incident.Severity = request.Severity.Trim();
        incident.OccurredAt = request.OccurredAt;
        return 1;
    }

    private async Task<int> UpdateStatusAtomicallyAsync(string role, int userId, int incidentId, int expectedStatusId,
        int nextStatusId, string nextCode, string? resolution, DateTime? resolvedAt, CancellationToken cancellationToken)
    {
        if (_context.Database.IsRelational())
        {
            var scoped = Scope(role, userId, tracking: false)!
                .Where(i => i.IncidentId == incidentId && i.StatusId == expectedStatusId);
            if (nextCode == BusinessStatusCodes.Resolved)
                return await scoped.ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.StatusId, nextStatusId)
                    .SetProperty(i => i.Resolution, resolution)
                    .SetProperty(i => i.ResolvedAt, resolvedAt), cancellationToken);
            return await scoped.ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.StatusId, nextStatusId), cancellationToken);
        }
        var incident = await Scope(role, userId, tracking: true)!
            .SingleOrDefaultAsync(i => i.IncidentId == incidentId, cancellationToken);
        if (incident is null || incident.StatusId != expectedStatusId) return 0;
        incident.StatusId = nextStatusId;
        incident.Status = await _context.Statuses.SingleAsync(s => s.StatusId == nextStatusId, cancellationToken);
        if (nextCode == BusinessStatusCodes.Resolved)
        {
            incident.Resolution = resolution;
            incident.ResolvedAt = resolvedAt;
        }
        return 1;
    }

    private async Task<int> UpdateResolutionAtomicallyAsync(string role, int userId, int incidentId,
        string resolution, CancellationToken cancellationToken)
    {
        if (_context.Database.IsRelational())
        {
            var scoped = Scope(role, userId, tracking: false)!
                .Where(i => i.IncidentId == incidentId && i.Status.StatusCode == BusinessStatusCodes.Processing);
            return await scoped.ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Resolution, resolution), cancellationToken);
        }
        var incident = await Scope(role, userId, tracking: true)!
            .SingleOrDefaultAsync(i => i.IncidentId == incidentId, cancellationToken);
        if (incident is null) return 0;
        incident.Resolution = resolution;
        return 1;
    }

    private async Task NotifyManagersResolvedAsync(int incidentId, int actorUserId, int? reporterUserId,
        CancellationToken cancellationToken)
    {
        var managers = await _context.Users.AsNoTracking().Where(u => u.Role.RoleName == RoleNames.Manager &&
            u.Status.StatusCode == AccountStatusCodes.Active && u.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(u => u.UserId).ToListAsync(cancellationToken);
        foreach (var managerId in managers.Distinct().Where(id => id != reporterUserId))
            await _notifications.AddAsync(managerId, actorUserId, NotificationTypeCodes.IncidentUpdated,
                "Sự cố đã hoàn tất", $"Sự cố #{incidentId} đã được xử lý hoàn tất.",
                AuditEntityNames.Incident, incidentId, cancellationToken);
    }

    private IQueryable<Incident>? Scope(string role, int userId, bool tracking)
    {
        var query = tracking ? _context.Incidents.AsQueryable() : _context.Incidents.AsNoTracking();
        if (role == RoleNames.Manager) return query;
        if (role == RoleNames.Coordinator)
            return query.Where(i => i.Booking.CoordinationAssignments.Any(a => a.Coordinator.UserId == userId &&
                a.Status.StatusCode == CoordinationAssigned));
        if (role == RoleNames.HallManager)
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, DateTime.Today);
            return query.Where(i => hallIds.Contains(i.Booking.HallSchedule.HallId));
        }
        return null;
    }

    private IQueryable<WeddingBooking>? ScopeBookings(string role, int userId)
    {
        var query = _context.WeddingBookings.AsQueryable();
        if (role == RoleNames.Manager) return query;
        if (role == RoleNames.Coordinator)
            return query.Where(b => b.CoordinationAssignments.Any(a => a.Coordinator.UserId == userId &&
                a.Status.StatusCode == CoordinationAssigned));
        if (role == RoleNames.HallManager)
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, DateTime.Today);
            return query.Where(b => hallIds.Contains(b.HallSchedule.HallId));
        }
        return null;
    }

    private static IQueryable<IncidentDetailDto> ProjectDetail(IQueryable<Incident> query, int userId, string role) =>
        query.Select(i => new IncidentDetailDto
        {
            IncidentId = i.IncidentId, BookingId = i.BookingId, BookingCode = i.Booking.BookingCode,
            HallName = i.Booking.HallSchedule.Hall.HallName, EventDate = i.Booking.HallSchedule.Date,
            Shift = i.Booking.HallSchedule.Shift, IncidentType = i.IncidentType, Description = i.Description,
            Severity = i.Severity, StatusCode = i.Status.StatusCode, StatusName = i.Status.StatusName,
            ReporterName = i.ReportedByEmployee != null ? i.ReportedByEmployee.FullName : "Không xác định",
            ReporterCode = i.ReportedByEmployee != null ? i.ReportedByEmployee.EmployeeCode : string.Empty,
            Resolution = i.Resolution, OccurredAt = i.OccurredAt, ResolvedAt = i.ResolvedAt,
            HallManagerNames = i.Booking.HallSchedule.Hall.HallManagerAssignments
                .Where(a => a.FromDate <= DateTime.Today &&
                    (!a.ToDate.HasValue || a.ToDate.Value >= DateTime.Today))
                .OrderByDescending(a => a.FromDate).Select(a => a.HallManager.FullName).ToList(),
            CanEdit = i.Status.StatusCode == BusinessStatusCodes.Pending &&
                (role != RoleNames.Coordinator || i.ReportedByEmployee != null && i.ReportedByEmployee.UserId == userId)
        });

    private Task<Employee?> ActiveEmployeeAsync(int userId, CancellationToken cancellationToken) =>
        _context.Employees.Include(e => e.User).ThenInclude(u => u.Status).Include(e => e.DataStatus)
            .SingleOrDefaultAsync(e => e.UserId == userId && e.User.Status.StatusCode == AccountStatusCodes.Active &&
                e.DataStatus.DataStatusCode == DataStatusCodes.Existing, cancellationToken);

    private Task<int?> IncidentStatusIdAsync(string code, CancellationToken cancellationToken) =>
        _context.Statuses.AsNoTracking().Where(s => s.StatusGroup == StatusGroups.Incident && s.StatusCode == code)
            .Select(s => (int?)s.StatusId).SingleOrDefaultAsync(cancellationToken);

    private static string? ValidateContent(string severity, DateTime occurredAt)
    {
        if (!Severities.Contains(severity.Trim())) return "Mức độ sự cố không hợp lệ.";
        if (occurredAt == default) return "Vui lòng nhập thời gian phát sinh.";
        if (occurredAt > DateTime.Now.AddMinutes(5)) return "Thời gian phát sinh không được ở tương lai.";
        return null;
    }

    private async Task AddCreatedNotificationsAsync(Incident incident, int actorUserId, CancellationToken cancellationToken)
    {
        var booking = await _context.WeddingBookings.AsNoTracking().Where(b => b.BookingId == incident.BookingId)
            .Select(b => new { b.BookingCode, HallId = b.HallSchedule.HallId })
            .SingleAsync(cancellationToken);
        var recipients = await _context.HallManagerAssignments.AsNoTracking()
            .Where(a => a.HallId == booking.HallId && a.FromDate <= DateTime.Today &&
                (!a.ToDate.HasValue || a.ToDate.Value >= DateTime.Today) &&
                a.HallManager.User.Role.RoleName == RoleNames.HallManager &&
                a.HallManager.User.Status.StatusCode == AccountStatusCodes.Active &&
                a.HallManager.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                a.HallManager.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(a => a.HallManager.UserId).ToListAsync(cancellationToken);
        recipients.AddRange(await _context.Users.AsNoTracking().Where(u => u.Role.RoleName == RoleNames.Manager &&
            u.Status.StatusCode == AccountStatusCodes.Active && u.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(u => u.UserId).ToListAsync(cancellationToken));
        foreach (var recipient in recipients.Distinct().Where(id => id != actorUserId))
            await _notifications.AddAsync(recipient, actorUserId, NotificationTypeCodes.IncidentCreated,
                incident.Severity == IncidentSeverityCodes.Critical ? "Sự cố nghiêm trọng cần chú ý" : "Sự cố tiệc mới",
                $"Tiệc {booking.BookingCode} vừa ghi nhận sự cố {incident.IncidentType}.",
                AuditEntityNames.Incident, incident.IncidentId, cancellationToken);
    }

    private async Task NotifyManagerInterventionAsync(int incidentId, int actorUserId, int? reporterUserId,
        string title, string message, CancellationToken cancellationToken)
    {
        var incident = await _context.Incidents.AsNoTracking().Where(i => i.IncidentId == incidentId)
            .Select(i => new { HallId = i.Booking.HallSchedule.HallId })
            .SingleAsync(cancellationToken);
        var recipients = await _context.HallManagerAssignments.AsNoTracking()
            .Where(a => a.HallId == incident.HallId && a.FromDate <= DateTime.Today &&
                (!a.ToDate.HasValue || a.ToDate.Value >= DateTime.Today) &&
                a.HallManager.User.Role.RoleName == RoleNames.HallManager &&
                a.HallManager.User.Status.StatusCode == AccountStatusCodes.Active &&
                a.HallManager.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                a.HallManager.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(a => a.HallManager.UserId).ToListAsync(cancellationToken);
        if (reporterUserId.HasValue) recipients.Add(reporterUserId.Value);
        foreach (var recipient in recipients.Distinct().Where(id => id != actorUserId))
            await _notifications.AddAsync(recipient, actorUserId, NotificationTypeCodes.IncidentUpdated,
                title, message, AuditEntityNames.Incident, incidentId, cancellationToken);
    }
}

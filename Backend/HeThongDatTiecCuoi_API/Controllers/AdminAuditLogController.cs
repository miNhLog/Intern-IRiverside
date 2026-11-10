using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AuditLog;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAuditLogController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public AdminAuditLogController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> GetAuditLogs(int page = 1, int pageSize = 20,
        string? action = null, DateTime? fromDate = null, DateTime? toDate = null,
        string? keyword = null, string? group = null, string? outcome = null,
        string? authMethod = null, string? mode = null, string? importantGroup = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        action = string.IsNullOrWhiteSpace(action) ? null : action.Trim().ToUpperInvariant();
        keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim().ToLowerInvariant();
        outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim().ToUpperInvariant();
        authMethod = string.IsNullOrWhiteSpace(authMethod) ? null : authMethod.Trim().ToUpperInvariant();
        mode = string.IsNullOrWhiteSpace(mode) ? null : mode.Trim().ToLowerInvariant();
        importantGroup = string.IsNullOrWhiteSpace(importantGroup) ? null : importantGroup.Trim().ToLowerInvariant();
        fromDate = fromDate?.Date; toDate = toDate?.Date;
        var appliedDefaultPeriod = false;
        if (mode == AuditLogModes.Important && !fromDate.HasValue && !toDate.HasValue)
        {
            toDate = RiversideBusinessClock.Today;
            fromDate = toDate.Value.AddDays(-6);
            appliedDefaultPeriod = true;
        }
        if (fromDate.HasValue && toDate.HasValue && toDate < fromDate) return BadRequest(new { message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu." });
        if (keyword?.Length > 200) return BadRequest(new { message = "Từ khóa tìm kiếm không được vượt quá 200 ký tự." });
        if (action is not null && !AuditActions.AdminVisibleActions.Contains(action)) return BadRequest(new { message = "Loại thao tác không hợp lệ." });
        if (mode is not null && mode is not (AuditLogModes.Important or AuditLogModes.All)) return BadRequest(new { message = "Chế độ nhật ký không hợp lệ." });
        var groupActions = AuditActions.ForGroup(group);
        if (groupActions is null) return BadRequest(new { message = "Nhóm nhật ký không hợp lệ." });
        var importantGroupActions = AuditImportantGroups.ForGroup(importantGroup);
        if (importantGroupActions is null) return BadRequest(new { message = "Nhóm nhật ký quan trọng không hợp lệ." });
        if (mode != AuditLogModes.Important && importantGroup is not null) return BadRequest(new { message = "Nhóm ưu tiên chỉ áp dụng cho chế độ nhật ký quan trọng." });
        if (outcome is not null && outcome is not (AuditOutcomes.Success or AuditOutcomes.Failed or AuditOutcomes.Blocked)) return BadRequest(new { message = "Kết quả không hợp lệ." });
        if (authMethod is not null && authMethod is not (AuditAuthMethods.Password or AuditAuthMethods.Google or AuditAuthMethods.Jwt or AuditAuthMethods.ResetToken)) return BadRequest(new { message = "Phương thức xác thực không hợp lệ." });

        var query = _context.AuditLogs.AsNoTracking().Where(log => AuditActions.AdminVisibleActions.Contains(log.Action));
        if (mode == AuditLogModes.Important) query = query.Where(log => importantGroupActions.Contains(log.Action));
        if (group is not null) query = query.Where(log => groupActions.Contains(log.Action));
        if (action is not null) query = query.Where(log => log.Action == action);
        if (outcome is not null) query = query.Where(log => log.Outcome == outcome);
        if (authMethod is not null) query = query.Where(log => log.AuthMethod == authMethod);
        if (fromDate.HasValue) query = query.Where(log => log.Timestamp >= fromDate.Value);
        if (toDate.HasValue) { var endExclusive = toDate.Value.AddDays(1); query = query.Where(log => log.Timestamp < endExclusive); }
        if (keyword is not null)
        {
            query = query.Where(log => log.Action.Contains(keyword) || (log.Notes != null && log.Notes.Contains(keyword)) ||
                (log.User != null && (log.User.Email.Contains(keyword) || (log.User.Employee != null && log.User.Employee.FullName.Contains(keyword)))) ||
                (log.EntityName == AuditEntityNames.User && _context.Users.Any(user => user.UserId == log.EntityId &&
                    (user.Email.Contains(keyword) || (user.Employee != null && (user.Employee.FullName.Contains(keyword) || user.Employee.EmployeeCode.Contains(keyword)))))) ||
                (log.EntityName == AuditEntityNames.RoleChangeRequest && _context.RoleChangeRequests.Any(request => request.RoleChangeRequestId == log.EntityId &&
                    (request.Employee.FullName.Contains(keyword) || (request.EmployeeCodeBefore ?? request.Employee.EmployeeCode).Contains(keyword) ||
                     (request.EmployeeCodeAfter != null && request.EmployeeCodeAfter.Contains(keyword))))) ||
                (log.EntityName == AuditEntityNames.Booking && _context.WeddingBookings.Any(x => x.BookingId == log.EntityId && x.BookingCode.Contains(keyword))) ||
                (log.EntityName == AuditEntityNames.Contract && _context.Contracts.Any(x => x.ContractId == log.EntityId && x.ContractCode.Contains(keyword))) ||
                (log.EntityName == AuditEntityNames.Hall && _context.Halls.Any(x => x.HallId == log.EntityId && (x.HallCode.Contains(keyword) || x.HallName.Contains(keyword)))) ||
                (log.EntityName == AuditEntityNames.Menu && _context.Menus.Any(x => x.MenuId == log.EntityId && (x.MenuCode.Contains(keyword) || x.MenuName.Contains(keyword)))) ||
                (log.EntityName == AuditEntityNames.Dish && _context.Dishes.Any(x => x.DishId == log.EntityId && (x.DishCode.Contains(keyword) || x.DishName.Contains(keyword)))));
        }

        var statistics = await query.GroupBy(_ => 1).Select(logs => new AuditLogStatisticsDto
        {
            NeedsReview = logs.Count(log => log.Action == AuditActions.LoginBlocked ||
                (AuditImportantGroups.SensitiveActions.Contains(log.Action) &&
                 (log.Outcome == AuditOutcomes.Failed || log.Outcome == AuditOutcomes.Blocked))),
            RoleChanges = logs.Count(log => AuditImportantGroups.RoleChangeActions.Contains(log.Action)),
            FinancialEvents = logs.Count(log => AuditImportantGroups.FinanceActions.Contains(log.Action))
        }).FirstOrDefaultAsync(cancellationToken) ?? new AuditLogStatisticsDto();

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        var rawItems = await query.OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.AuditLogId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new
            {
                log.AuditLogId, log.Timestamp, log.UserId, log.Action, log.EntityName, log.EntityId, log.OldData, log.NewData, log.Notes,
                log.IpAddress, log.UserAgent, log.AuthMethod, log.Outcome, log.CorrelationId,
                ActorEmail = log.User == null ? null : log.User.Email,
                ActorName = log.User != null && log.User.Employee != null ? log.User.Employee.FullName : null,
                ActorRole = log.User == null ? null : log.User.Role.RoleName,
                AccountEmail = log.EntityName == AuditEntityNames.User ? _context.Users.Where(x => x.UserId == log.EntityId).Select(x => x.Email).FirstOrDefault() : null,
                AccountCode = log.EntityName == AuditEntityNames.User ? _context.Employees.Where(x => x.UserId == log.EntityId).Select(x => x.EmployeeCode).FirstOrDefault() : null,
                AccountName = log.EntityName == AuditEntityNames.User ? _context.Employees.Where(x => x.UserId == log.EntityId).Select(x => x.FullName).FirstOrDefault() : null,
                AccountRole = log.EntityName == AuditEntityNames.User ? _context.Users.Where(x => x.UserId == log.EntityId).Select(x => x.Role.RoleName).FirstOrDefault() : null,
                RequestName = log.EntityName == AuditEntityNames.RoleChangeRequest ? _context.RoleChangeRequests.Where(x => x.RoleChangeRequestId == log.EntityId).Select(x => x.Employee.FullName).FirstOrDefault() : null,
                RequestCode = log.EntityName == AuditEntityNames.RoleChangeRequest ? _context.RoleChangeRequests.Where(x => x.RoleChangeRequestId == log.EntityId).Select(x => x.EmployeeCodeAfter ?? x.EmployeeCodeBefore ?? x.Employee.EmployeeCode).FirstOrDefault() : null,
                BookingCode = log.EntityName == AuditEntityNames.Booking ? _context.WeddingBookings.Where(x => x.BookingId == log.EntityId).Select(x => x.BookingCode).FirstOrDefault() : null,
                ContractCode = log.EntityName == AuditEntityNames.Contract ? _context.Contracts.Where(x => x.ContractId == log.EntityId).Select(x => x.ContractCode).FirstOrDefault() : null,
                HallName = log.EntityName == AuditEntityNames.Hall ? _context.Halls.Where(x => x.HallId == log.EntityId).Select(x => x.HallCode + " - " + x.HallName).FirstOrDefault() : null,
                MenuName = log.EntityName == AuditEntityNames.Menu ? _context.Menus.Where(x => x.MenuId == log.EntityId).Select(x => x.MenuCode + " - " + x.MenuName).FirstOrDefault() : null,
                DishName = log.EntityName == AuditEntityNames.Dish ? _context.Dishes.Where(x => x.DishId == log.EntityId).Select(x => x.DishCode + " - " + x.DishName).FirstOrDefault() : null,
                DecorName = log.EntityName == AuditEntityNames.DecorPackage ? _context.DecorPackages.Where(x => x.GoiTrangTriID == log.EntityId).Select(x => x.MaGoi + " - " + x.TenGoi).FirstOrDefault() : null,
                ServiceName = log.EntityName == AuditEntityNames.Service ? _context.ServiceItems.Where(x => x.DichVuID == log.EntityId).Select(x => x.MaDichVu + " - " + x.TenDichVu).FirstOrDefault() : null
            }).ToListAsync(cancellationToken);

        var items = rawItems.Select(item =>
        {
            var actorName = item.UserId.HasValue ? item.ActorName ?? item.ActorEmail ?? "Tài khoản không còn tồn tại" :
                item.Action == AuditActions.LoginFailed || item.Action == AuditActions.LoginBlocked || item.Action == AuditActions.PasswordResetCompleted
                    ? "Người thực hiện chưa xác thực" : "Hệ thống";
            var target = item.EntityName == AuditEntityNames.RoleChangeRequest
                ? $"Yêu cầu #{item.EntityId}" + (item.RequestName is null ? "" : $" - {item.RequestCode} - {item.RequestName}")
                : item.EntityName == AuditEntityNames.User ? item.AccountName is not null ? $"{item.AccountCode} - {item.AccountName}" : item.AccountEmail ?? $"Tài khoản #{item.EntityId}"
                : item.BookingCode is not null ? $"Đặt tiệc {item.BookingCode}"
                : item.ContractCode is not null ? $"Hợp đồng {item.ContractCode}"
                : item.HallName ?? item.MenuName ?? item.DishName ?? item.DecorName ?? item.ServiceName
                ?? $"{item.EntityName} #{item.EntityId}";
            var source = item.UserId.HasValue ? "Internal" : item.Action == AuditActions.Payment ? "PayOS" :
                item.Action is AuditActions.BookingHoldCancelled or "CANCEL" ? "Public/Guest" : "System";
            return new AuditLogDto
            {
                AuditLogId = item.AuditLogId, Timestamp = item.Timestamp, ActorUserId = item.UserId,
                ActorName = actorName, ActorEmail = item.ActorEmail, ActorRole = item.ActorRole,
                Actor = item.ActorName is not null && item.ActorEmail is not null ? $"{item.ActorName} ({item.ActorEmail})" : actorName,
                Action = item.Action, EntityName = item.EntityName, EntityId = item.EntityId,
                TargetUserId = item.EntityName == AuditEntityNames.User ? item.EntityId : null, Target = target, TargetRole = item.AccountRole, Notes = item.Notes,
                IpAddress = item.IpAddress, UserAgent = item.UserAgent, AuthMethod = item.AuthMethod,
                Outcome = item.Outcome, CorrelationId = item.CorrelationId,
                OldData = AuditDataSanitizer.Sanitize(item.OldData), NewData = AuditDataSanitizer.Sanitize(item.NewData),
                Group = AuditActions.GroupOf(item.Action), Source = source
            };
        }).ToList();
        var availableActions = (mode == AuditLogModes.Important ? importantGroupActions : groupActions)
            .Where(AuditActions.AdminVisibleActions.Contains)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        return Ok(new AuditLogPageDto
        {
            Items = items,
            Statistics = statistics,
            AvailableActions = availableActions,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Mode = mode,
            EffectiveFromDate = fromDate,
            EffectiveToDate = toDate,
            AppliedDefaultPeriod = appliedDefaultPeriod
        });
    }
}

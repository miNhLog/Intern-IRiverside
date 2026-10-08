using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AuditLog;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
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
        string? keyword = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        action = string.IsNullOrWhiteSpace(action) ? null : action.Trim().ToUpperInvariant();
        keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        fromDate = fromDate?.Date; toDate = toDate?.Date;
        if (fromDate.HasValue && toDate.HasValue && toDate < fromDate) return BadRequest(new { message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu." });
        if (keyword?.Length > 200) return BadRequest(new { message = "Từ khóa tìm kiếm không được vượt quá 200 ký tự." });
        if (action is not null && !AuditActions.AdminVisibleActions.Contains(action)) return BadRequest(new { message = "Loại thao tác không hợp lệ." });

        var query = _context.AuditLogs.AsNoTracking().Where(log => AuditActions.AdminVisibleActions.Contains(log.Action));
        if (action is not null) query = query.Where(log => log.Action == action);
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
                     (request.EmployeeCodeAfter != null && request.EmployeeCodeAfter.Contains(keyword))))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;
        var rawItems = await query.OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.AuditLogId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new
            {
                log.AuditLogId, log.Timestamp, log.UserId, log.Action, log.EntityName, log.EntityId, log.OldData, log.NewData, log.Notes,
                ActorEmail = log.User == null ? null : log.User.Email,
                ActorName = log.User != null && log.User.Employee != null ? log.User.Employee.FullName : null,
                AccountEmail = log.EntityName == AuditEntityNames.User ? _context.Users.Where(x => x.UserId == log.EntityId).Select(x => x.Email).FirstOrDefault() : null,
                AccountCode = log.EntityName == AuditEntityNames.User ? _context.Employees.Where(x => x.UserId == log.EntityId).Select(x => x.EmployeeCode).FirstOrDefault() : null,
                AccountName = log.EntityName == AuditEntityNames.User ? _context.Employees.Where(x => x.UserId == log.EntityId).Select(x => x.FullName).FirstOrDefault() : null,
                RequestName = log.EntityName == AuditEntityNames.RoleChangeRequest ? _context.RoleChangeRequests.Where(x => x.RoleChangeRequestId == log.EntityId).Select(x => x.Employee.FullName).FirstOrDefault() : null,
                RequestCode = log.EntityName == AuditEntityNames.RoleChangeRequest ? _context.RoleChangeRequests.Where(x => x.RoleChangeRequestId == log.EntityId).Select(x => x.EmployeeCodeAfter ?? x.EmployeeCodeBefore ?? x.Employee.EmployeeCode).FirstOrDefault() : null
            }).ToListAsync(cancellationToken);

        var items = rawItems.Select(item =>
        {
            var actorName = item.ActorName ?? item.ActorEmail ?? "Tài khoản không còn tồn tại";
            var target = item.EntityName == AuditEntityNames.RoleChangeRequest
                ? $"Yêu cầu #{item.EntityId}" + (item.RequestName is null ? "" : $" - {item.RequestCode} - {item.RequestName}")
                : item.AccountName is not null ? $"{item.AccountCode} - {item.AccountName}" : item.AccountEmail ?? $"Tài khoản #{item.EntityId}";
            return new AuditLogDto
            {
                AuditLogId = item.AuditLogId, Timestamp = item.Timestamp, ActorUserId = item.UserId,
                ActorName = actorName, ActorEmail = item.ActorEmail,
                Actor = item.ActorName is not null && item.ActorEmail is not null ? $"{item.ActorName} ({item.ActorEmail})" : actorName,
                Action = item.Action, EntityName = item.EntityName, EntityId = item.EntityId,
                TargetUserId = item.EntityId, Target = target, Notes = item.Notes,
                OldData = AuditDataSanitizer.Sanitize(item.OldData), NewData = AuditDataSanitizer.Sanitize(item.NewData)
            };
        }).ToList();
        return Ok(new AuditLogPageDto { Items = items, Page = page, PageSize = pageSize, TotalItems = totalItems, TotalPages = totalPages });
    }
}

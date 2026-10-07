using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.RoleChangeRequest;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Services;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/role-change-requests")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerRoleChangeRequestController : ControllerBase
{
    private const string DuplicatePendingMessage = "Nhân viên này đã có một yêu cầu thay đổi vai trò đang chờ duyệt.";
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<ManagerRoleChangeRequestController> _logger;

    public ManagerRoleChangeRequestController(
        ApplicationDbContext context,
        INotificationService notifications,
        ILogger<ManagerRoleChangeRequestController> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<List<RoleChangeCandidateDto>>> GetCandidates(CancellationToken cancellationToken)
    {
        var candidates = await _context.Employees.AsNoTracking()
            .Where(employee =>
                employee.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                employee.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                (employee.User.Role.RoleName == RoleNames.HallManager ||
                 employee.User.Role.RoleName == RoleNames.Coordinator) &&
                !_context.RoleChangeRequests.Any(request =>
                    request.EmployeeId == employee.EmployeeId &&
                    request.Status == RoleChangeRequestStatusCodes.Pending))
            .OrderBy(employee => employee.EmployeeCode)
            .Select(employee => new RoleChangeCandidateDto
            {
                EmployeeId = employee.EmployeeId,
                UserId = employee.UserId,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Email = employee.User.Email,
                PhoneNumber = employee.PhoneNumber,
                CurrentRoleId = employee.User.RoleId,
                CurrentRoleName = employee.User.Role.RoleName,
                RequestedRoleId = _context.Roles
                    .Where(role => role.RoleName == (employee.User.Role.RoleName == RoleNames.Coordinator
                        ? RoleNames.HallManager : RoleNames.Coordinator))
                    .Select(role => role.RoleId).Single(),
                RequestedRoleName = employee.User.Role.RoleName == RoleNames.Coordinator
                    ? RoleNames.HallManager : RoleNames.Coordinator,
                AccountStatus = employee.User.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(candidates);
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleChangeRequestDto>>> GetRequests(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Unauthorized(new { message = "Không xác định được tài khoản Quản lý." });

        var requests = await _context.RoleChangeRequests.AsNoTracking()
            .Where(request => request.RequestedByUserId == currentUserId)
            .OrderByDescending(request => request.RequestedAt)
            .ThenByDescending(request => request.RoleChangeRequestId)
            .Select(request => new RoleChangeRequestDto
            {
                RoleChangeRequestId = request.RoleChangeRequestId,
                EmployeeId = request.EmployeeId,
                EmployeeCode = request.Employee.EmployeeCode,
                EmployeeCodeBefore = request.EmployeeCodeBefore,
                EmployeeCodeAfter = request.EmployeeCodeAfter,
                FullName = request.Employee.FullName,
                CurrentRoleId = request.CurrentRoleId,
                CurrentRoleName = request.CurrentRole.RoleName,
                RequestedRoleId = request.RequestedRoleId,
                RequestedRoleName = request.RequestedRole.RoleName,
                Reason = request.Reason,
                Status = request.Status,
                RequestedAt = request.RequestedAt,
                ReviewedAt = request.ReviewedAt,
                RejectionReason = request.RejectionReason,
                ReviewedBy = request.ReviewedByUser == null ? null : request.ReviewedByUser.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpPost]
    public async Task<ActionResult<RoleChangeRequestDto>> Create(
        [FromBody] CreateRoleChangeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Unauthorized(new { message = "Không xác định được tài khoản Quản lý." });

        var managerIsValid = await _context.Users.AsNoTracking().AnyAsync(user =>
            user.UserId == currentUserId &&
            user.Role.RoleName == RoleNames.Manager &&
            user.DataStatus.DataStatusCode == DataStatusCodes.Existing,
            cancellationToken);
        if (!managerIsValid)
            return Forbid();

        var employee = await _context.Employees
            .Include(item => item.DataStatus)
            .Include(item => item.User).ThenInclude(user => user.DataStatus)
            .Include(item => item.User).ThenInclude(user => user.Role)
            .SingleOrDefaultAsync(item => item.EmployeeId == request.EmployeeId, cancellationToken);

        if (employee is null || employee.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
            employee.User.DataStatus.DataStatusCode != DataStatusCodes.Existing)
            return BadRequest(new { message = "Nhân viên không tồn tại hoặc không còn hoạt động trong hệ thống." });

        var targetRoleName = employee.User.Role.RoleName switch
        {
            RoleNames.Coordinator => RoleNames.HallManager,
            RoleNames.HallManager => RoleNames.Coordinator,
            _ => null
        };
        if (targetRoleName is null)
            return BadRequest(new { message = "Chỉ có thể đề xuất thay đổi giữa Nhân viên điều phối và Quản lý sảnh." });

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 500)
            return BadRequest(new { message = "Lý do thay đổi không được vượt quá 500 ký tự." });

        if (await _context.RoleChangeRequests.AnyAsync(item =>
            item.EmployeeId == employee.EmployeeId && item.Status == RoleChangeRequestStatusCodes.Pending,
            cancellationToken))
            return Conflict(new { message = DuplicatePendingMessage });

        var targetRole = await _context.Roles.SingleOrDefaultAsync(role => role.RoleName == targetRoleName, cancellationToken);
        if (targetRole is null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không tìm thấy vai trò đích trong hệ thống." });

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = new RoleChangeRequest
            {
                EmployeeId = employee.EmployeeId,
                CurrentRoleId = employee.User.RoleId,
                RequestedRoleId = targetRole.RoleId,
                RequestedByUserId = currentUserId,
                Reason = reason,
                EmployeeCodeBefore = employee.EmployeeCode,
                Status = RoleChangeRequestStatusCodes.Pending,
                RequestedAt = DateTime.Now
            };
            _context.RoleChangeRequests.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = AuditActions.RoleChangeRequested,
                EntityName = AuditEntityNames.RoleChangeRequest,
                EntityId = entity.RoleChangeRequestId,
                NewData = JsonSerializer.Serialize(new
                {
                    employee.EmployeeId,
                    EmployeeCodeBefore = employee.EmployeeCode,
                    employee.FullName,
                    CurrentRoleName = employee.User.Role.RoleName,
                    RequestedRoleName = targetRole.RoleName,
                    Reason = reason,
                    entity.Status
                }),
                Timestamp = DateTime.Now,
                Notes = $"Quản lý gửi yêu cầu thay đổi vai trò cho {employee.EmployeeCode} - {employee.FullName} từ {employee.User.Role.RoleName} sang {targetRole.RoleName}." + (reason is null ? string.Empty : $" Lý do: {reason}")
            });
            var adminUserId = await _notifications.FindActiveAdminUserIdAsync(cancellationToken);
            if (adminUserId.HasValue)
            {
                var managerName = await _context.Users.Where(user => user.UserId == currentUserId)
                    .Select(user => user.Employee != null ? user.Employee.FullName : user.Email).SingleAsync(cancellationToken);
                await _notifications.AddAsync(adminUserId.Value, currentUserId,
                    NotificationTypeCodes.RoleChangeRequestCreated, "Yêu cầu thay đổi vai trò mới",
                    $"Quản lý {managerName} đã gửi yêu cầu thay đổi vai trò cho {employee.EmployeeCode} - {employee.FullName} từ {employee.User.Role.RoleName} sang {targetRole.RoleName}.",
                    AuditEntityNames.RoleChangeRequest, entity.RoleChangeRequestId, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Không tìm thấy tài khoản Admin đang hoạt động để nhận thông báo cho yêu cầu đổi vai trò {RequestId}.",
                    entity.RoleChangeRequestId);
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new RoleChangeRequestDto
            {
                RoleChangeRequestId = entity.RoleChangeRequestId,
                EmployeeId = employee.EmployeeId,
                EmployeeCode = employee.EmployeeCode,
                EmployeeCodeBefore = entity.EmployeeCodeBefore,
                EmployeeCodeAfter = entity.EmployeeCodeAfter,
                FullName = employee.FullName,
                CurrentRoleId = entity.CurrentRoleId,
                CurrentRoleName = employee.User.Role.RoleName,
                RequestedRoleId = entity.RequestedRoleId,
                RequestedRoleName = targetRole.RoleName,
                Reason = entity.Reason,
                Status = entity.Status,
                RequestedAt = entity.RequestedAt
            });
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = DuplicatePendingMessage });
        }
    }

    private bool TryGetCurrentUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

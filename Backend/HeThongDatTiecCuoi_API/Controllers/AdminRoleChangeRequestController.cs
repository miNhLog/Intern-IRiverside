using System.Data;
using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.RoleChangeRequest;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/role-change-requests")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminRoleChangeRequestController : ControllerBase
{
    private const string AlreadyProcessedMessage = "Yêu cầu này đã được xử lý trước đó.";
    private readonly ApplicationDbContext _context;
    private readonly IEmployeeCodeGenerator _employeeCodeGenerator;
    private readonly INotificationService _notifications;

    public AdminRoleChangeRequestController(ApplicationDbContext context, IEmployeeCodeGenerator employeeCodeGenerator, INotificationService notifications)
    {
        _context = context;
        _employeeCodeGenerator = employeeCodeGenerator;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminRoleChangeRequestDto>>> GetAll(
        string? status, string? keyword, CancellationToken cancellationToken)
    {
        var query = _context.RoleChangeRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            if (normalized is not (RoleChangeRequestStatusCodes.Pending or RoleChangeRequestStatusCodes.Approved or RoleChangeRequestStatusCodes.Rejected))
                return BadRequest(new { message = "Trạng thái yêu cầu không hợp lệ." });
            query = query.Where(item => item.Status == normalized);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(item => item.Employee.FullName.Contains(term) ||
                (item.EmployeeCodeBefore ?? item.Employee.EmployeeCode).Contains(term));
        }

        var items = await query
            .OrderBy(item => item.Status == RoleChangeRequestStatusCodes.Pending ? 0 : 1)
            .ThenByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.RoleChangeRequestId)
            .Select(item => new AdminRoleChangeRequestDto
            {
                RoleChangeRequestId = item.RoleChangeRequestId,
                EmployeeId = item.EmployeeId,
                FullName = item.Employee.FullName,
                EmployeeCodeBefore = item.EmployeeCodeBefore ?? item.Employee.EmployeeCode,
                EmployeeCodeAfter = item.EmployeeCodeAfter,
                CurrentRoleId = item.CurrentRoleId,
                CurrentRoleName = item.CurrentRole.RoleName,
                RequestedRoleId = item.RequestedRoleId,
                RequestedRoleName = item.RequestedRole.RoleName,
                Reason = item.Reason,
                Status = item.Status,
                RequestedAt = item.RequestedAt,
                RequestedByUserId = item.RequestedByUserId,
                RequestedByName = item.RequestedByUser.Employee != null
                    ? item.RequestedByUser.Employee.FullName : item.RequestedByUser.Email,
                ReviewedByUserId = item.ReviewedByUserId,
                ReviewedByName = item.ReviewedByUser == null ? null : item.ReviewedByUser.Email,
                ReviewedAt = item.ReviewedAt,
                RejectionReason = item.RejectionReason,
                CanApprove = item.Status == RoleChangeRequestStatusCodes.Pending
            }).ToListAsync(cancellationToken);

        foreach (var item in items.Where(item => item.Status == RoleChangeRequestStatusCodes.Pending))
        {
            item.BlockingReason = await GetAssignmentBlockingReasonAsync(item.EmployeeId, item.CurrentRoleName, cancellationToken);
            item.CanApprove = item.BlockingReason is null;
        }
        return Ok(items);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized(new { message = "Không xác định được tài khoản Admin." });
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var request = await _context.RoleChangeRequests
                .FromSqlInterpolated($"SELECT * FROM YeuCauThayDoiVaiTro WITH (UPDLOCK, ROWLOCK) WHERE YeuCauThayDoiVaiTroID = {id}")
                .Include(item => item.Employee).ThenInclude(employee => employee.DataStatus)
                .Include(item => item.Employee).ThenInclude(employee => employee.User).ThenInclude(user => user.DataStatus)
                .Include(item => item.CurrentRole).Include(item => item.RequestedRole)
                .SingleOrDefaultAsync(cancellationToken);
            if (request is null) return NotFound(new { message = "Không tìm thấy yêu cầu." });
            if (request.Status != RoleChangeRequestStatusCodes.Pending) return Conflict(new { message = AlreadyProcessedMessage });

            var employee = request.Employee;
            if (employee.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
                employee.User.DataStatus.DataStatusCode != DataStatusCodes.Existing)
                return BadRequest(new { message = "Nhân viên hoặc tài khoản không còn tồn tại trong hệ thống." });
            if (employee.User.RoleId != request.CurrentRoleId)
                return Conflict(new { message = "Vai trò hiện tại của nhân viên đã thay đổi so với thời điểm gửi yêu cầu. Vui lòng kiểm tra lại." });
            if (!IsValidRolePair(request.CurrentRole.RoleName, request.RequestedRole.RoleName))
                return BadRequest(new { message = "Cặp vai trò trong yêu cầu không hợp lệ." });

            var blockingReason = await GetAssignmentBlockingReasonAsync(employee.EmployeeId, request.CurrentRole.RoleName, cancellationToken);
            if (blockingReason is not null) return Conflict(new { message = blockingReason });

            var oldCode = request.EmployeeCodeBefore ?? employee.EmployeeCode;
            var newCode = await _employeeCodeGenerator.GenerateAsync(request.RequestedRole.RoleName, cancellationToken);
            employee.User.RoleId = request.RequestedRoleId;
            employee.User.UpdatedAt = DateTime.Now;
            employee.EmployeeCode = newCode;
            request.Status = RoleChangeRequestStatusCodes.Approved;
            request.ReviewedByUserId = actorId;
            request.ReviewedAt = DateTime.Now;
            request.RejectionReason = null;
            request.EmployeeCodeBefore = oldCode;
            request.EmployeeCodeAfter = newCode;
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = AuditActions.RoleChangeApproved,
                EntityName = AuditEntityNames.RoleChangeRequest, EntityId = request.RoleChangeRequestId,
                OldData = JsonSerializer.Serialize(new { employee.EmployeeId, EmployeeCodeBefore = oldCode, request.CurrentRoleId, CurrentRoleName = request.CurrentRole.RoleName }),
                NewData = JsonSerializer.Serialize(new { employee.EmployeeId, EmployeeCodeAfter = newCode, request.RequestedRoleId, RequestedRoleName = request.RequestedRole.RoleName, Status = RoleChangeRequestStatusCodes.Approved }),
                Timestamp = DateTime.Now,
                Notes = $"Admin duyệt yêu cầu thay đổi vai trò của {employee.FullName} từ {request.CurrentRole.RoleName} sang {request.RequestedRole.RoleName}. Mã nhân viên thay đổi từ {oldCode} sang {newCode}."
            });
            await _notifications.AddAsync(request.RequestedByUserId, actorId,
                NotificationTypeCodes.RoleChangeApproved, "Yêu cầu thay đổi vai trò đã được duyệt",
                $"Yêu cầu thay đổi vai trò của {employee.FullName} đã được duyệt. Mã nhân viên thay đổi từ {oldCode} thành {newCode}.",
                AuditEntityNames.RoleChangeRequest, request.RoleChangeRequestId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "Đã duyệt yêu cầu và cập nhật vai trò. Nhân viên cần đăng xuất và đăng nhập lại để nhận quyền mới." });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Không thể cấp mã nhân viên mới do mã vừa phát sinh đã tồn tại. Vui lòng thử lại." });
        }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRoleChangeRequestDto input, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized(new { message = "Không xác định được tài khoản Admin." });
        var reason = input.RejectionReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return BadRequest(new { message = "Lý do từ chối là bắt buộc và không được vượt quá 500 ký tự." });

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await _context.RoleChangeRequests
            .FromSqlInterpolated($"SELECT * FROM YeuCauThayDoiVaiTro WITH (UPDLOCK, ROWLOCK) WHERE YeuCauThayDoiVaiTroID = {id}")
            .Include(item => item.Employee).Include(item => item.CurrentRole).Include(item => item.RequestedRole)
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return NotFound(new { message = "Không tìm thấy yêu cầu." });
        if (request.Status != RoleChangeRequestStatusCodes.Pending) return Conflict(new { message = AlreadyProcessedMessage });

        request.Status = RoleChangeRequestStatusCodes.Rejected;
        request.ReviewedByUserId = actorId;
        request.ReviewedAt = DateTime.Now;
        request.RejectionReason = reason;
        request.EmployeeCodeAfter = null;
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId, Action = AuditActions.RoleChangeRejected,
            EntityName = AuditEntityNames.RoleChangeRequest, EntityId = request.RoleChangeRequestId,
            OldData = JsonSerializer.Serialize(new { request.EmployeeId, EmployeeCodeBefore = request.EmployeeCodeBefore ?? request.Employee.EmployeeCode, request.CurrentRoleId, CurrentRoleName = request.CurrentRole.RoleName, request.RequestedRoleId, RequestedRoleName = request.RequestedRole.RoleName, Status = RoleChangeRequestStatusCodes.Pending }),
            NewData = JsonSerializer.Serialize(new { request.EmployeeId, CurrentRoleName = request.CurrentRole.RoleName, RequestedRoleName = request.RequestedRole.RoleName, Status = RoleChangeRequestStatusCodes.Rejected, RejectionReason = reason }),
            Timestamp = DateTime.Now,
            Notes = $"Admin từ chối yêu cầu thay đổi vai trò của {request.Employee.FullName} từ {request.CurrentRole.RoleName} sang {request.RequestedRole.RoleName}."
        });
        await _notifications.AddAsync(request.RequestedByUserId, actorId,
            NotificationTypeCodes.RoleChangeRejected, "Yêu cầu thay đổi vai trò bị từ chối",
            $"Yêu cầu thay đổi vai trò của {request.EmployeeCodeBefore ?? request.Employee.EmployeeCode} - {request.Employee.FullName} từ {request.CurrentRole.RoleName} sang {request.RequestedRole.RoleName} đã bị từ chối.",
            AuditEntityNames.RoleChangeRequest, request.RoleChangeRequestId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { message = "Đã từ chối yêu cầu thay đổi vai trò." });
    }

    private async Task<string?> GetAssignmentBlockingReasonAsync(int employeeId, string currentRoleName, CancellationToken cancellationToken)
    {
        if (currentRoleName == RoleNames.HallManager)
        {
            var hall = await _context.HallManagerAssignments.AsNoTracking()
                .Where(item => item.HallManagerEmployeeId == employeeId && item.ToDate == null)
                .Select(item => item.Hall.HallName).FirstOrDefaultAsync(cancellationToken);
            return hall is null ? null : $"Không thể duyệt yêu cầu vì nhân viên vẫn đang được bổ nhiệm phụ trách sảnh {hall}. Vui lòng kết thúc hoặc điều chỉnh phân công trước.";
        }
        if (currentRoleName == RoleNames.Coordinator)
        {
            var booking = await _context.CoordinationAssignments.AsNoTracking()
                .Where(item => item.CoordinatorEmployeeId == employeeId &&
                    item.Status.StatusCode == BusinessStatusCodes.Assigned &&
                    item.Booking.Status.StatusCode != BookingStatusCodes.Completed &&
                    item.Booking.Status.StatusCode != BookingStatusCodes.Cancelled)
                .Select(item => item.Booking.BookingCode).FirstOrDefaultAsync(cancellationToken);
            return booking is null ? null : $"Không thể duyệt yêu cầu vì nhân viên vẫn còn nhiệm vụ điều phối tiệc {booking} đang hiệu lực. Vui lòng xử lý phân công trước.";
        }
        return "Vai trò hiện tại không thuộc quy trình thay đổi vai trò.";
    }

    private static bool IsValidRolePair(string current, string requested) =>
        (current == RoleNames.Coordinator && requested == RoleNames.HallManager) ||
        (current == RoleNames.HallManager && requested == RoleNames.Coordinator);
    private bool TryGetActorId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

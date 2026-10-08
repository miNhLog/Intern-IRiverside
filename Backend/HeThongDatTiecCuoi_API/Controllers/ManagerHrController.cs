using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.ManagerHr;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/hr")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerHrController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public ManagerHrController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ManagerHrPageDto>> GetPage(
        string? search, string? role, string? status, string? assignmentStatus,
        int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.Employees.AsNoTracking().Where(e =>
            e.UserId != currentUserId && e.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            (e.User.Role.RoleName == RoleNames.HallManager || e.User.Role.RoleName == RoleNames.Coordinator));

        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(e => e.FullName.Contains(search) || e.EmployeeCode.Contains(search) || e.User.Email.Contains(search) || e.PhoneNumber.Contains(search));
        if (role is RoleNames.HallManager or RoleNames.Coordinator) query = query.Where(e => e.User.Role.RoleName == role);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.User.Status.StatusCode == status);
        if (assignmentStatus == "ASSIGNED") query = query.Where(e => e.User.Role.RoleName == RoleNames.HallManager && e.HallManagerAssignments.Any(a => a.ToDate == null));
        if (assignmentStatus == "UNASSIGNED") query = query.Where(e => e.User.Role.RoleName == RoleNames.HallManager && !e.HallManagerAssignments.Any(a => a.ToDate == null));

        var allEligible = _context.Employees.AsNoTracking().Where(e => e.DataStatus.DataStatusCode == DataStatusCodes.Existing && e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing && (e.User.Role.RoleName == RoleNames.HallManager || e.User.Role.RoleName == RoleNames.Coordinator));
        var totalEmployees = await allEligible.CountAsync(cancellationToken);
        var hallManagerCount = await allEligible.CountAsync(e => e.User.Role.RoleName == RoleNames.HallManager, cancellationToken);
        var totalFiltered = await query.CountAsync(cancellationToken);
        var employees = await query.OrderBy(e => e.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new ManagerEmployeeDto {
                EmployeeId=e.EmployeeId, EmployeeCode=e.EmployeeCode, FullName=e.FullName, Email=e.User.Email,
                PhoneNumber=e.PhoneNumber, RoleName=e.User.Role.RoleName, AccountStatusCode=e.User.Status.StatusCode,
                AccountStatusName=e.User.Status.StatusName,
                HasPendingRoleRequest=e.User.Employee != null && _context.RoleChangeRequests.Any(r => r.EmployeeId == e.EmployeeId && r.Status == RoleChangeRequestStatusCodes.Pending)
            }).ToListAsync(cancellationToken);
        var ids = employees.Select(e => e.EmployeeId).ToList();
        var assignments = await _context.HallManagerAssignments.AsNoTracking().Where(a => ids.Contains(a.HallManagerEmployeeId) && a.ToDate == null)
            .Select(a => new { a.HallManagerEmployeeId, Item = new EmployeeHallAssignmentDto { AssignmentId=a.HallManagerAssignmentId, HallId=a.HallId, HallName=a.Hall.HallName, FromDate=a.FromDate } }).ToListAsync(cancellationToken);
        foreach (var employee in employees) employee.ActiveAssignments = assignments.Where(a => a.HallManagerEmployeeId == employee.EmployeeId).Select(a => a.Item).ToList();

        var roleRequests = await _context.RoleChangeRequests.AsNoTracking().Where(r => r.RequestedByUserId == currentUserId).OrderByDescending(r => r.RequestedAt).Take(100)
            .Select(r => new ManagerRoleRequestDto { RequestId=r.RoleChangeRequestId, EmployeeId=r.EmployeeId, EmployeeCode=r.Employee.EmployeeCode, FullName=r.Employee.FullName, CurrentRoleName=r.CurrentRole.RoleName, RequestedRoleName=r.RequestedRole.RoleName, Reason=r.Reason, Status=r.Status, RequestedAt=r.RequestedAt, ReviewedAt=r.ReviewedAt, RejectionReason=r.RejectionReason }).ToListAsync(cancellationToken);
        var history = await _context.HallManagerAssignments.AsNoTracking().OrderByDescending(a => a.FromDate).ThenByDescending(a => a.HallManagerAssignmentId).Take(200)
            .Select(a => new HallAssignmentHistoryDto { AssignmentId=a.HallManagerAssignmentId, EmployeeId=a.HallManagerEmployeeId, EmployeeCode=a.HallManager.EmployeeCode, EmployeeName=a.HallManager.FullName, HallName=a.Hall.HallName, FromDate=a.FromDate, ToDate=a.ToDate }).ToListAsync(cancellationToken);
        var halls = await _context.Halls.AsNoTracking().Where(h => h.DataStatus.DataStatusCode == DataStatusCodes.Existing && h.Status.StatusCode != HallStatusCodes.Inactive).OrderBy(h => h.HallName)
            .Select(h => new AssignableHallDto { HallId=h.HallId, HallName=h.HallName, StatusCode=h.Status.StatusCode,
                CurrentEmployeeId=h.HallManagerAssignments.Where(a => a.ToDate == null).Select(a => (int?)a.HallManagerEmployeeId).FirstOrDefault(),
                CurrentEmployeeCode=h.HallManagerAssignments.Where(a => a.ToDate == null).Select(a => a.HallManager.EmployeeCode).FirstOrDefault(),
                CurrentManagerName=h.HallManagerAssignments.Where(a => a.ToDate == null).Select(a => a.HallManager.FullName).FirstOrDefault() }).ToListAsync(cancellationToken);
        return Ok(new ManagerHrPageDto { TotalEmployees=totalEmployees, HallManagerCount=hallManagerCount, CoordinatorCount=totalEmployees-hallManagerCount,
            PendingRequestCount=roleRequests.Count(r => r.Status == RoleChangeRequestStatusCodes.Pending), Page=page, PageSize=pageSize,
            TotalPages=(int)Math.Ceiling(totalFiltered/(double)pageSize), Employees=employees, RoleRequests=roleRequests, AssignmentHistory=history, Halls=halls });
    }

    [HttpGet("employees/{employeeId:int}")]
    public async Task<ActionResult<ManagerEmployeeDto>> GetEmployee(int employeeId, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees.AsNoTracking().Where(e => e.EmployeeId == employeeId && e.DataStatus.DataStatusCode == DataStatusCodes.Existing && (e.User.Role.RoleName == RoleNames.HallManager || e.User.Role.RoleName == RoleNames.Coordinator))
            .Select(e => new ManagerEmployeeDto { EmployeeId=e.EmployeeId, EmployeeCode=e.EmployeeCode, FullName=e.FullName, Email=e.User.Email, PhoneNumber=e.PhoneNumber, RoleName=e.User.Role.RoleName, AccountStatusCode=e.User.Status.StatusCode, AccountStatusName=e.User.Status.StatusName, HasPendingRoleRequest=_context.RoleChangeRequests.Any(r => r.EmployeeId == e.EmployeeId && r.Status == RoleChangeRequestStatusCodes.Pending), ActiveAssignments=e.HallManagerAssignments.Where(a => a.ToDate == null).Select(a => new EmployeeHallAssignmentDto { AssignmentId=a.HallManagerAssignmentId, HallId=a.HallId, HallName=a.Hall.HallName, FromDate=a.FromDate }).ToList() }).SingleOrDefaultAsync(cancellationToken);
        return employee is null ? NotFound(new { message="Không tìm thấy nhân sự phù hợp." }) : Ok(employee);
    }

    [HttpPost("employees/{employeeId:int}/hall-assignments")]
    public async Task<ActionResult<ManagerHrActionResponse>> AssignHalls(int employeeId, AssignHallsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var employee = await _context.Employees.Include(e=>e.User).ThenInclude(u=>u.Role).Include(e=>e.User).ThenInclude(u=>u.Status).SingleOrDefaultAsync(e=>e.EmployeeId==employeeId,cancellationToken);
        if (employee is null || employee.User.Role.RoleName != RoleNames.HallManager) return BadRequest(new { message="Chỉ nhân viên có vai trò Quản lý sảnh mới có thể được bổ nhiệm phụ trách sảnh." });
        if (employee.User.Status.StatusCode != AccountStatusCodes.Active) return BadRequest(new { message="Chỉ có thể bổ nhiệm nhân viên có tài khoản đang hoạt động." });
        var hallIds=request.HallIds.Distinct().ToList(); if(hallIds.Count==0) return BadRequest(new {message="Vui lòng chọn ít nhất một sảnh."});
        var halls=await _context.Halls.Where(h=>hallIds.Contains(h.HallId) && h.DataStatus.DataStatusCode==DataStatusCodes.Existing && h.Status.StatusCode!=HallStatusCodes.Inactive).ToListAsync(cancellationToken);
        if(halls.Count!=hallIds.Count) return BadRequest(new {message="Một hoặc nhiều sảnh không tồn tại hoặc không hoạt động."});
        await using var tx=await _context.Database.BeginTransactionAsync(cancellationToken); var today=DateTime.Today; var changed=0;
        foreach(var hallId in hallIds) { var active=await _context.HallManagerAssignments.SingleOrDefaultAsync(a=>a.HallId==hallId && a.ToDate==null,cancellationToken); if(active?.HallManagerEmployeeId==employeeId) continue; if(active is not null) active.ToDate=today; _context.HallManagerAssignments.Add(new HallManagerAssignment{HallId=hallId,HallManagerEmployeeId=employeeId,FromDate=today}); changed++; }
        _context.AuditLogs.Add(new AuditLog{UserId=userId,Action=AuditActions.HallManagerAssigned,EntityName=AuditEntityNames.HallManagerAssignment,EntityId=employeeId,NewData=JsonSerializer.Serialize(new{employeeId,HallIds=hallIds}),Timestamp=DateTime.Now,Notes=$"Bổ nhiệm {employee.EmployeeCode} - {employee.FullName} phụ trách {changed} sảnh."});
        await _context.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return Ok(new ManagerHrActionResponse{Message=changed==0?"Nhân viên đã phụ trách các sảnh được chọn.":$"Đã cập nhật phân công cho {changed} sảnh."});
    }

    [HttpPost("hall-assignments/{assignmentId:int}/end")]
    public async Task<ActionResult<ManagerHrActionResponse>> EndAssignment(int assignmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var assignment=await _context.HallManagerAssignments.Include(a=>a.HallManager).ThenInclude(e=>e.User).ThenInclude(u=>u.Role).Include(a=>a.Hall).SingleOrDefaultAsync(a=>a.HallManagerAssignmentId==assignmentId,cancellationToken);
        if(assignment is null || assignment.ToDate is not null) return NotFound(new {message="Phân công không tồn tại hoặc đã kết thúc."});
        if(assignment.HallManager.User.Role.RoleName!=RoleNames.HallManager) return Conflict(new {message="Dữ liệu phân công không nhất quán; không thể tự động kết thúc."});
        assignment.ToDate=DateTime.Today;
        _context.AuditLogs.Add(new AuditLog{UserId=userId,Action=AuditActions.HallManagerAssignmentEnded,EntityName=AuditEntityNames.HallManagerAssignment,EntityId=assignmentId,Timestamp=DateTime.Now,Notes=$"Kết thúc phân công {assignment.HallManager.EmployeeCode} tại {assignment.Hall.HallName}."});
        await _context.SaveChangesAsync(cancellationToken); return Ok(new ManagerHrActionResponse{Message="Đã kết thúc phụ trách sảnh."});
    }

    private bool TryGetCurrentUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

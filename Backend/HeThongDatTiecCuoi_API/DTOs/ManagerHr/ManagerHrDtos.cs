using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.ManagerHr;

public sealed class ManagerHrPageDto
{
    public int TotalEmployees { get; init; }
    public int HallManagerCount { get; init; }
    public int CoordinatorCount { get; init; }
    public int PendingRequestCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public List<ManagerEmployeeDto> Employees { get; init; } = [];
    public List<ManagerRoleRequestDto> RoleRequests { get; init; } = [];
    public List<HallAssignmentHistoryDto> AssignmentHistory { get; init; } = [];
    public List<AssignableHallDto> Halls { get; init; } = [];
}

public sealed class ManagerEmployeeDto
{
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string AccountStatusCode { get; init; } = string.Empty;
    public string AccountStatusName { get; init; } = string.Empty;
    public bool HasPendingRoleRequest { get; init; }
    public List<EmployeeHallAssignmentDto> ActiveAssignments { get; set; } = [];
}

public sealed class EmployeeHallAssignmentDto
{
    public int AssignmentId { get; init; }
    public int HallId { get; init; }
    public string HallName { get; init; } = string.Empty;
    public DateTime FromDate { get; init; }
}

public sealed class AssignableHallDto
{
    public int HallId { get; init; }
    public string HallName { get; init; } = string.Empty;
    public string StatusCode { get; init; } = string.Empty;
    public int? CurrentEmployeeId { get; init; }
    public string? CurrentEmployeeCode { get; init; }
    public string? CurrentManagerName { get; init; }
}

public sealed class HallAssignmentHistoryDto
{
    public int AssignmentId { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public DateTime FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

public sealed class ManagerRoleRequestDto
{
    public int RequestId { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string CurrentRoleName { get; init; } = string.Empty;
    public string RequestedRoleName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class AssignHallsRequest
{
    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một sảnh.")]
    public List<int> HallIds { get; init; } = [];
}

public sealed class ManagerHrActionResponse
{
    public string Message { get; init; } = string.Empty;
}

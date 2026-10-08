namespace HeThongDatTiecCuoi_WEB.Models.ManagerHr;

public sealed class ManagerHrPageViewModel
{
    public int TotalEmployees { get; set; } public int HallManagerCount { get; set; } public int CoordinatorCount { get; set; } public int PendingRequestCount { get; set; }
    public int Page { get; set; } public int PageSize { get; set; } public int TotalPages { get; set; }
    public List<ManagerEmployeeViewModel> Employees { get; set; }=[]; public List<ManagerRoleRequestItem> RoleRequests { get; set; }=[]; public List<HallAssignmentHistoryItem> AssignmentHistory { get; set; }=[]; public List<AssignableHallViewModel> Halls { get; set; }=[];
    public string? Search { get; set; } public string? Role { get; set; } public string? Status { get; set; } public string? AssignmentStatus { get; set; } public string? ErrorMessage { get; set; }
}
public sealed class ManagerEmployeeViewModel { public int EmployeeId{get;set;} public string EmployeeCode{get;set;}=""; public string FullName{get;set;}=""; public string Email{get;set;}=""; public string PhoneNumber{get;set;}=""; public string RoleName{get;set;}=""; public string AccountStatusCode{get;set;}=""; public string AccountStatusName{get;set;}=""; public bool HasPendingRoleRequest{get;set;} public List<EmployeeHallAssignmentViewModel> ActiveAssignments{get;set;}=[]; }
public sealed class EmployeeHallAssignmentViewModel { public int AssignmentId{get;set;} public int HallId{get;set;} public string HallName{get;set;}=""; public DateTime FromDate{get;set;} }
public sealed class AssignableHallViewModel { public int HallId{get;set;} public string HallName{get;set;}=""; public string StatusCode{get;set;}=""; public int? CurrentEmployeeId{get;set;} public string? CurrentEmployeeCode{get;set;} public string? CurrentManagerName{get;set;} }
public sealed class HallAssignmentHistoryItem { public int AssignmentId{get;set;} public int EmployeeId{get;set;} public string EmployeeCode{get;set;}=""; public string EmployeeName{get;set;}=""; public string HallName{get;set;}=""; public DateTime FromDate{get;set;} public DateTime? ToDate{get;set;} }
public sealed class ManagerRoleRequestItem { public int RequestId{get;set;} public int EmployeeId{get;set;} public string EmployeeCode{get;set;}=""; public string FullName{get;set;}=""; public string CurrentRoleName{get;set;}=""; public string RequestedRoleName{get;set;}=""; public string? Reason{get;set;} public string Status{get;set;}=""; public DateTime RequestedAt{get;set;} public DateTime? ReviewedAt{get;set;} public string? RejectionReason{get;set;} }
public sealed class AssignHallsViewModel { public List<int> HallIds{get;set;}=[]; }
public sealed class ManagerHrActionViewModel { public string Message{get;set;}=""; }

namespace HeThongDatTiecCuoi_WEB.Models.AdminAccount;

public sealed class AccountDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? EmployeeStatus { get; set; }
    public string? EmployeeStatusName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool MustChangePassword { get; set; }
}

public sealed class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public sealed class AccountManagementViewModel
{
    public AccountDto? AdminAccount { get; set; }
    public List<AccountDto> Accounts { get; set; } = [];
    public List<RoleDto> Roles { get; set; } = [];
    public string? Keyword { get; set; }
    public int? RoleId { get; set; }
    public string? Status { get; set; }
    public bool? MustChangePassword { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UpdateAdministratorAccountRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class SystemDashboardViewModel
{
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int LockedAccounts { get; set; }
    public int MustChangePasswordEmployees { get; set; }
    public int TotalInternalEmployees { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<RoleDistributionViewModel> RoleDistribution { get; set; } = [];
    public List<DashboardAccountViewModel> RecentInternalAccounts { get; set; } = [];
    public List<DashboardAttentionAccountViewModel> AttentionAccounts { get; set; } = [];
    public List<AuditLogViewModel> RecentAuditLogs { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class AuditLogViewModel
{
    public long AuditLogId { get; set; }
    public DateTime Timestamp { get; set; }
    public int? ActorUserId { get; set; }
    public string Actor { get; set; } = "Hệ thống";
    public string ActorName { get; set; } = "Hệ thống";
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public long TargetUserId { get; set; }
    public string Target { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public string? Notes { get; set; }
}

public sealed class AuditLogPageViewModel
{
    public List<AuditLogViewModel> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public string? Keyword { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class RoleDistributionViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardAccountViewModel
{
    public int UserId { get; set; }
    public string? EmployeeCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class DashboardAttentionAccountViewModel : DashboardAccountViewModel
{
    public string Issue { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CreateEmployeeAccountRequest
{
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class UpdateEmployeeAccountRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? EmployeeStatus { get; set; }
}

public sealed class AccountActionResponse
{
    public string Message { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public string? EmployeeCode { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? RoleName { get; set; }
    public string? Status { get; set; }
    public string? AccountStatus { get; set; }
    public string? EmployeeStatus { get; set; }
}

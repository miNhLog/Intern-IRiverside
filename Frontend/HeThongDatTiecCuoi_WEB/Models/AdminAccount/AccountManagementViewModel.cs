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
}

public sealed class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public sealed class AccountManagementViewModel
{
    public List<AccountDto> Accounts { get; set; } = [];
    public List<RoleDto> Roles { get; set; } = [];
    public string? Keyword { get; set; }
    public int? RoleId { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class CreateEmployeeAccountRequest
{
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string InitialPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class UpdateEmployeeAccountRequest
{
    public int RoleId { get; set; }
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

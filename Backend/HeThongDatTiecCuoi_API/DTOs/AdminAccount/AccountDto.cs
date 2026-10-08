namespace HeThongDatTiecCuoi_API.DTOs.AdminAccount;

public sealed class AccountDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public byte RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? EmployeeCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DataStatus { get; set; } = string.Empty;
    public string DataStatusName { get; set; } = string.Empty;
    public string? EmployeeDataStatus { get; set; }
    public string? EmployeeDataStatusName { get; set; }
    public bool MustChangePassword { get; set; }
}

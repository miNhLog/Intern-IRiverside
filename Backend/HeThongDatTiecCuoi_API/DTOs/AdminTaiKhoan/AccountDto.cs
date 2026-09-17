namespace HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;

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

    public DateTime CreatedAt { get; set; }
    public string? EmployeeStatus { get; set; }
}

namespace HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;

public sealed class UpdateStaffAccountRequest
{
    public int RoleId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? EmployeeStatus { get; set; }
}

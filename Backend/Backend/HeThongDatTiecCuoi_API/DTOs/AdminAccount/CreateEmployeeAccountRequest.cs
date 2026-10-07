namespace HeThongDatTiecCuoi_API.DTOs.AdminAccount;

public sealed class CreateEmployeeAccountRequest
{
    public byte RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

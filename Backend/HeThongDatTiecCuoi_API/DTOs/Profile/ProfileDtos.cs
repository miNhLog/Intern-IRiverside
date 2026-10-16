using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Profile;

public sealed record ProfileResponse(
    int UserId,
    string Email,
    string RoleName,
    string AccountStatus,
    DateTime CreatedAt,
    string? EmployeeCode,
    string? FullName,
    string? PhoneNumber);

public sealed class UpdateProfileRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}

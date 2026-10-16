using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HeThongDatTiecCuoi_WEB.Models.Profile;

public sealed record ProfileDto(
    int UserId,
    string Email,
    string RoleName,
    string AccountStatus,
    DateTime CreatedAt,
    string? EmployeeCode,
    string? FullName,
    string? PhoneNumber);

public sealed record ProfileUpdateResponse(string Message, ProfileDto Profile);

public sealed class ProfilePageViewModel
{
    [ValidateNever]
    public ProfileDto Profile { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}

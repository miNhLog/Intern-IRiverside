using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [StringLength(150)]
    public string Identifier { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100)]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

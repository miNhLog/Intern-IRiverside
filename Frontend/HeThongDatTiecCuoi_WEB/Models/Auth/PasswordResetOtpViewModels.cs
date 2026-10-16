using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public class SendPasswordResetOtpViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class VerifyPasswordResetOtpViewModel : SendPasswordResetOtpViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
    public string OtpCode { get; set; } = string.Empty;
}

public sealed class CompletePasswordResetViewModel
{
    [Required]
    public string ResetToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

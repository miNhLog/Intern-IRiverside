using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed class VerifyPasswordResetOtpRequest
{
    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$")]
    public string OtpCode { get; set; } = string.Empty;
}

public sealed record PasswordResetOtpResponse(string Message, int CooldownSeconds);
public sealed record PasswordResetOtpVerificationResponse(string Message, string ResetToken);

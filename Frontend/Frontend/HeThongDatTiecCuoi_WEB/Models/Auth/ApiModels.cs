using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed record CurrentUserDto(
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string RoleName,
    string Status,
    bool MustChangePassword);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserDto User);

public sealed record ApiErrorDto(string Message, Dictionary<string, string[]>? Errors = null);
public sealed record MessageResponseDto(string Message);
public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record ApiCallResult<T>(bool Succeeded, T? Value, string? Error, int? StatusCode = null)
{
    public static ApiCallResult<T> Success(T value) => new(true, value, null);
    public static ApiCallResult<T> Failure(string error, int? statusCode = null) =>
        new(false, default, error, statusCode);
}

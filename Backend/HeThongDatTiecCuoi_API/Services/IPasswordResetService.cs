namespace HeThongDatTiecCuoi_API.Services;

public interface IPasswordResetService
{
    Task<bool> SendResetOtpForEmailAsync(string email, CancellationToken cancellationToken);
    Task<ServiceResult<object>> VerifyResetOtpAsync(
        string email,
        string otpCode,
        CancellationToken cancellationToken);
    Task<bool> SendResetLinkForEmailAsync(string email, CancellationToken cancellationToken);
    Task<ServiceResult<object>> SendResetLinkForUserAsync(int userId, CancellationToken cancellationToken);
    Task<ServiceResult<object>> ResetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken);
}

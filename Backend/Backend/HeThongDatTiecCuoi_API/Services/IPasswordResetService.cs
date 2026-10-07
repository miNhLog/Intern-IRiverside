namespace HeThongDatTiecCuoi_API.Services;

public interface IPasswordResetService
{
    Task<bool> SendResetLinkForEmailAsync(string email, CancellationToken cancellationToken);
    Task<ServiceResult<object>> SendResetLinkForUserAsync(int userId, CancellationToken cancellationToken);
    Task<ServiceResult<object>> ResetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken);
}

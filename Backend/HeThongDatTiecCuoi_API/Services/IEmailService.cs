namespace HeThongDatTiecCuoi_API.Services;

public interface IEmailService
{
    Task SendFirstPasswordOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetLink,
        int lifetimeMinutes,
        bool administratorRequested,
        CancellationToken cancellationToken = default);
}

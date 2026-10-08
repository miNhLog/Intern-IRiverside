namespace HeThongDatTiecCuoi_API.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetLink,
        int lifetimeMinutes,
        bool administratorRequested,
        CancellationToken cancellationToken = default);
}

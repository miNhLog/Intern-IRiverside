namespace HeThongDatTiecCuoi_API.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetLink,
        CancellationToken cancellationToken = default);
}

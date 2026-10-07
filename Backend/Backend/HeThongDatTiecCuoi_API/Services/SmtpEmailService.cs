using System.Net;
using System.Net.Mail;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;

    public SmtpEmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetLink,
        int lifetimeMinutes,
        bool administratorRequested,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new InvalidOperationException(
                "SMTP chưa được cấu hình. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName),
            Subject = "[Riverside Palace] Yêu cầu đặt lại mật khẩu",
            Body = $"""
                Xin chào nhân viên Riverside Palace,

                {(administratorRequested ? "Quản trị viên hệ thống đã yêu cầu đặt lại mật khẩu cho tài khoản của bạn." : "Hệ thống đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.")}

                Đặt lại mật khẩu:
                {resetLink}

                Liên kết chỉ sử dụng được một lần và hết hạn sau {lifetimeMinutes} phút.

                Nếu bạn không nhận ra yêu cầu này, vui lòng liên hệ Quản trị viên hệ thống.

                Riverside Palace
                """,
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Username, _options.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}

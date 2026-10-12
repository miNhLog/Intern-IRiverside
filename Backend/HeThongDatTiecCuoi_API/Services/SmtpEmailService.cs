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

    public Task SendFirstPasswordOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            "[Riverside Palace] Mã xác nhận đổi mật khẩu lần đầu",
            $"""
            Xin chào nhân viên Riverside Palace,

            Mã OTP xác nhận đổi mật khẩu lần đầu của bạn là: {otpCode}

            Mã có hiệu lực trong {lifetimeMinutes} phút và chỉ sử dụng được một lần.
            Không chia sẻ mã này với bất kỳ ai.

            Nếu bạn không thực hiện yêu cầu này, vui lòng liên hệ Quản trị viên hệ thống.

            Riverside Palace
            """,
            cancellationToken);

    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetLink,
        int lifetimeMinutes,
        bool administratorRequested,
        CancellationToken cancellationToken = default)
    {
        var body = $"""
                Xin chào nhân viên Riverside Palace,

                {(administratorRequested ? "Quản trị viên hệ thống đã yêu cầu đặt lại mật khẩu cho tài khoản của bạn." : "Hệ thống đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.")}

                Đặt lại mật khẩu:
                {resetLink}

                Liên kết chỉ sử dụng được một lần và hết hạn sau {lifetimeMinutes} phút.

                Nếu bạn không nhận ra yêu cầu này, vui lòng liên hệ Quản trị viên hệ thống.

                Riverside Palace
                """;
        await SendAsync(recipientEmail, "[Riverside Palace] Yêu cầu đặt lại mật khẩu", body, cancellationToken);
    }

    private async Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.SenderEmail))
            throw new InvalidOperationException("SMTP chưa được cấu hình. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName),
            Subject = subject,
            Body = body,
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

using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>
/// FR15 / UC13: Alias đúng tên yêu cầu khóa luận (EmailService).
/// Kế thừa toàn bộ logic SMTP/HTML từ <see cref="SmtpEmailService"/> để DI
/// có thể đăng ký <c>AddScoped&lt;IEmailService, EmailService&gt;</c>.
/// Dùng SmtpClient async (không bắt buộc thêm package MailKit/MimeKit).
/// </summary>
public sealed class EmailService : SmtpEmailService
{
    public EmailService(
        IOptions<SmtpOptions> smtp,
        IOptions<MailSettings> mail,
        ILogger<SmtpEmailService> logger)
        : base(smtp, mail, logger)
    {
    }
}

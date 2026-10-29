using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _smtp;
    private readonly MailSettings _mail;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<SmtpOptions> smtp,
        IOptions<MailSettings> mail,
        ILogger<SmtpEmailService> logger)
    {
        _smtp = smtp.Value;
        _mail = mail.Value;
        _logger = logger;
    }

    // Constructor tương thích khi DI chỉ đăng ký SmtpOptions (giữ build cũ / test).
    public SmtpEmailService(IOptions<SmtpOptions> smtp)
        : this(smtp, Microsoft.Extensions.Options.Options.Create(new MailSettings()),
            LoggerFactory.Create(b => { }).CreateLogger<SmtpEmailService>())
    {
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

    public Task SendPasswordResetOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            "[Riverside Palace] Mã OTP đặt lại mật khẩu",
            $"""
            Xin chào nhân viên Riverside Palace,

            Mã OTP đặt lại mật khẩu của bạn là: {otpCode}

            Mã có hiệu lực trong {lifetimeMinutes} phút và chỉ sử dụng được một lần.
            Không chia sẻ mã này với bất kỳ ai.

            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và liên hệ Quản trị viên hệ thống.

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

    /// <inheritdoc />
    public Task SendBookingConfirmationAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        int totalTables,
        int guestCount,
        string? menuName,
        string? decorName,
        decimal totalEstimatedCost,
        DateTime holdExpiresAt,
        string? phoneNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"[Riverside Palace] Xác nhận đăng ký giữ chỗ tiệc cưới - Mã hồ sơ: {bookingCode}";
        var html = BuildBookingConfirmationHtml(
            customerName, bookingCode, eventDate, shift, hallName,
            totalTables, guestCount, menuName, decorName, totalEstimatedCost, holdExpiresAt,
            _mail.ClientAppBaseUrl, phoneNumber);
        return SendHtmlAsync(recipientEmail, subject, html, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendContractStatusChangedAsync(
        string recipientEmail,
        string customerName,
        string contractCode,
        string bookingCode,
        string statusName,
        decimal totalValue,
        decimal paidAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"[Riverside Palace] Cập nhật hợp đồng {contractCode} - {statusName}";
        var html = BuildContractStatusHtml(
            customerName, contractCode, bookingCode, statusName, totalValue, paidAmount,
            eventDate, shift, hallName, _mail.ClientAppBaseUrl);
        return SendHtmlAsync(recipientEmail, subject, html, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendBookingCancelledAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"[Riverside Palace] Đã hủy giữ chỗ tiệc cưới - Mã hồ sơ: {bookingCode}";
        var html = BuildBookingCancelledHtml(
            customerName, bookingCode, eventDate, shift, hallName, cancellationReason, _mail.ClientAppBaseUrl);
        return SendHtmlAsync(recipientEmail, subject, html, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendBookingRescheduledAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        DateTime oldEventDate,
        string oldShift,
        string oldHallName,
        DateTime newEventDate,
        string newShift,
        string newHallName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"[Riverside Palace] Đã đổi lịch tiệc cưới - Mã hồ sơ: {bookingCode}";
        var html = BuildBookingRescheduledHtml(
            customerName, bookingCode,
            oldEventDate, oldShift, oldHallName,
            newEventDate, newShift, newHallName,
            _mail.ClientAppBaseUrl);
        return SendHtmlAsync(recipientEmail, subject, html, cancellationToken);
    }

    internal static string BuildContractStatusHtml(
        string customerName,
        string contractCode,
        string bookingCode,
        string statusName,
        decimal totalValue,
        decimal paidAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        string? clientAppBaseUrl = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        string Money(decimal amount) => E(amount.ToString("N0", vi) + " VNĐ");
        var remaining = totalValue - paidAmount;
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = $"{baseUrl}/MyBooking";

        var sb = new StringBuilder();
        sb.Append($"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Cập nhật hợp đồng {E(contractCode)}</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:Arial,Helvetica,sans-serif;color:#111822;">
        <div style="max-width:640px;margin:0 auto;padding:24px 16px;">
          <div style="background:#111822;color:#fff;text-align:center;padding:28px 20px;border-radius:12px 12px 0 0;">
            <div style="letter-spacing:4px;font-size:13px;color:#B89758;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;">Cập nhật hợp đồng tiệc cưới</h1>
            <p style="margin:0;color:#d7d7d7;font-size:13px;">Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</p>
          </div>
          <div style="background:#fff;border:1px solid #eee;border-top:none;border-radius:0 0 12px 12px;padding:24px;">
            <p style="font-size:15px;">Xin chào <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:14px;line-height:1.6;">Hợp đồng của Quý khách vừa được cập nhật trạng thái mới.</p>
            <div style="border:2px dashed #B89758;border-radius:10px;padding:16px;text-align:center;background:#FDFBF7;margin:16px 0;">
              <div style="font-size:13px;color:#666;">MÃ HỢP ĐỒNG</div>
              <div style="font-size:24px;font-weight:bold;letter-spacing:2px;color:#111822;">{E(contractCode)}</div>
              <div style="font-size:12px;color:#666;margin-top:6px;">Trạng thái: <strong>{E(statusName)}</strong></div>
            </div>
            <table style="width:100%;border-collapse:collapse;font-size:14px;">
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;width:38%;">Mã hồ sơ</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{E(bookingCode)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Sự kiện</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{eventStr} — {E(hallName)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Tổng giá trị hợp đồng</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;color:#B89758;">{Money(totalValue)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Đã thanh toán</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;">{Money(paidAmount)}</td></tr>
              <tr><td style="padding:10px;color:#666;">Còn lại</td><td style="padding:10px;font-weight:bold;">{Money(remaining)}</td></tr>
            </table>
            <div style="background:#fff8e8;border-left:4px solid #B89758;padding:12px 14px;border-radius:6px;margin:16px 0;font-size:13px;line-height:1.6;">
              Nếu cần hỗ trợ về hợp đồng hoặc các khoản thanh toán, vui lòng liên hệ Riverside Palace.
            </div>
            <div style="text-align:center;margin:20px 0;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;font-size:15px;">Tra cứu hồ sơ của tôi</a>
              <div style="font-size:12px;color:#666;margin-top:8px;">{E(lookupUrl)}</div>
            </div>
            <hr style="border:none;border-top:1px solid #eee;margin:20px 0;"/>
            <p style="font-size:12px;color:#666;line-height:1.7;margin:0;">
              Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace<br/>
              Địa chỉ: 360D Bến Vân Đồn, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline tư vấn: <strong>0933 777 888</strong> — Email: riversidepalace.contact@gmail.com
            </p>
          </div>
        </div>
        </body></html>
        """);
        return sb.ToString();
    }

    internal static string BuildBookingConfirmationHtml(
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        int totalTables,
        int guestCount,
        string? menuName,
        string? decorName,
        decimal totalEstimatedCost,
        DateTime holdExpiresAt,
        string? clientAppBaseUrl = null,
        string? phoneNumber = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var totalStr = totalEstimatedCost.ToString("N0", vi) + " VNĐ";
        // Quy ước chung: hiển thị duy nhất `dd/MM/yyyy · Ca` (khớp lookup MyBooking/Index.cshtml:184).
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var scaleStr = $"{guestCount} khách ({totalTables} bàn)";
        var holdStr = holdExpiresAt.ToString("HH:mm - dd/MM/yyyy", vi);
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        // Không đưa mã + SĐT lên URL: khách tự nhập tại form /MyBooking sau khi mở email.
        var lookupUrl = $"{baseUrl}/MyBooking";

        var sb = new StringBuilder();
        sb.Append($"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Xác nhận giữ chỗ {E(bookingCode)}</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:Arial,Helvetica,sans-serif;color:#111822;">
        <div style="max-width:640px;margin:0 auto;padding:24px 16px;">
          <div style="background:#111822;color:#fff;text-align:center;padding:28px 20px;border-radius:12px 12px 0 0;">
            <div style="letter-spacing:4px;font-size:13px;color:#B89758;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;">Xác nhận đăng ký giữ chỗ tiệc cưới</h1>
            <p style="margin:0;color:#d7d7d7;font-size:13px;">Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</p>
          </div>
          <div style="background:#fff;border:1px solid #eee;border-top:none;border-radius:0 0 12px 12px;padding:24px;">
            <p style="font-size:15px;">Xin chào <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:14px;line-height:1.6;">Cảm ơn Quý khách đã tin tưởng Riverside Palace. Hồ sơ giữ chỗ của Quý khách đã được ghi nhận thành công với thông tin bên dưới.</p>
            <div style="border:2px dashed #B89758;border-radius:10px;padding:16px;text-align:center;background:#FDFBF7;margin:16px 0;">
              <div style="font-size:13px;color:#666;">MÃ HỒ SƠ GIỮ CHỖ</div>
              <div style="font-size:26px;font-weight:bold;letter-spacing:2px;color:#111822;">{E(bookingCode)}</div>
              <div style="font-size:12px;color:#666;margin-top:6px;">Vui lòng lưu mã này để tra cứu tiến độ tại mục <strong>MyBooking</strong>.</div>
            </div>
            <table style="width:100%;border-collapse:collapse;font-size:14px;">
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;width:38%;">Đại sảnh</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Thời gian</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{eventStr}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Quy mô</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{E(scaleStr)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Thực đơn</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;">{E(menuName)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Trang trí</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;">{E(decorName)}</td></tr>
              <tr><td style="padding:10px;color:#666;">Tổng dự kiến</td><td style="padding:10px;font-weight:bold;color:#B89758;font-size:16px;">{E(totalStr)}</td></tr>
            </table>
            <div style="background:#fff8e8;border-left:4px solid #B89758;padding:12px 14px;border-radius:6px;margin:16px 0;font-size:13px;line-height:1.6;">
              <strong>Lưu ý giữ chỗ 48 giờ (BR07):</strong> hồ sơ được giữ đến <strong>{E(holdStr)}</strong>.
              Sau thời gian này nếu chưa xác nhận đặt cọc, lịch sảnh sẽ tự động giải phóng.
              Đội ngũ tư vấn sẽ liên hệ trong <strong>24 giờ</strong> để hỗ trợ Quý khách hoàn tất thủ tục.
            </div>
            <div style="text-align:center;margin:20px 0;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;font-size:15px;">Tra cứu hồ sơ của tôi</a>
              <div style="font-size:12px;color:#666;margin-top:8px;">{E(lookupUrl)}</div>
            </div>
            <hr style="border:none;border-top:1px solid #eee;margin:20px 0;"/>
            <p style="font-size:12px;color:#666;line-height:1.7;margin:0;">
              Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace<br/>
              Địa chỉ: 360D Bến Vân Đồn, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline tư vấn: <strong>0933 777 888</strong> — Email: riversidepalace.contact@gmail.com
            </p>
          </div>
        </div>
        </body></html>
        """);
        return sb.ToString();
    }

    internal static string BuildBookingCancelledHtml(
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        string cancellationReason,
        string? clientAppBaseUrl = null)
    {
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = $"{baseUrl}/MyBooking";

        var sb = new StringBuilder();
        sb.Append($"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Đã hủy giữ chỗ {E(bookingCode)}</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:Arial,Helvetica,sans-serif;color:#111822;">
        <div style="max-width:640px;margin:0 auto;padding:24px 16px;">
          <div style="background:#111822;color:#fff;text-align:center;padding:28px 20px;border-radius:12px 12px 0 0;">
            <div style="letter-spacing:4px;font-size:13px;color:#B89758;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;">Đã hủy giữ chỗ tiệc cưới</h1>
            <p style="margin:0;color:#d7d7d7;font-size:13px;">Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</p>
          </div>
          <div style="background:#fff;border:1px solid #eee;border-top:none;border-radius:0 0 12px 12px;padding:24px;">
            <p style="font-size:15px;">Xin chào <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:14px;line-height:1.6;">Riverside Palace xác nhận hồ sơ giữ chỗ của Quý khách đã được hủy theo yêu cầu trực tuyến.</p>
            <div style="border:2px dashed #B89758;border-radius:10px;padding:16px;text-align:center;background:#FDFBF7;margin:16px 0;">
              <div style="font-size:13px;color:#666;">MÃ HỒ SƠ</div>
              <div style="font-size:24px;font-weight:bold;letter-spacing:2px;color:#111822;">{E(bookingCode)}</div>
              <div style="font-size:12px;color:#666;margin-top:6px;">Trạng thái: <strong>Đã hủy</strong></div>
            </div>
            <table style="width:100%;border-collapse:collapse;font-size:14px;">
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;width:38%;">Thời gian dự kiến</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{eventStr}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Đại sảnh</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px;color:#666;">Lý do hủy</td><td style="padding:10px;">{E(cancellationReason)}</td></tr>
            </table>
            <div style="background:#fff8e8;border-left:4px solid #B89758;padding:12px 14px;border-radius:6px;margin:16px 0;font-size:13px;line-height:1.6;">
              Nếu Quý khách cần đặt lại tiệc hoặc có thắc mắc về hồ sơ, vui lòng liên hệ hotline <strong>0933 777 888</strong>.
            </div>
            <div style="text-align:center;margin:20px 0;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;font-size:15px;">Tra cứu hồ sơ của tôi</a>
            </div>
            <hr style="border:none;border-top:1px solid #eee;margin:20px 0;"/>
            <p style="font-size:12px;color:#666;line-height:1.7;margin:0;">
              Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace<br/>
              Địa chỉ: 360D Bến Vân Đồn, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline tư vấn: <strong>0933 777 888</strong> — Email: riversidepalace.contact@gmail.com
            </p>
          </div>
        </div>
        </body></html>
        """);
        return sb.ToString();
    }

    internal static string BuildBookingRescheduledHtml(
        string customerName,
        string bookingCode,
        DateTime oldEventDate,
        string oldShift,
        string oldHallName,
        DateTime newEventDate,
        string newShift,
        string newHallName,
        string? clientAppBaseUrl = null)
    {
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var oldEventStr = $"{oldEventDate:dd/MM/yyyy} · {E(oldShift)}";
        var newEventStr = $"{newEventDate:dd/MM/yyyy} · {E(newShift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = $"{baseUrl}/MyBooking";

        var sb = new StringBuilder();
        sb.Append($"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Đã đổi lịch {E(bookingCode)}</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:Arial,Helvetica,sans-serif;color:#111822;">
        <div style="max-width:640px;margin:0 auto;padding:24px 16px;">
          <div style="background:#111822;color:#fff;text-align:center;padding:28px 20px;border-radius:12px 12px 0 0;">
            <div style="letter-spacing:4px;font-size:13px;color:#B89758;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;">Đã đổi lịch tiệc cưới</h1>
            <p style="margin:0;color:#d7d7d7;font-size:13px;">Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</p>
          </div>
          <div style="background:#fff;border:1px solid #eee;border-top:none;border-radius:0 0 12px 12px;padding:24px;">
            <p style="font-size:15px;">Xin chào <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:14px;line-height:1.6;">Riverside Palace xác nhận hồ sơ giữ chỗ của Quý khách đã được đổi sang lịch mới.</p>
            <div style="border:2px dashed #B89758;border-radius:10px;padding:16px;text-align:center;background:#FDFBF7;margin:16px 0;">
              <div style="font-size:13px;color:#666;">MÃ HỒ SƠ</div>
              <div style="font-size:24px;font-weight:bold;letter-spacing:2px;color:#111822;">{E(bookingCode)}</div>
              <div style="font-size:12px;color:#666;margin-top:6px;">Lịch mới: <strong>{newEventStr}</strong></div>
            </div>
            <table style="width:100%;border-collapse:collapse;font-size:14px;">
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;width:38%;">Lịch cũ</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;">{oldEventStr} — {E(oldHallName)}</td></tr>
              <tr><td style="padding:10px;border-bottom:1px solid #f0e8d5;color:#666;">Lịch mới</td><td style="padding:10px;border-bottom:1px solid #f0e8d5;font-weight:bold;color:#B89758;">{newEventStr} — {E(newHallName)}</td></tr>
              <tr><td style="padding:10px;color:#666;">Đại sảnh</td><td style="padding:10px;font-weight:bold;">{E(newHallName)} (không thay đổi)</td></tr>
            </table>
            <div style="background:#fff8e8;border-left:4px solid #B89758;padding:12px 14px;border-radius:6px;margin:16px 0;font-size:13px;line-height:1.6;">
              Thời hạn giữ chỗ 48 giờ của hồ sơ <strong>không thay đổi</strong> khi đổi lịch.
              Nếu cần hỗ trợ thêm, vui lòng liên hệ hotline <strong>0933 777 888</strong>.
            </div>
            <div style="text-align:center;margin:20px 0;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;font-size:15px;">Tra cứu hồ sơ của tôi</a>
            </div>
            <hr style="border:none;border-top:1px solid #eee;margin:20px 0;"/>
            <p style="font-size:12px;color:#666;line-height:1.7;margin:0;">
              Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace<br/>
              Địa chỉ: 360D Bến Vân Đồn, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline tư vấn: <strong>0933 777 888</strong> — Email: riversidepalace.contact@gmail.com
            </p>
          </div>
        </div>
        </body></html>
        """);
        return sb.ToString();
    }

    private (string Host, int Port, bool EnableSsl, string SenderEmail, string SenderName, ICredentialsByHost Credentials) ResolveSmtp()
    {
        // Ưu tiên MailSettings (FR15), fallback Smtp cũ (OTP/Reset) để không gãy cấu hình hiện tại.
        string host = string.IsNullOrWhiteSpace(_mail.Server) ? _smtp.Host : _mail.Server.Trim();
        var senderEmail = string.IsNullOrWhiteSpace(_mail.SenderEmail) ? _smtp.SenderEmail : _mail.SenderEmail.Trim();
        var senderName = string.IsNullOrWhiteSpace(_mail.SenderName) ? _smtp.SenderName : _mail.SenderName.Trim();
        var password = string.IsNullOrWhiteSpace(_mail.Password) || _mail.Password == "YOUR_APP_PASSWORD"
            ? _smtp.Password : _mail.Password;
        var username = string.IsNullOrWhiteSpace(_smtp.Username) ? senderEmail : _smtp.Username;
        var port = _mail.Port != 0 ? _mail.Port : _smtp.Port;
        var ssl = _mail.EnableSsl || _smtp.EnableSsl;

        if (string.IsNullOrWhiteSpace(password) || password == "YOUR_APP_PASSWORD")
            throw new InvalidOperationException("Chưa cấu hình mật khẩu ứng dụng SMTP (MailSettings:Password). Nạp qua dotnet user-secrets hoặc biến môi trường MailSettings__Password.");

        ICredentialsByHost creds = string.IsNullOrWhiteSpace(username)
            ? CredentialCache.DefaultNetworkCredentials
            : new NetworkCredential(username, password);
        return (host, port, ssl, senderEmail, senderName, creds);
    }

    private async Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var cfg = ResolveSmtp();
        if (string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.SenderEmail))
            throw new InvalidOperationException("SMTP chưa được cấu hình. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");

        using var message = new MailMessage
        {
            From = new MailAddress(cfg.SenderEmail, cfg.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = cfg.Credentials
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task SendHtmlAsync(string recipientEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var cfg = ResolveSmtp();
        if (string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.SenderEmail))
            throw new InvalidOperationException("SMTP chưa được cấu hình (MailSettings/Smtp). Hãy điền SenderEmail + App Password.");

        using var message = new MailMessage
        {
            From = new MailAddress(cfg.SenderEmail, cfg.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = cfg.Credentials,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Gửi email xác nhận giữ chỗ tới {Email}, subject={Subject}", recipientEmail, subject);
        await client.SendMailAsync(message, cancellationToken);
    }
}

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

    public SmtpEmailService(IOptions<SmtpOptions> smtp)
        : this(smtp, Microsoft.Extensions.Options.Options.Create(new MailSettings()),
            LoggerFactory.Create(b => { }).CreateLogger<SmtpEmailService>())
    {
    }

    public Task SendScheduleChangeOtpEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            $"Riverside Palace · Mã xác nhận thay đổi lịch {bookingCode}",
            $"""
            Xin chào {customerName},

            Mã OTP xác nhận yêu cầu thay đổi lịch cho booking {bookingCode} là: {otpCode}

            Mã có hiệu lực trong {lifetimeMinutes} phút và chỉ sử dụng được một lần.
            Không chia sẻ mã này với bất kỳ ai. Riverside Palace không yêu cầu bạn cung cấp OTP qua điện thoại.

            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và liên hệ Riverside Palace.

            Riverside Palace Convention Center
            """,
            cancellationToken);

    public Task SendFirstPasswordOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            "Riverside Palace · Mã xác nhận đổi mật khẩu lần đầu",
            $"""
            Xin chào nhân viên Riverside Palace,

            Mã OTP xác nhận đổi mật khẩu lần đầu của bạn là: {otpCode}

            Mã có hiệu lực trong {lifetimeMinutes} phút và chỉ sử dụng được một lần.
            Không chia sẻ mã này với bất kỳ ai.

            Nếu bạn không thực hiện yêu cầu này, vui lòng liên hệ Quản trị viên hệ thống.

            Riverside Palace Convention Center
            """,
            cancellationToken);

    public Task SendPasswordResetOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            "Riverside Palace · Mã OTP đặt lại mật khẩu",
            $"""
            Xin chào nhân viên Riverside Palace,

            Mã OTP đặt lại mật khẩu của bạn là: {otpCode}

            Mã có hiệu lực trong {lifetimeMinutes} phút và chỉ sử dụng được một lần.
            Không chia sẻ mã này với bất kỳ ai.

            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và liên hệ Quản trị viên hệ thống.

            Riverside Palace Convention Center
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

                Riverside Palace Convention Center
                """;
        await SendAsync(recipientEmail, "Riverside Palace · Yêu cầu đặt lại mật khẩu", body, cancellationToken);
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

        var subject = $"Riverside Palace - Xác nhận giữ chỗ tiệc cưới #{bookingCode}";
        var effectiveBaseUrl = !string.IsNullOrWhiteSpace(_mail.PublicSiteUrl) ? _mail.PublicSiteUrl : _mail.ClientAppBaseUrl;
        var (html, plain) = BuildBookingConfirmationContent(
            customerName, bookingCode, eventDate, shift, hallName,
            totalTables, guestCount, menuName, decorName, totalEstimatedCost, holdExpiresAt,
            effectiveBaseUrl, phoneNumber);

        return SendMultipartAsync(recipientEmail, subject, plain, html, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendDepositPaymentSuccessEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        long orderCode,
        decimal depositAmount,
        decimal remainingAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        string? phoneNumber = null,
        byte[]? invoicePdfBytes = null,
        string? invoiceFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"Riverside Palace - Xác nhận đặt cọc thành công hồ sơ #{bookingCode}";
        var effectiveBaseUrl = !string.IsNullOrWhiteSpace(_mail.PublicSiteUrl) ? _mail.PublicSiteUrl : _mail.ClientAppBaseUrl;
        var (html, plain) = BuildDepositSuccessContent(
            customerName, bookingCode, orderCode, depositAmount, remainingAmount,
            eventDate, shift, hallName, effectiveBaseUrl, phoneNumber);

        var attachmentName = invoiceFileName ?? $"BienLaiDatCoc_{bookingCode}.pdf";
        return SendMultipartAsync(recipientEmail, subject, plain, html, invoicePdfBytes, attachmentName, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendFullPaymentSuccessEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        string contractCode,
        decimal totalAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        string? phoneNumber = null,
        byte[]? invoicePdfBytes = null,
        string? invoiceFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"Riverside Palace - Xác nhận hoàn tất thanh toán tiệc cưới #{bookingCode}";
        var effectiveBaseUrl = !string.IsNullOrWhiteSpace(_mail.PublicSiteUrl) ? _mail.PublicSiteUrl : _mail.ClientAppBaseUrl;
        var (html, plain) = BuildFullPaymentSuccessContent(
            customerName, bookingCode, contractCode, totalAmount,
            eventDate, shift, hallName, effectiveBaseUrl, phoneNumber);

        var attachmentName = invoiceFileName ?? $"HoaDonDienTu_{bookingCode}.pdf";
        return SendMultipartAsync(recipientEmail, subject, plain, html, invoicePdfBytes, attachmentName, cancellationToken);
    }

    public static (string Html, string Plain) BuildBookingConfirmationContent(
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
        var depositEstStr = Math.Round(totalEstimatedCost * 0.20m, 0).ToString("N0", vi) + " VNĐ";
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var scaleStr = $"{guestCount} khách ({totalTables} bàn)";
        var holdStr = holdExpiresAt.ToString("HH:mm - dd/MM/yyyy", vi);
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');

        // Link tra cứu thông thường
        var lookupUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}";

        // Deep-link thanh toán đặt cọc trực tuyến có autoDeposit=true
        var depositUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&autoDeposit=true"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}&autoDeposit=true";

        var html = $"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Xác nhận giữ chỗ {E(bookingCode)} - Riverside Palace</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:'Plus Jakarta Sans',Arial,Helvetica,sans-serif;color:#111822;">
        <!-- Preheader preview text for inbox (RFC-compliant, no tiny font or zero opacity) -->
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;visibility:hidden;color:transparent;line-height:0;">
          Xác nhận đăng ký giữ chỗ tiệc cưới #{E(bookingCode)} tại Riverside Palace.
        </div>
        <div style="max-width:620px;margin:0 auto;padding:24px 16px;">
          <!-- Header -->
          <div style="background:linear-gradient(135deg, #1A222D 0%, #111822 100%);color:#fff;text-align:center;padding:30px 20px;border-radius:16px 16px 0 0;border-bottom:3px solid #B89758;">
            <div style="letter-spacing:4px;font-size:12px;font-weight:700;color:#B89758;text-transform:uppercase;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;font-weight:700;">Xác Nhận Giữ Chỗ Tiệc Cưới</h1>
            <p style="margin:0;color:#cbd5e1;font-size:12.5px;">Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace ven sông Bến Vân Đồn</p>
          </div>
          <!-- Body -->
          <div style="background:#ffffff;border:1px solid #E9E2D3;border-top:none;border-radius:0 0 16px 16px;padding:26px;box-shadow:0 8px 25px rgba(0,0,0,0.04);">
            <p style="font-size:14.5px;margin-top:0;">Kính gửi Quý khách <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:13.5px;line-height:1.6;color:#475569;">Cảm ơn hai bạn đã lựa chọn Riverside Palace là nơi lưu giữ khoảnh khắc trăm năm. Hồ sơ đăng ký giữ chỗ tiệc cưới của bạn đã được ghi nhận trên hệ thống với thông tin chi tiết:</p>

            <!-- Booking Code Box -->
            <div style="border:2px dashed #B89758;border-radius:12px;padding:16px;text-align:center;background:#FDFBF7;margin:18px 0;">
              <div style="font-size:11.5px;font-weight:700;letter-spacing:1px;color:#64748B;">MÃ HỒ SƠ GIỮ CHỖ CỦA BẠN</div>
              <div style="font-size:26px;font-weight:800;letter-spacing:2px;color:#111822;margin:4px 0;">{E(bookingCode)}</div>
              <div style="font-size:12px;color:#64748B;">Tiền cọc giữ chỗ (20%): <strong style="color:#DC2626;font-size:14px;">{depositEstStr}</strong></div>
            </div>

            <!-- Details Table -->
            <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:18px;">
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;width:38%;">Đại sảnh tiệc</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Thời gian tổ chức</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{eventStr}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Quy mô tiệc</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{E(scaleStr)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Gói thực đơn</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#111822;">{E(menuName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Gói trang trí</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#111822;">{E(decorName)}</td></tr>
              <tr><td style="padding:10px 6px;color:#64748B;">Tổng dự toán ước tính</td><td style="padding:10px 6px;font-weight:800;color:#B89758;font-size:16px;">{E(totalStr)}</td></tr>
            </table>

            <!-- Notice Box -->
            <div style="background:#FFFBEB;border:1px solid #FDE68A;border-left:4px solid #D97706;padding:12px 14px;border-radius:8px;margin:18px 0;font-size:12.5px;line-height:1.6;color:#92400E;">
              <strong>Thời hạn giữ chỗ 48 giờ (BR07):</strong> Hồ sơ được ưu tiên giữ đến <strong>{E(holdStr)}</strong>.<br/>
              Sau thời gian này, nếu chưa hoàn tất thanh toán đặt cọc 20%, lịch sảnh sẽ tự động giải phóng để phục vụ các cặp đôi khác.
            </div>

            <!-- Call-To-Action Buttons -->
            <div style="text-align:center;margin:24px 0 16px;">
              <a href="{E(depositUrl)}" style="display:inline-block;background:#B89758;color:#ffffff;text-decoration:none;padding:14px 30px;border-radius:10px;font-weight:800;font-size:15px;box-shadow:0 4px 14px rgba(184,151,88,0.35);">
                💳 Thanh toán đặt cọc VietQR ngay (20%)
              </a>
              <div style="margin-top:12px;">
                <a href="{E(lookupUrl)}" style="display:inline-block;background:#ffffff;border:1.5px solid #CBD5E1;color:#111822;text-decoration:none;padding:9px 22px;border-radius:8px;font-weight:700;font-size:13px;">
                  📄 Tra cứu hồ sơ &amp; chi tiết thực đơn
                </a>
              </div>
            </div>

            <!-- Footer Contact -->
            <hr style="border:none;border-top:1px solid #F1ECE1;margin:22px 0 16px;"/>
            <p style="font-size:11.5px;color:#64748B;line-height:1.7;margin:0;">
              <strong>Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</strong><br/>
              Địa chỉ: 360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline tư vấn: <strong>0933 777 888</strong> · (028) 6256 8888 — Email: event@riversidepalace.vn<br/>
              Website: <a href="{baseUrl}" style="color:#B89758;text-decoration:none;">{baseUrl}</a>
            </p>
          </div>
        </div>
        </body></html>
        """;

        var plain = $"""
        RIVERSIDE PALACE - XÁC NHẬN GIỮ CHỖ TIỆC CƯỚI
        =====================================================
        Kính gửi Quý khách: {customerName}
        Mã hồ sơ giữ chỗ: {bookingCode}

        THÔNG TIN TIỆC CƯỚI:
        - Đại sảnh: {hallName}
        - Thời gian: {eventDate:dd/MM/yyyy} ({shift})
        - Quy mô: {guestCount} khách ({totalTables} bàn)
        - Thực đơn: {menuName ?? "—"}
        - Trang trí: {decorName ?? "—"}
        - Tổng dự toán: {totalStr}
        - Tiền cọc giữ chỗ (20%): {depositEstStr}

        LƯU Ý GIỮ CHỖ 48 GIỜ:
        Hồ sơ được ưu tiên giữ đến {holdStr}.
        Sau thời gian này, nếu chưa thanh toán cọc 20%, lịch sảnh sẽ tự động giải phóng.

        ĐẶT CỌC TRỰC TUYẾN NGAY QUA VIETQR:
        {depositUrl}

        TRA CỨU HỒ SƠ CHI TIẾT:
        {lookupUrl}

        -----------------------------------------------------
        Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace
        Địa chỉ: 360D Bến Vân Đồn, P.1, Q.4, TP.HCM
        Hotline: 0933 777 888 - (028) 6256 8888
        Email: event@riversidepalace.vn
        """;

        return (html, plain);
    }

    public static (string Html, string Plain) BuildDepositSuccessContent(
        string customerName,
        string bookingCode,
        long orderCode,
        decimal depositAmount,
        decimal remainingAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        string? clientAppBaseUrl = null,
        string? phoneNumber = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var depositStr = depositAmount.ToString("N0", vi) + " VNĐ";
        var remainStr = remainingAmount.ToString("N0", vi) + " VNĐ";
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}";

        var html = $"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Xác nhận đặt cọc thành công #{E(bookingCode)} - Riverside Palace</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:'Plus Jakarta Sans',Arial,Helvetica,sans-serif;color:#111822;">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;visibility:hidden;color:transparent;line-height:0;">
          Xác nhận thanh toán đặt cọc 20% thành công cho hồ sơ #{E(bookingCode)} tại Riverside Palace.
        </div>
        <div style="max-width:620px;margin:0 auto;padding:24px 16px;">
          <div style="background:linear-gradient(135deg, #065F46 0%, #047857 100%);color:#fff;text-align:center;padding:30px 20px;border-radius:16px 16px 0 0;border-bottom:3px solid #10B981;">
            <div style="letter-spacing:4px;font-size:12px;font-weight:700;color:#A7F3D0;text-transform:uppercase;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;font-weight:700;">✓ Xác Nhận Đặt Cọc Thành Công</h1>
            <p style="margin:0;color:#D1FAE5;font-size:12.5px;">Lịch sảnh tiệc của bạn đã được khóa chính thức trên hệ thống</p>
          </div>
          <div style="background:#ffffff;border:1px solid #E9E2D3;border-top:none;border-radius:0 0 16px 16px;padding:26px;box-shadow:0 8px 25px rgba(0,0,0,0.04);">
            <p style="font-size:14.5px;margin-top:0;">Kính gửi Quý khách <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:13.5px;line-height:1.6;color:#475569;">Hệ thống thanh toán điện tử Riverside Palace trân trọng thông báo giao dịch đặt cọc giữ chỗ của Quý khách đã được xác nhận thành công qua cổng thanh toán VietQR PayOS.</p>

            <!-- Success Box -->
            <div style="background:#ECFDF5;border:1.5px solid #A7F3D0;border-radius:12px;padding:18px;text-align:center;margin:18px 0;">
              <div style="font-size:12px;font-weight:700;color:#065F46;">SỐ TIỀN ĐẶT CỌC ĐÃ THANH TOÁN (20%)</div>
              <div style="font-size:28px;font-weight:800;color:#047857;margin:4px 0;">{depositStr}</div>
              <div style="font-size:12px;color:#065F46;">Mã giao dịch VietQR: <strong>#{orderCode}</strong> · Mã hồ sơ: <strong>{E(bookingCode)}</strong></div>
            </div>

            <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:18px;">
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;width:40%;">Trạng thái hồ sơ</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:800;color:#047857;">ĐÃ XÁC NHẬN (CONFIRMED)</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Sảnh tiệc đã khóa</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Thời gian tổ chức</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{eventStr}</td></tr>
              <tr><td style="padding:10px 6px;color:#64748B;">Dự toán còn lại cần thanh toán</td><td style="padding:10px 6px;font-weight:800;color:#B89758;font-size:15px;">{remainStr}</td></tr>
            </table>

            <div style="background:#F0FDF4;border-left:4px solid #10B981;padding:12px 14px;border-radius:6px;font-size:12.5px;line-height:1.6;color:#166534;margin:18px 0;">
              <strong>Biên lai &amp; Hóa đơn điện tử có chữ ký số:</strong> File PDF biên lai thu tiền đã được ký số điện tử hợp chuẩn (PAdES) và đính kèm trong email này để Quý khách lưu trữ phục vụ đối soát.
            </div>

            <div style="text-align:center;margin:22px 0 16px;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:700;font-size:14px;box-shadow:0 4px 12px rgba(184,151,88,0.3);">
                📄 Xem chi tiết hồ sơ &amp; Hợp đồng trực tuyến
              </a>
            </div>

            <hr style="border:none;border-top:1px solid #F1ECE1;margin:22px 0 16px;"/>
            <p style="font-size:11.5px;color:#64748B;line-height:1.7;margin:0;">
              <strong>Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</strong><br/>
              Địa chỉ: 360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline hỗ trợ tiệc cưới: <strong>0933 777 888</strong> · (028) 6256 8888
            </p>
          </div>
        </div>
        </body></html>
        """;

        var plain = $"""
        RIVERSIDE PALACE - XÁC NHẬN ĐẶT CỌC THÀNH CÔNG
        =====================================================
        Kính gửi Quý khách: {customerName}
        Mã hồ sơ: {bookingCode}
        Mã giao dịch VietQR: #{orderCode}

        SỐ TIỀN ĐẶT CỌC ĐÃ THANH TOÁN: {depositStr}
        DỰ TOÁN CÒN LẠI CẦN THANH TOÁN: {remainStr}

        THÔNG TIN SẢNH TIỆC:
        - Đại sảnh: {hallName} (Đã khóa chính thức)
        - Thời gian: {eventDate:dd/MM/yyyy} ({shift})
        - Trạng thái: ĐÃ XÁC NHẬN (CONFIRMED)

        Hóa đơn/biên lai điện tử có chữ ký số (PAdES) được đính kèm trong email này.

        XEM HỒ SƠ & HỢP ĐỒNG TRỰC TUYẾN:
        {lookupUrl}

        -----------------------------------------------------
        Riverside Palace Convention Center
        Hotline: 0933 777 888 - (028) 6256 8888
        Email: event@riversidepalace.vn
        """;

        return (html, plain);
    }

    public static (string Html, string Plain) BuildFullPaymentSuccessContent(
        string customerName,
        string bookingCode,
        string contractCode,
        decimal totalAmount,
        DateTime eventDate,
        string shift,
        string hallName,
        string? clientAppBaseUrl = null,
        string? phoneNumber = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var totalStr = totalAmount.ToString("N0", vi) + " VNĐ";
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}";

        var html = $"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Xác nhận hoàn tất thanh toán #{E(bookingCode)} - Riverside Palace</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:'Plus Jakarta Sans',Arial,Helvetica,sans-serif;color:#111822;">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;visibility:hidden;color:transparent;line-height:0;">
          Xác nhận hoàn tất quyết toán 100% hợp đồng tiệc cưới #{E(bookingCode)} tại Riverside Palace.
        </div>
        <div style="max-width:620px;margin:0 auto;padding:24px 16px;">
          <div style="background:linear-gradient(135deg, #1E3A8A 0%, #172554 100%);color:#fff;text-align:center;padding:30px 20px;border-radius:16px 16px 0 0;border-bottom:3px solid #3B82F6;">
            <div style="letter-spacing:4px;font-size:12px;font-weight:700;color:#93C5FD;text-transform:uppercase;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;font-weight:700;">✓ Hoàn Tất 100% Thanh Toán</h1>
            <p style="margin:0;color:#BFDBFE;font-size:12.5px;">Hợp đồng tiệc cưới đã được quyết toán đầy đủ</p>
          </div>
          <div style="background:#ffffff;border:1px solid #E9E2D3;border-top:none;border-radius:0 0 16px 16px;padding:26px;box-shadow:0 8px 25px rgba(0,0,0,0.04);">
            <p style="font-size:14.5px;margin-top:0;">Kính gửi Quý khách <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:13.5px;line-height:1.6;color:#475569;">Riverside Palace trân trọng cảm ơn Quý khách. Hợp đồng tổ chức tiệc cưới của bạn đã hoàn tất quyết toán 100%.</p>

            <div style="background:#EFF6FF;border:1.5px solid #BFDBFE;border-radius:12px;padding:18px;text-align:center;margin:18px 0;">
              <div style="font-size:12px;font-weight:700;color:#1E40AF;">TỔNG GIÁ TRỊ HỢP ĐỒNG ĐÃ QUYẾT TOÁN</div>
              <div style="font-size:28px;font-weight:800;color:#1D4ED8;margin:4px 0;">{totalStr}</div>
              <div style="font-size:12px;color:#1E40AF;">Mã hợp đồng: <strong>{E(contractCode)}</strong> · Số dư còn lại: <strong>0 VNĐ</strong></div>
            </div>

            <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:18px;">
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;width:40%;">Đại sảnh tiệc</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Thời gian tổ chức</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;">{eventStr}</td></tr>
              <tr><td style="padding:10px 6px;color:#64748B;">Tình trạng công nợ</td><td style="padding:10px 6px;font-weight:800;color:#047857;">HOÀN TẤT (100%)</td></tr>
            </table>

            <div style="background:#F0FDF4;border-left:4px solid #10B981;padding:12px 14px;border-radius:6px;font-size:12.5px;line-height:1.6;color:#166534;margin:18px 0;">
              <strong>Hóa đơn điện tử có chữ ký số:</strong> Hóa đơn điện tử chính thức đã được ký số PAdES bởi Riverside Palace và đính kèm trong email này.
            </div>

            <div style="text-align:center;margin:22px 0 16px;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:700;font-size:14px;box-shadow:0 4px 12px rgba(184,151,88,0.3);">
                📄 Xem hồ sơ tiệc &amp; Hóa đơn trực tuyến
              </a>
            </div>

            <hr style="border:none;border-top:1px solid #F1ECE1;margin:22px 0 16px;"/>
            <p style="font-size:11.5px;color:#64748B;line-height:1.7;margin:0;">
              <strong>Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</strong><br/>
              Địa chỉ: 360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline: <strong>0933 777 888</strong> · (028) 6256 8888
            </p>
          </div>
        </div>
        </body></html>
        """;

        var plain = $"""
        RIVERSIDE PALACE - XÁC NHẬN HOÀN TẤT THANH TOÁN
        =====================================================
        Kính gửi Quý khách: {customerName}
        Mã hồ sơ: {bookingCode}
        Mã hợp đồng: {contractCode}

        TỔNG GIÁ TRỊ ĐÃ QUYẾT TOÁN: {totalStr}
        SỐ DƯ CÒN LẠI: 0 VNĐ (Đã thanh toán 100%)

        THÔNG TIN TIỆC:
        - Đại sảnh: {hallName}
        - Thời gian: {eventDate:dd/MM/yyyy} ({shift})

        Hóa đơn điện tử có chữ ký số PAdES được đính kèm trong email này.

        TRA CỨU HỒ SƠ:
        {lookupUrl}

        -----------------------------------------------------
        Riverside Palace Convention Center
        Hotline: 0933 777 888 - (028) 6256 8888
        Email: event@riversidepalace.vn
        """;

        return (html, plain);
    }

    private (string Host, int Port, bool EnableSsl, string SenderEmail, string SenderName, ICredentialsByHost Credentials) ResolveSmtp()
    {
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

        var fromAddress = new MailAddress(cfg.SenderEmail, cfg.SenderName, Encoding.UTF8);
        using var message = new MailMessage
        {
            From = fromAddress,
            Sender = fromAddress,
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            Priority = MailPriority.Normal
        };
        message.ReplyToList.Add(fromAddress);
        message.To.Add(new MailAddress(recipientEmail));

        // RFC 3834 & Anti-spam standards
        message.Headers.Add("Auto-Submitted", "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");

        var domain = cfg.SenderEmail.Contains('@') ? cfg.SenderEmail.Split('@')[1] : "gmail.com";
        message.Headers.Add("Message-ID", $"<{Guid.NewGuid():N}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}@{domain}>");
        message.Headers.Add("Date", DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture));

        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = cfg.Credentials,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task SendMultipartAsync(
        string recipientEmail,
        string subject,
        string plainTextBody,
        string htmlBody,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null,
        CancellationToken cancellationToken = default)
    {
        var cfg = ResolveSmtp();
        if (string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.SenderEmail))
            throw new InvalidOperationException("SMTP chưa được cấu hình (MailSettings/Smtp). Hãy điền SenderEmail + App Password.");

        var fromAddress = new MailAddress(cfg.SenderEmail, cfg.SenderName, Encoding.UTF8);
        using var message = new MailMessage
        {
            From = fromAddress,
            Sender = fromAddress,
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            Priority = MailPriority.Normal
        };
        message.ReplyToList.Add(fromAddress);
        message.To.Add(new MailAddress(recipientEmail));

        // Chuẩn MIME multipart/alternative: Chứa đồng thời text/plain và text/html
        var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, "text/plain");
        plainView.ContentType.CharSet = "utf-8";
        var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
        htmlView.ContentType.CharSet = "utf-8";
        message.AlternateViews.Add(plainView);
        message.AlternateViews.Add(htmlView);

        // RFC 3834 & Anti-spam standards
        message.Headers.Add("Auto-Submitted", "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");

        var domain = cfg.SenderEmail.Contains('@') ? cfg.SenderEmail.Split('@')[1] : "gmail.com";
        message.Headers.Add("Message-ID", $"<{Guid.NewGuid():N}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}@{domain}>");
        message.Headers.Add("Date", DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture));

        // Đính kèm file PDF nếu có (hóa đơn ký số / biên lai)
        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            var ms = new MemoryStream(attachmentBytes);
            var attachment = new Attachment(ms, attachmentFileName ?? "HoaDonDienTu_RiversidePalace.pdf", "application/pdf");
            message.Attachments.Add(attachment);
        }

        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = cfg.Credentials,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20000
        };

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Gửi email (Multipart/Alternative) tới {Email}, subject={Subject}", recipientEmail, subject);
        await client.SendMailAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendContractPaymentRequestEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        string contractCode,
        string paymentType,
        decimal amount,
        string checkoutUrl,
        DateTime? paymentDeadline,
        DateTime eventDate,
        string shift,
        string hallName,
        string? phoneNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"Riverside Palace - Thông báo thanh toán {paymentType} #{contractCode}";
        var effectiveBaseUrl = !string.IsNullOrWhiteSpace(_mail.PublicSiteUrl) ? _mail.PublicSiteUrl : _mail.ClientAppBaseUrl;
        var (html, plain) = BuildContractPaymentRequestContent(
            customerName, bookingCode, contractCode, paymentType, amount, checkoutUrl, paymentDeadline,
            eventDate, shift, hallName, effectiveBaseUrl, phoneNumber);

        return SendMultipartAsync(recipientEmail, subject, plain, html, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendContractPaymentSuccessEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        string contractCode,
        string paymentType,
        decimal amountPaid,
        decimal remainingDebt,
        DateTime eventDate,
        string shift,
        string hallName,
        string? phoneNumber = null,
        byte[]? invoicePdfBytes = null,
        string? invoiceFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Email người nhận không được để trống.", nameof(recipientEmail));

        var subject = $"Riverside Palace - Xác nhận thanh toán {paymentType} thành công #{contractCode}";
        var effectiveBaseUrl = !string.IsNullOrWhiteSpace(_mail.PublicSiteUrl) ? _mail.PublicSiteUrl : _mail.ClientAppBaseUrl;
        var (html, plain) = BuildContractPaymentSuccessContent(
            customerName, bookingCode, contractCode, paymentType, amountPaid, remainingDebt,
            eventDate, shift, hallName, effectiveBaseUrl, phoneNumber);

        var attachmentName = invoiceFileName ?? $"HoaDonDienTu_{bookingCode}_Dot2.pdf";
        return SendMultipartAsync(recipientEmail, subject, plain, html, invoicePdfBytes, attachmentName, cancellationToken);
    }

    public static (string Html, string Plain) BuildContractPaymentRequestContent(
        string customerName,
        string bookingCode,
        string contractCode,
        string paymentType,
        decimal amount,
        string checkoutUrl,
        DateTime? paymentDeadline,
        DateTime eventDate,
        string shift,
        string hallName,
        string? clientAppBaseUrl = null,
        string? phoneNumber = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var amountStr = amount.ToString("N0", vi) + " VNĐ";
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var deadlineStr = paymentDeadline.HasValue ? paymentDeadline.Value.ToString("dd/MM/yyyy", vi) : "Trước ngày tiệc 7 ngày";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}";

        var html = $"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Thông báo thanh toán {E(paymentType)} #{E(contractCode)} - Riverside Palace</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:'Plus Jakarta Sans',Arial,Helvetica,sans-serif;color:#111822;">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;visibility:hidden;color:transparent;line-height:0;">
          Thông báo thanh toán {E(paymentType)} hợp đồng tiệc cưới #{E(contractCode)} tại Riverside Palace.
        </div>
        <div style="max-width:620px;margin:0 auto;padding:24px 16px;">
          <div style="background:linear-gradient(135deg, #1A222D 0%, #111822 100%);color:#fff;text-align:center;padding:30px 20px;border-radius:16px 16px 0 0;border-bottom:3px solid #B89758;">
            <div style="letter-spacing:4px;font-size:12px;font-weight:700;color:#B89758;text-transform:uppercase;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;font-weight:700;">Thông Báo Thanh Toán Hợp Đồng</h1>
            <p style="margin:0;color:#cbd5e1;font-size:12.5px;">Tiến độ thanh toán: {E(paymentType)}</p>
          </div>
          <div style="background:#ffffff;border:1px solid #E9E2D3;border-top:none;border-radius:0 0 16px 16px;padding:26px;box-shadow:0 8px 25px rgba(0,0,0,0.04);">
            <p style="font-size:14.5px;margin-top:0;">Kính gửi Quý khách <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:13.5px;line-height:1.6;color:#475569;">Để chuẩn bị chu đáo nhất cho ngày trọng đại của hai bạn theo đúng hợp đồng tiệc cưới, Riverside Palace trân trọng gửi thông tin thanh toán từ xa:</p>

            <div style="border:2px dashed #B89758;border-radius:12px;padding:18px;text-align:center;background:#FDFBF7;margin:18px 0;">
              <div style="font-size:12px;font-weight:700;letter-spacing:1px;color:#64748B;">SỐ TIỀN CẦN THANH TOÁN ({E(paymentType).ToUpperInvariant()})</div>
              <div style="font-size:28px;font-weight:800;color:#DC2626;margin:4px 0;">{amountStr}</div>
              <div style="font-size:12px;color:#64748B;">Mã hợp đồng: <strong>{E(contractCode)}</strong> · Mã tiệc: <strong>{E(bookingCode)}</strong></div>
            </div>

            <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:18px;">
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;width:40%;">Đại sảnh tiệc</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Thời gian tổ chức</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{eventStr}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Hạn hoàn tất thanh toán</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#DC2626;">{E(deadlineStr)}</td></tr>
            </table>

            <div style="background:#FFFBEB;border-left:4px solid #D97706;padding:12px 14px;border-radius:6px;font-size:12.5px;line-height:1.6;color:#92400E;margin:18px 0;">
              <strong>Thanh toán trực tuyến từ xa:</strong> Quý khách không cần đến trực tiếp quầy thu ngân. Vui lòng bấm nút <strong>"Thanh toán ngay qua PayOS"</strong> bên dưới để mở mã VietQR và quét chuyển khoản từ ứng dụng ngân hàng bất kỳ.
            </div>

            <div style="text-align:center;margin:24px 0 16px;">
              <a href="{E(checkoutUrl)}" style="display:inline-block;background:#B89758;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:10px;font-weight:800;font-size:15px;box-shadow:0 4px 14px rgba(184,151,88,0.35);">
                💳 Thanh toán trực tuyến ngay qua PayOS
              </a>
              <div style="margin-top:12px;">
                <a href="{E(lookupUrl)}" style="display:inline-block;background:#ffffff;border:1.5px solid #CBD5E1;color:#111822;text-decoration:none;padding:9px 22px;border-radius:8px;font-weight:700;font-size:13px;">
                  📄 Tra cứu hồ sơ &amp; Hợp đồng trực tuyến
                </a>
              </div>
            </div>

            <hr style="border:none;border-top:1px solid #F1ECE1;margin:22px 0 16px;"/>
            <p style="font-size:11.5px;color:#64748B;line-height:1.7;margin:0;">
              <strong>Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</strong><br/>
              Địa chỉ: 360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline: <strong>0933 777 888</strong> · (028) 6256 8888
            </p>
          </div>
        </div>
        </body></html>
        """;

        var plain = $"""
        RIVERSIDE PALACE - THÔNG BÁO THANH TOÁN {paymentType.ToUpperInvariant()}
        =====================================================
        Kính gửi Quý khách: {customerName}
        Mã hợp đồng: {contractCode}
        Mã hồ sơ: {bookingCode}

        SỐ TIỀN CẦN THANH TOÁN: {amountStr}
        HẠN THANH TOÁN: {deadlineStr}

        THÔNG TIN TIỆC:
        - Đại sảnh: {hallName}
        - Thời gian: {eventDate:dd/MM/yyyy} ({shift})

        LIÊN KẾT THANH TOÁN VIETQR PAYOS TỪ XA:
        {checkoutUrl}

        TRA CỨU HỒ SƠ TIỆC CƯỚI:
        {lookupUrl}

        -----------------------------------------------------
        Riverside Palace Convention Center
        Hotline: 0933 777 888 - (028) 6256 8888
        """;

        return (html, plain);
    }

    public static (string Html, string Plain) BuildContractPaymentSuccessContent(
        string customerName,
        string bookingCode,
        string contractCode,
        string paymentType,
        decimal amountPaid,
        decimal remainingDebt,
        DateTime eventDate,
        string shift,
        string hallName,
        string? clientAppBaseUrl = null,
        string? phoneNumber = null)
    {
        var vi = new CultureInfo("vi-VN");
        string E(string? v) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v.Trim());
        var amountStr = amountPaid.ToString("N0", vi) + " VNĐ";
        var remainStr = remainingDebt.ToString("N0", vi) + " VNĐ";
        var eventStr = $"{eventDate:dd/MM/yyyy} · {E(shift)}";
        var baseUrl = string.IsNullOrWhiteSpace(clientAppBaseUrl) ? "https://localhost:7102" : clientAppBaseUrl.TrimEnd('/');
        var lookupUrl = string.IsNullOrWhiteSpace(phoneNumber)
            ? $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}"
            : $"{baseUrl}/MyBooking?bookingCode={Uri.EscapeDataString(bookingCode)}&phone={Uri.EscapeDataString(phoneNumber.Trim())}";

        var html = $"""
        <!DOCTYPE html>
        <html lang="vi"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>Xác nhận thanh toán {E(paymentType)} #{E(contractCode)} - Riverside Palace</title></head>
        <body style="margin:0;padding:0;background:#FDFBF7;font-family:'Plus Jakarta Sans',Arial,Helvetica,sans-serif;color:#111822;">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;visibility:hidden;color:transparent;line-height:0;">
          Xác nhận thanh toán {E(paymentType)} thành công cho hợp đồng #{E(contractCode)} tại Riverside Palace.
        </div>
        <div style="max-width:620px;margin:0 auto;padding:24px 16px;">
          <div style="background:linear-gradient(135deg, #065F46 0%, #047857 100%);color:#fff;text-align:center;padding:30px 20px;border-radius:16px 16px 0 0;border-bottom:3px solid #10B981;">
            <div style="letter-spacing:4px;font-size:12px;font-weight:700;color:#A7F3D0;text-transform:uppercase;">RIVERSIDE PALACE</div>
            <h1 style="margin:8px 0 4px;font-size:22px;font-weight:700;">✓ Xác Nhận Thanh Toán Thành Công</h1>
            <p style="margin:0;color:#D1FAE5;font-size:12.5px;">Khoản thanh toán: {E(paymentType)}</p>
          </div>
          <div style="background:#ffffff;border:1px solid #E9E2D3;border-top:none;border-radius:0 0 16px 16px;padding:26px;box-shadow:0 8px 25px rgba(0,0,0,0.04);">
            <p style="font-size:14.5px;margin-top:0;">Kính gửi Quý khách <strong>{E(customerName)}</strong>,</p>
            <p style="font-size:13.5px;line-height:1.6;color:#475569;">Hệ thống thanh toán điện tử Riverside Palace trân trọng xác nhận đã nhận thanh toán thành công qua cổng thanh toán VietQR PayOS.</p>

            <div style="background:#ECFDF5;border:1.5px solid #A7F3D0;border-radius:12px;padding:18px;text-align:center;margin:18px 0;">
              <div style="font-size:12px;font-weight:700;color:#065F46;">SỐ TIỀN ĐÃ THANH TOÁN</div>
              <div style="font-size:28px;font-weight:800;color:#047857;margin:4px 0;">{amountStr}</div>
              <div style="font-size:12px;color:#065F46;">Mã hợp đồng: <strong>{E(contractCode)}</strong> · Mã tiệc: <strong>{E(bookingCode)}</strong></div>
            </div>

            <table style="width:100%;border-collapse:collapse;font-size:13.5px;margin-bottom:18px;">
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;width:40%;">Khoản thanh toán</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#047857;">{E(paymentType)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Đại sảnh tiệc</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{E(hallName)}</td></tr>
              <tr><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;color:#64748B;">Thời gian tổ chức</td><td style="padding:10px 6px;border-bottom:1px solid #F1ECE1;font-weight:700;color:#111822;">{eventStr}</td></tr>
              <tr><td style="padding:10px 6px;color:#64748B;">Số dư công nợ còn lại</td><td style="padding:10px 6px;font-weight:800;color:#B89758;font-size:15px;">{remainStr}</td></tr>
            </table>

            <div style="background:#F0FDF4;border-left:4px solid #10B981;padding:12px 14px;border-radius:6px;font-size:12.5px;line-height:1.6;color:#166534;margin:18px 0;">
              <strong>Hóa đơn điện tử có chữ ký số (PAdES):</strong> Hóa đơn điện tử giá trị gia tăng hợp chuẩn đã được ký số bởi Riverside Palace và đính kèm trong email này để Quý khách lưu trữ phục vụ đối soát pháp lý.
            </div>

            <div style="text-align:center;margin:22px 0 16px;">
              <a href="{E(lookupUrl)}" style="display:inline-block;background:#B89758;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:700;font-size:14px;box-shadow:0 4px 12px rgba(184,151,88,0.3);">
                📄 Xem hồ sơ tiệc &amp; Hóa đơn trực tuyến
              </a>
            </div>

            <hr style="border:none;border-top:1px solid #F1ECE1;margin:22px 0 16px;"/>
            <p style="font-size:11.5px;color:#64748B;line-height:1.7;margin:0;">
              <strong>Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace</strong><br/>
              Địa chỉ: 360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh<br/>
              Hotline: <strong>0933 777 888</strong> · (028) 6256 8888
            </p>
          </div>
        </div>
        </body></html>
        """;

        var plain = $"""
        RIVERSIDE PALACE - XÁC NHẬN THANH TOÁN {paymentType.ToUpperInvariant()} THÀNH CÔNG
        =====================================================
        Kính gửi Quý khách: {customerName}
        Mã hợp đồng: {contractCode}
        Mã hồ sơ: {bookingCode}

        SỐ TIỀN ĐÃ THANH TOÁN: {amountStr}
        SỐ DƯ CÔNG NỢ CÒN LẠI: {remainStr}

        THÔNG TIN TIỆC:
        - Đại sảnh: {hallName}
        - Thời gian: {eventDate:dd/MM/yyyy} ({shift})

        Hóa đơn điện tử có chữ ký số điện tử PAdES được đính kèm trong email này.

        TRA CỨU HỒ SƠ:
        {lookupUrl}

        -----------------------------------------------------
        Riverside Palace Convention Center
        Hotline: 0933 777 888 - (028) 6256 8888
        """;

        return (html, plain);
    }
}

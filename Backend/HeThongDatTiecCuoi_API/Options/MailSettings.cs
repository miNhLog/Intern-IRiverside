namespace HeThongDatTiecCuoi_API.Options;

/// <summary>
/// FR15 / UC13: Cấu hình SMTP gửi email xác nhận giữ chỗ (bind section "MailSettings").
/// Giữ song song với <see cref="SmtpOptions"/> (section "Smtp" cũ) để tương thích OTP/Reset.
/// Ưu tiên đọc MailSettings, fallback sang Smtp khi MailSettings chưa điền.
/// </summary>
public sealed class MailSettings
{
    public const string SectionName = "MailSettings";

    public string Server { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderName { get; set; } = "Trung Tâm Hội Nghị Tiệc Cưới Riverside Palace";
    public string SenderEmail { get; set; } = "riversidepalace.contact@gmail.com";
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    /// <summary>URL gốc của Frontend để tạo link tra cứu MyBooking. Nạp từ config hoặc biến môi trường.</summary>
    public string? ClientAppBaseUrl { get; set; } = "https://localhost:7102";
}

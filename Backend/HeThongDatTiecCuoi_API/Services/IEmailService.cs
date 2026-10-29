namespace HeThongDatTiecCuoi_API.Services;

public interface IEmailService
{
    Task SendPasswordResetOtpEmailAsync(
        string recipientEmail,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// FR15 / UC13: Gửi email xác nhận đăng ký giữ chỗ tiệc cưới (HTML brand Riverside Palace).
    /// Fault-tolerant: caller phải try-catch để lỗi SMTP không rollback booking.
    /// </summary>
    Task SendBookingConfirmationAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FR15 / UC16: Gửi email cho khách khi hợp đồng đổi trạng thái (kích hoạt/hoàn tất/hủy).
    /// Fault-tolerant: caller phải try-catch sau khi transaction đã commit.
    /// </summary>
    Task SendContractStatusChangedAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC15 / FR17: Gửi email xác nhận khách đã hủy giữ chỗ trực tuyến.
    /// Fault-tolerant: caller phải try-catch sau khi transaction đã commit.
    /// </summary>
    Task SendBookingCancelledAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        string cancellationReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC15 / FR17: Gửi email xác nhận đổi ngày/ca (giữ nguyên sảnh).
    /// Fault-tolerant: caller phải try-catch sau khi transaction đã commit.
    /// </summary>
    Task SendBookingRescheduledAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        DateTime oldEventDate,
        string oldShift,
        string oldHallName,
        DateTime newEventDate,
        string newShift,
        string newHallName,
        CancellationToken cancellationToken = default);
}

namespace HeThongDatTiecCuoi_API.Services;

public interface IEmailService
{
    Task SendScheduleChangeOtpEmailAsync(
        string recipientEmail,
        string customerName,
        string bookingCode,
        string otpCode,
        int lifetimeMinutes,
        CancellationToken cancellationToken = default);

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
    /// Gửi email xác nhận đặt cọc thành công qua VietQR PayOS (20%).
    /// Lịch sảnh đã được khóa chính thức trên hệ thống Riverside Palace.
    /// Đính kèm hóa đơn / biên lai điện tử có chữ ký số (PAdES).
    /// </summary>
    Task SendDepositPaymentSuccessEmailAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email xác nhận hoàn tất 100% thanh toán tiệc cưới (Quyết toán).
    /// Đính kèm hóa đơn điện tử giá trị gia tăng có chữ ký số (PAdES).
    /// </summary>
    Task SendFullPaymentSuccessEmailAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email thông báo / yêu cầu thanh toán hợp đồng (Đợt 2 hoặc Quyết toán) kèm liên kết PayOS thanh toán từ xa.
    /// </summary>
    Task SendContractPaymentRequestEmailAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email xác nhận thanh toán hợp đồng (Đợt 2 hoặc Quyết toán) thành công qua VietQR PayOS.
    /// Đính kèm hóa đơn điện tử có chữ ký số điện tử (PAdES).
    /// </summary>
    Task SendContractPaymentSuccessEmailAsync(
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
        CancellationToken cancellationToken = default);
}
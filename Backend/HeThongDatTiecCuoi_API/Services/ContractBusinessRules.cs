using HeThongDatTiecCuoi_API.Constants.StatusCodes;

namespace HeThongDatTiecCuoi_API.Services;

public sealed record ContractPaymentProgress(
    decimal PaidAmount,
    decimal RemainingBalance,
    decimal Percentage,
    bool HasInconsistency);

public static class ContractBusinessRules
{
    public static bool IsEligibleBooking(string bookingStatusCode, bool hasContract) =>
        bookingStatusCode == BookingStatusCodes.Confirmed && !hasContract;

    public static ContractPaymentProgress CalculateProgress(
        decimal totalValue,
        IEnumerable<(decimal Amount, string StatusCode)> payments)
    {
        var paid = payments.Where(payment => payment.StatusCode == PaymentStatusCodes.Success)
            .Sum(payment => payment.Amount);
        return new ContractPaymentProgress(
            paid,
            totalValue - paid,
            totalValue > 0 ? paid / totalValue * 100m : 0m,
            paid > totalValue);
    }

    public static bool IsAwaitingPayment(string contractStatusCode, decimal totalValue, decimal paidAmount) =>
        contractStatusCode == ContractStatusCodes.Active && totalValue - paidAmount > 0;

    public static string? ValidateActivation(
        string contractStatusCode,
        decimal totalValue,
        string? contractContent,
        string? paymentTerms,
        string bookingStatusCode,
        decimal? requiredDepositAmount = null,
        decimal successfulDeposits = 0m)
    {
        if (contractStatusCode != ContractStatusCodes.Draft) return "Chỉ hợp đồng bản nháp mới được kích hoạt.";
        if (totalValue <= 0) return "Tổng giá trị hợp đồng phải lớn hơn 0 trước khi kích hoạt.";
        if (string.IsNullOrWhiteSpace(contractContent)) return "Nội dung hợp đồng không được để trống.";
        if (string.IsNullOrWhiteSpace(paymentTerms)) return "Điều khoản thanh toán không được để trống.";
        if (!PaymentBusinessRules.IsValidCommitment(requiredDepositAmount, totalValue) ||
            successfulDeposits < requiredDepositAmount!.Value)
            return "Chưa nhận đủ tiền cọc cam kết đã được xác minh.";
        return bookingStatusCode == BookingStatusCodes.Confirmed
            ? null
            : "Booking liên kết không còn ở trạng thái đã xác nhận.";
    }

    /// <summary>
    /// BR05: chỉ hoàn tất khi đã thanh toán đủ và dữ liệu hiện hữu xác nhận sự kiện đã diễn ra
    /// (booking COMPLETED hoặc ngày sự kiện đã qua). Không tạo trạng thái/phê duyệt mới.
    /// </summary>
    public static string? ValidateCompletion(
        string contractStatusCode,
        decimal totalValue,
        decimal paidAmount,
        string bookingStatusCode,
        DateTime eventDate,
        DateTime today)
    {
        if (contractStatusCode != ContractStatusCodes.Active) return "Chỉ hợp đồng đang hiệu lực mới được hoàn tất.";
        if (paidAmount < totalValue) return "Hợp đồng chưa được thanh toán đủ.";
        if (paidAmount > totalValue) return "Tổng thanh toán vượt giá trị hợp đồng. Vui lòng kiểm tra dữ liệu.";
        if (bookingStatusCode == BookingStatusCodes.Cancelled)
            return "Booking đã hủy nên sự kiện không được xác nhận hoàn tất.";
        if (bookingStatusCode != BookingStatusCodes.Completed && eventDate.Date >= today.Date)
            return "Sự kiện chưa diễn ra nên chưa thể hoàn tất hợp đồng.";
        return null;
    }

    public static bool CanCancel(string contractStatusCode) =>
        contractStatusCode is ContractStatusCodes.Draft or ContractStatusCodes.Active;
}

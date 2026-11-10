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
        string bookingStatusCode)
    {
        if (contractStatusCode != ContractStatusCodes.Draft) return "Chỉ hợp đồng bản nháp mới được kích hoạt.";
        if (totalValue <= 0) return "Tổng giá trị hợp đồng phải lớn hơn 0 trước khi kích hoạt.";
        if (string.IsNullOrWhiteSpace(contractContent)) return "Nội dung hợp đồng không được để trống.";
        if (string.IsNullOrWhiteSpace(paymentTerms)) return "Điều khoản thanh toán không được để trống.";
        return bookingStatusCode == BookingStatusCodes.Confirmed
            ? null
            : "Booking liên kết không còn ở trạng thái đã xác nhận.";
    }

    public static string? ValidateCompletion(string contractStatusCode, decimal totalValue, decimal paidAmount)
    {
        if (contractStatusCode != ContractStatusCodes.Active) return "Chỉ hợp đồng đang hiệu lực mới được hoàn tất.";
        if (paidAmount < totalValue) return "Hợp đồng chưa được thanh toán đủ.";
        if (paidAmount > totalValue) return "Tổng thanh toán vượt giá trị hợp đồng. Vui lòng kiểm tra dữ liệu.";
        return null;
    }

    public static bool CanCancel(string contractStatusCode) =>
        contractStatusCode is ContractStatusCodes.Draft or ContractStatusCodes.Active;
}

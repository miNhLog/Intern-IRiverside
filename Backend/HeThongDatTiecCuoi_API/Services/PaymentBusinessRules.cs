using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;

namespace HeThongDatTiecCuoi_API.Services;

public static class PaymentBusinessRules
{
    public static bool IsMoney(decimal amount) => amount > 0m && amount <= 9999999999999999.99m &&
        decimal.Round(amount, 2) == amount;

    public static bool IsValidCommitment(decimal? amount, decimal total) =>
        amount.HasValue && IsMoney(amount.Value) && amount.Value <= total;

    public static string? Validate(string contractStatus, string bookingStatus, decimal total,
        decimal? commitment, decimal paid, string type, decimal amount)
    {
        if (contractStatus is not (ContractStatusCodes.Draft or ContractStatusCodes.Active))
            return "Hợp đồng không còn cho phép ghi nhận thanh toán.";
        if (bookingStatus is BookingStatusCodes.Cancelled or BookingStatusCodes.Pending)
            return "Booking không đủ điều kiện nhận thanh toán.";
        if (!IsValidCommitment(commitment, total)) return "Cần xác minh mức cọc cam kết của hợp đồng.";
        if (type is not (PaymentTypes.Deposit or PaymentTypes.Installment or PaymentTypes.Settlement))
            return "Loại thanh toán không hợp lệ.";
        if (contractStatus == ContractStatusCodes.Draft && type != PaymentTypes.Deposit)
            return "Hợp đồng nháp chỉ được nhận tiền đặt cọc.";
        if (!IsMoney(amount)) return "Số tiền phải dương và có tối đa hai chữ số thập phân.";
        return paid < 0m || paid > total || amount > total - paid
            ? "Số tiền vượt công nợ hoặc dữ liệu thanh toán đang bất nhất." : null;
    }
}

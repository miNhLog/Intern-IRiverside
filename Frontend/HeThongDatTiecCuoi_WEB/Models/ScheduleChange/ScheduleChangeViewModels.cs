using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.ScheduleChange;

public sealed record HallManagerScheduleChangeBookingViewModel(int BookingId, string BookingCode, string CustomerName,
    DateTime CurrentDate, string CurrentShift, string CurrentHallName, int GuestCount);
public sealed record HallManagerScheduleChangeOptionsViewModel(DateTime MinimumNewDate, int ExceptionThresholdDays,
    DateTime Today, List<HallManagerScheduleChangeBookingViewModel> Bookings);

public static class ScheduleChangeStatusDisplay
{
    public static string Name(string? status) => status switch
    {
        "PENDING" => "Chờ tiếp nhận",
        "UNDER_REVIEW" => "Đang kiểm tra",
        "AWAITING_QUOTE_ACCEPTANCE" => "Chờ xác nhận báo giá",
        "PAYMENT_PENDING" => "Chờ thanh toán",
        "APPROVED" => "Đã thay đổi lịch thành công",
        "REJECTED" => "Đã từ chối",
        "CANCELLED" => "Đã rút yêu cầu",
        "EXPIRED" => "Đã hết hạn",
        "RECONCILIATION_REQUIRED" => "Đang đối soát thanh toán",
        _ => status ?? "Chưa xác định"
    };
}

public class ScheduleChangeLookupRequest
{
    [Required] public string BookingCode { get; set; } = string.Empty;
    [Required] public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class ScheduleChangeEligibilityViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = string.Empty;
    public DateTime CurrentDate { get; set; }
    public string CurrentShift { get; set; } = string.Empty;
    public int CurrentHallId { get; set; }
    public string CurrentHallName { get; set; } = string.Empty;
    public int DaysBeforeEvent { get; set; }
    public bool CanRequestOnline { get; set; }
    public string? Reason { get; set; }
    public DateTime MinimumNewDate { get; set; }
    public decimal ScheduleChangeFee { get; set; }
    public int PolicyVersion { get; set; }
}

public sealed class ScheduleChangeAvailabilityViewModel
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Shift { get; set; } = string.Empty;
    public decimal HallPrice { get; set; }
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public bool Available { get; set; }
    public string? UnavailableReason { get; set; }
}

public sealed class ScheduleChangeQuoteViewModel
{
    public int QuoteId { get; set; }
    public int Version { get; set; }
    public decimal OldHallPrice { get; set; }
    public decimal NewHallPrice { get; set; }
    public decimal HallPriceDifference { get; set; }
    public decimal ScheduleChangeFee { get; set; }
    public decimal AdjustmentTotal { get; set; }
    public decimal ContractValueBefore { get; set; }
    public decimal ContractValueAfter { get; set; }
    public decimal AmountDue { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public sealed class ScheduleChangeHistoryViewModel
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class ScheduleChangeViewModel
{
    public int ScheduleChangeId { get; set; }
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? ExceptionReason { get; set; }
    public string? ReviewResult { get; set; }
    public string? ResolutionReason { get; set; }
    public int OldHallId { get; set; }
    public string OldHallName { get; set; } = string.Empty;
    public DateTime OldDate { get; set; }
    public string OldShift { get; set; } = string.Empty;
    public int NewHallId { get; set; }
    public string NewHallName { get; set; } = string.Empty;
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public decimal AppliedFee { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public ScheduleChangeQuoteViewModel? Quote { get; set; }
    public List<ScheduleChangeHistoryViewModel> History { get; set; } = [];
    public string DisplayStatus => ScheduleChangeStatusDisplay.Name(Status);
}

public sealed class ScheduleChangePaymentViewModel
{
    public int ScheduleChangeId { get; set; }
    public long? OrderCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string ScheduleChangeStatus { get; set; } = string.Empty;
    public decimal BusinessAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool IsDemoMode { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class OtpVerificationViewModel
{
    public string VerificationToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class QuoteAcceptanceResultViewModel
{
    public ScheduleChangeViewModel ScheduleChange { get; set; } = new();
    public ScheduleChangePaymentViewModel? Payment { get; set; }
}

public sealed class ScheduleChangePolicyTierViewModel
{
    public int MinimumDays { get; set; }
    public int? MaximumDays { get; set; }
    public decimal FeeAmount { get; set; }
    public bool CustomerOnlineAllowed { get; set; }
}

public sealed class ScheduleChangePolicyViewModel
{
    public int PolicyId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public int OnlineMinimumDays { get; set; }
    public int NewDateMinimumDays { get; set; }
    public int PaymentWindowMinutes { get; set; }
    public int HoldWindowMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ChangeNote { get; set; }
    public List<ScheduleChangePolicyTierViewModel> Tiers { get; set; } = [];
}

public sealed class ScheduleChangeTransactionViewModel
{
    public long TransactionId { get; set; }
    public long OrderCode { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal BusinessAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ReconciliationData { get; set; }
}

public sealed class ScheduleChangeAmendmentViewModel
{
    public int AmendmentId { get; set; }
    public int ContractId { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal ContractValueBefore { get; set; }
    public decimal ContractValueAfter { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
    public DateTime EffectiveAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ScheduleChangeRefundViewModel
{
    public int RefundId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public sealed class ScheduleChangeFinancialViewModel
{
    public List<ScheduleChangeTransactionViewModel> Transactions { get; set; } = [];
    public List<ScheduleChangeRefundViewModel> Refunds { get; set; } = [];
    public ScheduleChangeAmendmentViewModel? Amendment { get; set; }
}

public sealed class ScheduleChangeAdminPageViewModel
{
    public List<ScheduleChangeViewModel> Items { get; set; } = [];
    public ScheduleChangeViewModel? Selected { get; set; }
    public ScheduleChangeFinancialViewModel? Financials { get; set; }
    public string? FilterStatus { get; set; }
    public string? Search { get; set; }
}

public sealed class ScheduleChangePolicyPageViewModel
{
    public ScheduleChangePolicyViewModel? Active { get; set; }
    public List<ScheduleChangePolicyViewModel> History { get; set; } = [];
}

public sealed class CreateScheduleChangeInput
{
    public string BookingCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public int NewHallId { get; set; }
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class HallManagerCreateScheduleChangeInput
{
    public int BookingId { get; set; }
    public int NewHallId { get; set; }
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? ExceptionReason { get; set; }
    public string CustomerConsentEvidence { get; set; } = string.Empty;
}

public sealed class ScheduleChangePolicyVersionInput
{
    public int OnlineMinimumDays { get; set; }
    public int NewDateMinimumDays { get; set; }
    public int PaymentWindowMinutes { get; set; }
    public int HoldWindowMinutes { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? ChangeNote { get; set; }
    public List<ScheduleChangePolicyTierViewModel> Tiers { get; set; } = [];
}

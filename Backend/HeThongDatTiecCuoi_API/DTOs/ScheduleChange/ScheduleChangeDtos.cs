using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.ScheduleChange;

public sealed record HallManagerScheduleChangeBookingDto(int BookingId, string BookingCode, string CustomerName,
    DateTime CurrentDate, string CurrentShift, string CurrentHallName, int GuestCount);
public sealed record HallManagerScheduleChangeOptionsDto(DateTime MinimumNewDate, int ExceptionThresholdDays,
    DateTime Today, List<HallManagerScheduleChangeBookingDto> Bookings);

public class ScheduleChangeLookupRequest
{
    [Required, StringLength(50)] public string BookingCode { get; init; } = string.Empty;
    [Required, StringLength(320)] public string PhoneNumber { get; init; } = string.Empty;
}

public sealed class ScheduleChangeAvailabilityRequest : ScheduleChangeLookupRequest
{
    [Required] public DateTime Date { get; init; }
    [Required, StringLength(20)] public string Shift { get; init; } = string.Empty;
}

public class SendScheduleChangeOtpRequest : ScheduleChangeLookupRequest
{
    [Required, StringLength(40)] public string Purpose { get; init; } = string.Empty;
    public int? ScheduleChangeId { get; init; }
}

public sealed class VerifyScheduleChangeOtpRequest : SendScheduleChangeOtpRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; init; } = string.Empty;
}

public sealed class CreateScheduleChangeRequest
{
    [Required, StringLength(50)] public string BookingCode { get; init; } = string.Empty;
    [Required, StringLength(320)] public string PhoneNumber { get; init; } = string.Empty;
    [Required, StringLength(200)] public string VerificationToken { get; init; } = string.Empty;
    [Range(1, int.MaxValue)] public int NewHallId { get; init; }
    [Required] public DateTime NewDate { get; init; }
    [Required, StringLength(20)] public string NewShift { get; init; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
}

public sealed class HallManagerCreateScheduleChangeRequest
{
    [Range(1, int.MaxValue)] public int BookingId { get; init; }
    [Range(1, int.MaxValue)] public int NewHallId { get; init; }
    [Required] public DateTime NewDate { get; init; }
    [Required, StringLength(20)] public string NewShift { get; init; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
    [StringLength(1000)] public string? ExceptionReason { get; init; }
    [Required, StringLength(2000, MinimumLength = 3)] public string CustomerConsentEvidence { get; init; } = string.Empty;
}

public sealed class ReviewScheduleChangeRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string ReviewResult { get; init; } = string.Empty;
}

public sealed class QuoteScheduleChangeRequest
{
    [StringLength(1000)] public string? Note { get; init; }
}

public sealed class RejectScheduleChangeRequest
{
    [Required, StringLength(1000, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
}

public sealed class AcceptScheduleChangeQuoteRequest : ScheduleChangeLookupRequest
{
    [Required, StringLength(200)] public string VerificationToken { get; init; } = string.Empty;
    [Range(1, int.MaxValue)] public int QuoteId { get; init; }
}

public sealed class WithdrawScheduleChangeRequest : ScheduleChangeLookupRequest
{
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
}

public sealed class ReconciliationScheduleChangeRequest
{
    [Required, StringLength(30)] public string Action { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 3)] public string Evidence { get; init; } = string.Empty;
}

public sealed class ManualRefundConfirmationRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string Evidence { get; init; } = string.Empty;
}

public sealed class ScheduleChangePolicyVersionRequest
{
    [Range(1, 30)] public int OnlineMinimumDays { get; init; } = 7;
    [Range(1, 365)] public int NewDateMinimumDays { get; init; } = 14;
    [Range(1, 120)] public int PaymentWindowMinutes { get; init; } = 15;
    [Range(1, 120)] public int HoldWindowMinutes { get; init; } = 20;
    public DateTime EffectiveFrom { get; init; }
    [StringLength(1000)] public string? ChangeNote { get; init; }
    [MinLength(1)] public List<ScheduleChangePolicyTierRequest> Tiers { get; init; } = [];
}

public sealed class ScheduleChangePolicyTierRequest
{
    [Range(0, 3650)] public int MinimumDays { get; init; }
    [Range(0, 3650)] public int? MaximumDays { get; init; }
    [Range(0, 1_000_000_000)] public decimal FeeAmount { get; init; }
    public bool CustomerOnlineAllowed { get; init; }
}

public sealed class ScheduleChangeEligibilityDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string BookingStatus { get; init; } = string.Empty;
    public DateTime CurrentDate { get; init; }
    public string CurrentShift { get; init; } = string.Empty;
    public int CurrentHallId { get; init; }
    public string CurrentHallName { get; init; } = string.Empty;
    public int DaysBeforeEvent { get; init; }
    public bool CanRequestOnline { get; init; }
    public string? Reason { get; init; }
    public DateTime MinimumNewDate { get; init; }
    public decimal ScheduleChangeFee { get; init; }
    public int PolicyVersion { get; init; }
}

public sealed class ScheduleChangeAvailabilityDto
{
    public int HallId { get; init; }
    public string HallName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Shift { get; init; } = string.Empty;
    public decimal HallPrice { get; init; }
    public int? MinimumCapacity { get; init; }
    public int MaximumCapacity { get; init; }
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
}

public sealed class ScheduleChangeQuoteDto
{
    public int QuoteId { get; init; }
    public int Version { get; init; }
    public decimal OldHallPrice { get; init; }
    public decimal NewHallPrice { get; init; }
    public decimal HallPriceDifference { get; init; }
    public decimal ScheduleChangeFee { get; init; }
    public decimal AdjustmentTotal { get; init; }
    public decimal ContractValueBefore { get; init; }
    public decimal ContractValueAfter { get; init; }
    public decimal AmountDue { get; init; }
    public decimal CreditAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
}

public sealed class ScheduleChangeDto
{
    public int ScheduleChangeId { get; init; }
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? ExceptionReason { get; init; }
    public string? ReviewResult { get; init; }
    public string? ResolutionReason { get; init; }
    public int OldHallId { get; init; }
    public string OldHallName { get; init; } = string.Empty;
    public DateTime OldDate { get; init; }
    public string OldShift { get; init; } = string.Empty;
    public int NewHallId { get; init; }
    public string NewHallName { get; init; } = string.Empty;
    public DateTime NewDate { get; init; }
    public string NewShift { get; init; } = string.Empty;
    public int PolicyVersion { get; init; }
    public decimal AppliedFee { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public ScheduleChangeQuoteDto? Quote { get; init; }
    public List<ScheduleChangeHistoryDto> History { get; init; } = [];
}

public sealed class ScheduleChangeHistoryDto
{
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTime ChangedAt { get; init; }
}

public sealed class ScheduleChangePaymentDto
{
    public int ScheduleChangeId { get; init; }
    public long? OrderCode { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string ScheduleChangeStatus { get; init; } = string.Empty;
    public decimal BusinessAmount { get; init; }
    public decimal ActualAmount { get; init; }
    public bool IsDemoMode { get; init; }
    public string? CheckoutUrl { get; init; }
    public string? QrCode { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class OtpVerificationDto
{
    public string VerificationToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class QuoteAcceptanceResultDto
{
    public ScheduleChangeDto ScheduleChange { get; init; } = null!;
    public ScheduleChangePaymentDto? Payment { get; init; }
}

public sealed class ScheduleChangePolicyDto
{
    public int PolicyId { get; init; }
    public string PolicyCode { get; init; } = string.Empty;
    public int Version { get; init; }
    public int OnlineMinimumDays { get; init; }
    public int NewDateMinimumDays { get; init; }
    public int PaymentWindowMinutes { get; init; }
    public int HoldWindowMinutes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeNote { get; init; }
    public List<ScheduleChangePolicyTierRequest> Tiers { get; init; } = [];
}

public sealed class ScheduleChangeTransactionDto
{
    public long TransactionId { get; init; }
    public long OrderCode { get; init; }
    public decimal ActualAmount { get; init; }
    public decimal BusinessAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? ReconciliationData { get; init; }
}

public sealed class ScheduleChangeAmendmentDto
{
    public int AmendmentId { get; init; }
    public int ContractId { get; init; }
    public int Version { get; init; }
    public string Content { get; init; } = string.Empty;
    public decimal ContractValueBefore { get; init; }
    public decimal ContractValueAfter { get; init; }
    public DateTime? CustomerConfirmedAt { get; init; }
    public DateTime EffectiveAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class ScheduleChangeRefundDto
{
    public int RefundId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
}

public sealed class ScheduleChangeFinancialDetailDto
{
    public List<ScheduleChangeTransactionDto> Transactions { get; init; } = [];
    public List<ScheduleChangeRefundDto> Refunds { get; init; } = [];
    public ScheduleChangeAmendmentDto? Amendment { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

public class ManagerBookingRescheduleQuoteRequestDto
{
    [Required] public DateTime? NewDate { get; set; }
    [Required] public string NewShift { get; set; } = string.Empty;
    [Required] public int? NewHallId { get; set; }
    public bool ChangeMenu { get; set; }
    public int? MenuId { get; set; }
    public List<int>? CustomDishIds { get; set; }
}

public sealed class ManagerBookingRescheduleRequestDto : ManagerBookingRescheduleQuoteRequestDto
{
    [MaxLength(500)] public string? Reason { get; set; }
    public bool PriceDifferenceConfirmed { get; set; }
    public decimal? ConfirmedPriceDifference { get; set; }
    public bool MenuPriceDifferenceConfirmed { get; set; }
    public decimal? ConfirmedMenuPriceDifference { get; set; }
    public Guid? RequestId { get; set; }
    public string? QuoteToken { get; set; }
    public string? PolicyVersion { get; set; }
    public string? CustomerAgreementReference { get; set; }
    public string? ManagerAgreementReference { get; set; }
}

public sealed class ManagerBookingCancelRequestDto
{
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    public Guid? RequestId { get; set; }
    public string? QuoteToken { get; set; }
    public string? Origin { get; set; }
    public string? PolicyVersion { get; set; }
    public string? AgreementReference { get; set; }
    public decimal? AgreedLowerRetention { get; set; }
    public bool? DepositSanctionsApply { get; set; }
}

public sealed class ManagerBookingCancelQuoteRequestDto
{
    [Required] public string Origin { get; set; } = string.Empty;
    public string? PolicyVersion { get; set; }
    public string? AgreementReference { get; set; }
    public decimal? AgreedLowerRetention { get; set; }
    public bool? DepositSanctionsApply { get; set; }
}

public sealed record ManagerBookingCancelQuoteDto(
    int BookingId, int? ContractId, decimal RetainedDeposit, decimal PrincipalRefundDue,
    decimal DepositPrincipalPortion, decimal CompensationDue, string QuoteToken);

public sealed record ManagerBookingCancelResultDto(
    Guid RequestId, int BookingId, int? ContractId, decimal RetainedDeposit,
    decimal PrincipalRefundDue, decimal DepositPrincipalPortion, decimal CompensationDue, bool Replayed);

public sealed class AttestBookingTableCountsRequestDto
{
    [Range(1, 500)] public int BilledTableCount { get; set; }
    [Required, Range(0, 499)] public int? SpareTableCount { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 1)] public string Reason { get; set; } = string.Empty;
}

public sealed class ManagerBookingRescheduleQuoteDto
{
    public int BookingId { get; set; }
    public int NewHallId { get; set; }
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public decimal? SavedHallPrice { get; set; }
    public decimal CurrentHallPrice { get; set; }
    public decimal PriceDifference { get; set; }
    public bool RequiresPriceConfirmation { get; set; }
    public string CapacityCheck { get; set; } = "UNKNOWN";
    public string CapacityMessage { get; set; } = string.Empty;
    public int? SpareTableCount { get; set; }
    public decimal? SavedMenuPrice { get; set; }
    public decimal? CurrentMenuPrice { get; set; }
    public decimal MenuPriceDifference { get; set; }
    public bool RequiresMenuPriceConfirmation { get; set; }
    public decimal? EstimatedTotalAfterChange { get; set; }
    public bool IsFinancialAmendment { get; set; }
    public string? PolicyVersion { get; set; }
    public decimal? SignedPriceDelta { get; set; }
    public decimal? FeeAmount { get; set; }
    public decimal? EffectiveTotalAfterChange { get; set; }
    public decimal? RefundDue { get; set; }
    public decimal? DepositPrincipalRefundDue { get; set; }
    public string? QuoteToken { get; set; }
    public int? MenuId { get; set; }
    public string? CustomMenuSnapshotJson { get; set; }
}

public sealed record ManagerBookingRescheduleResultDto(
    Guid RequestId, int BookingId, int ContractId, string Kind,
    decimal SignedPriceDelta, decimal FeeAmount, decimal EffectiveTotal, decimal RefundDue,
    decimal DepositPrincipalRefundDue, string? PolicyVersion, bool Replayed);

public sealed class ManagerBookingHandoffRequestDto
{
    [Required] public Guid RequestId { get; set; }
    [Required] public string Operation { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 1)] public string Reason { get; set; } = string.Empty;
}

public sealed record ManagerBookingHandoffResultDto(Guid RequestId, int BookingId, string Operation, bool Replayed);

public sealed class ManagerBookingHallOptionDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public decimal RentalPrice { get; set; }
}

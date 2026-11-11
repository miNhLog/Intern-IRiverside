namespace HeThongDatTiecCuoi_API.Models;

public sealed class Uc18BookingOperation
{
    public const string Reschedule = "RESCHEDULE";
    public const string MenuChange = "MENU_CHANGE";
    public const string Cancel = "CANCEL";
    public const string Handoff = "HANDOFF";
    public const string PriorRefund = "PRIOR_REFUND";

    public Guid RequestId { get; set; }
    public int BookingId { get; set; }
    public int? ContractId { get; set; }
    public int ActorUserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    public string? PolicyVersion { get; set; }
    public string? ManagerAgreementReference { get; set; }
    public string? SnapshotJson { get; set; }
    public decimal SignedPriceDelta { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal? EffectiveTotal { get; set; }
    public decimal RetainedDeposit { get; set; }
    public decimal PrincipalRefundDue { get; set; }
    public decimal DepositPrincipalPortion { get; set; }
    public decimal CompensationDue { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public Contract? Contract { get; set; }
    public User Actor { get; set; } = null!;
    public ICollection<Uc18RefundConfirmation> RefundConfirmations { get; set; } = [];
}

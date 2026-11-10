namespace HeThongDatTiecCuoi_API.Models;

public sealed class ScheduleChangeQuote
{
    public int QuoteId { get; set; }
    public int ScheduleChangeId { get; set; }
    public int Version { get; set; }
    public decimal OldHallPrice { get; set; }
    public decimal NewHallPrice { get; set; }
    public decimal HallPriceDifference { get; set; }
    public decimal ScheduleChangeFee { get; set; }
    public decimal AdjustmentTotal { get; set; }
    public decimal ContractValueBefore { get; set; }
    public decimal ContractValueAfter { get; set; }
    public decimal PaidAmountSnapshot { get; set; }
    public decimal AmountDue { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public WeddingScheduleChange ScheduleChange { get; set; } = null!;
}

public sealed class ScheduleChangeOtp
{
    public long OtpId { get; set; }
    public int BookingId { get; set; }
    public int? ScheduleChangeId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string PhoneHash { get; set; } = string.Empty;
    public string IpHash { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string? VerificationTokenHash { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public WeddingScheduleChange? ScheduleChange { get; set; }
}

public sealed class ScheduleChangeHold
{
    public int HoldId { get; set; }
    public int ScheduleChangeId { get; set; }
    public int HallScheduleId { get; set; }
    public long? PaymentTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public WeddingScheduleChange ScheduleChange { get; set; } = null!;
    public HallSchedule HallSchedule { get; set; } = null!;
    public PaymentTransaction? PaymentTransaction { get; set; }
}

public sealed class ScheduleChangeStatusHistory
{
    public long HistoryId { get; set; }
    public int ScheduleChangeId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int? ActorUserId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }

    public WeddingScheduleChange ScheduleChange { get; set; } = null!;
}

public sealed class ContractAmendment
{
    public int AmendmentId { get; set; }
    public int ContractId { get; set; }
    public int ScheduleChangeId { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal ContractValueBefore { get; set; }
    public decimal ContractValueAfter { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
    public int ApprovedByUserId { get; set; }
    public DateTime EffectiveAt { get; set; }
    public string Status { get; set; } = string.Empty;

    public Contract Contract { get; set; } = null!;
    public WeddingScheduleChange ScheduleChange { get; set; } = null!;
}

public sealed class ScheduleChangeRefund
{
    public int RefundId { get; set; }
    public int ScheduleChangeId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? ProcessedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Evidence { get; set; }

    public WeddingScheduleChange ScheduleChange { get; set; } = null!;
}

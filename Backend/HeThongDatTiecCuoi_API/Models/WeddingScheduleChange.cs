namespace HeThongDatTiecCuoi_API.Models;

public sealed class WeddingScheduleChange
{
    public int ScheduleChangeId { get; set; }
    public int BookingId { get; set; }
    public int OldHallScheduleId { get; set; }
    public int NewHallScheduleId { get; set; }
    public string? Reason { get; set; }
    public string RequestSource { get; set; } = string.Empty;
    public int? CreatedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ExceptionReason { get; set; }
    public string? CustomerConsentEvidence { get; set; }
    public string? ReviewResult { get; set; }
    public string? ResolutionReason { get; set; }
    public int StatusId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
    public DateTime? PaymentCompletedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int PolicyId { get; set; }
    public int PolicyVersion { get; set; }
    public int SubmittedDaysBeforeEvent { get; set; }
    public decimal AppliedFee { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public WeddingBooking Booking { get; set; } = null!;
    public HallSchedule OldHallSchedule { get; set; } = null!;
    public HallSchedule NewHallSchedule { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ScheduleChangePolicy Policy { get; set; } = null!;
    public ICollection<ScheduleChangeQuote> Quotes { get; set; } = [];
    public ICollection<ScheduleChangeHold> Holds { get; set; } = [];
    public ICollection<ScheduleChangeStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
    public ICollection<ScheduleChangeOtp> Otps { get; set; } = [];
    public ContractAmendment? ContractAmendment { get; set; }
    public ICollection<ScheduleChangeRefund> Refunds { get; set; } = [];
}

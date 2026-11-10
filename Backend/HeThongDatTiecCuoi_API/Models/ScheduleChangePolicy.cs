namespace HeThongDatTiecCuoi_API.Models;

public sealed class ScheduleChangePolicy
{
    public int PolicyId { get; set; }
    public string PolicyCode { get; set; } = "POL-CHG-05";
    public int Version { get; set; }
    public int OnlineMinimumDays { get; set; }
    public int NewDateMinimumDays { get; set; }
    public int PaymentWindowMinutes { get; set; }
    public int HoldWindowMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangeNote { get; set; }

    public ICollection<ScheduleChangePolicyTier> Tiers { get; set; } = [];
    public ICollection<WeddingScheduleChange> ScheduleChanges { get; set; } = [];
}

public sealed class ScheduleChangePolicyTier
{
    public int TierId { get; set; }
    public int PolicyId { get; set; }
    public int MinimumDays { get; set; }
    public int? MaximumDays { get; set; }
    public decimal FeeAmount { get; set; }
    public bool CustomerOnlineAllowed { get; set; }

    public ScheduleChangePolicy Policy { get; set; } = null!;
}


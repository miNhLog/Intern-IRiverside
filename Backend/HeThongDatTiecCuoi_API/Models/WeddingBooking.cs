namespace HeThongDatTiecCuoi_API.Models;

public sealed class WeddingBooking
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int HallScheduleId { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int GuestCount { get; set; }
    public decimal? FinalMenuPrice { get; set; }
    public decimal? FinalDecorationPrice { get; set; }
    public decimal? FinalHallPrice { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string? SpecialRequests { get; set; }
    public int StatusId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime BookedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public HallSchedule HallSchedule { get; set; } = null!;
    public Menu? Menu { get; set; }
    public DecorPackage? DecorationPackage { get; set; }
    public Status Status { get; set; } = null!;
    public Contract? Contract { get; set; }
    public ICollection<WeddingBookingService> BookingServices { get; set; } = [];
    public ICollection<CoordinationAssignment> CoordinationAssignments { get; set; } = [];
    public ICollection<Incident> Incidents { get; set; } = [];
    public ICollection<WeddingScheduleChange> ScheduleChanges { get; set; } = [];
    public ReviewQrCode? ReviewQrCode { get; set; }
}

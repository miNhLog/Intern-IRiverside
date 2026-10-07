namespace HeThongDatTiecCuoi_API.Models;

public sealed class WeddingScheduleChange
{
    public int ScheduleChangeId { get; set; }
    public int BookingId { get; set; }
    public int OldHallScheduleId { get; set; }
    public int NewHallScheduleId { get; set; }
    public string? Reason { get; set; }
    public int StatusId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public HallSchedule OldHallSchedule { get; set; } = null!;
    public HallSchedule NewHallSchedule { get; set; } = null!;
    public Status Status { get; set; } = null!;
}

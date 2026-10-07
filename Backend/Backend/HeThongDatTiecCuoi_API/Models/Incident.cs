namespace HeThongDatTiecCuoi_API.Models;

public sealed class Incident
{
    public int IncidentId { get; set; }
    public int BookingId { get; set; }
    public int? ReportedByEmployeeId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string? Resolution { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public Employee? ReportedByEmployee { get; set; }
    public Status Status { get; set; } = null!;
}

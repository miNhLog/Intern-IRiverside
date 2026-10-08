namespace HeThongDatTiecCuoi_API.DTOs.Dashboard;

public sealed class ManagerDashboardDto
{
    public DateTime GeneratedAt { get; init; }
    public int TodayEventCount { get; init; }
    public int UpcomingEventCount { get; init; }
    public int PendingBookingCount { get; init; }
    public int NewBookingCount { get; init; }
    public decimal RevenueThisMonth { get; init; }
    public decimal OutstandingDebt { get; init; }
    public int OpenIncidentCount { get; init; }
    public List<ManagerUpcomingEventDto> UpcomingEvents { get; init; } = [];
    public List<ManagerRecentBookingDto> RecentBookings { get; init; } = [];
    public List<ManagerIncidentDto> OpenIncidents { get; init; } = [];
}

public sealed class ManagerUpcomingEventDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string Shift { get; init; } = string.Empty;
    public int GuestCount { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
}

public sealed class ManagerRecentBookingDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime BookedAt { get; init; }
    public DateTime EventDate { get; init; }
    public decimal EstimatedTotal { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
}

public sealed class ManagerIncidentDto
{
    public int IncidentId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}

namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class ManagerDashboardViewModel
{
    public DateTime GeneratedAt { get; set; }
    public int TodayEventCount { get; set; }
    public int UpcomingEventCount { get; set; }
    public int PendingBookingCount { get; set; }
    public int NewBookingCount { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal OutstandingDebt { get; set; }
    public int OpenIncidentCount { get; set; }
    public List<ManagerUpcomingEventViewModel> UpcomingEvents { get; set; } = [];
    public List<ManagerRecentBookingViewModel> RecentBookings { get; set; } = [];
    public List<ManagerIncidentViewModel> OpenIncidents { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class ManagerUpcomingEventViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class ManagerRecentBookingViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
    public DateTime EventDate { get; set; }
    public decimal EstimatedTotal { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class ManagerIncidentViewModel
{
    public int IncidentId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

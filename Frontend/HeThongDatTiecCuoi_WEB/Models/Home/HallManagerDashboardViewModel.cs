namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class HallManagerDashboardViewModel
{
    public DateTime GeneratedAt { get; set; }
    public HallManagerDashboardKpiViewModel Kpi { get; set; } = new();
    public HallManagerScheduleViewModel Schedule { get; set; } = new();
    public List<HallManagerUpcomingBookingViewModel> UpcomingBookings { get; set; } = [];
    public List<HallManagerTaskViewModel> Tasks { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class HallManagerDashboardKpiViewModel
{
    public int ManagedHalls { get; set; }
    public int TodayBookings { get; set; }
    public int NextSevenDaysBookings { get; set; }
    public int UnassignedBookings { get; set; }
    public int CoordinationConflicts { get; set; }
    public int OpenIncidents { get; set; }
}

public sealed class HallManagerScheduleViewModel
{
    public DateTime Date { get; set; }
    public List<HallManagerScheduleRowViewModel> Halls { get; set; } = [];
}

public sealed class HallManagerScheduleRowViewModel
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public HallManagerScheduleSlotViewModel Lunch { get; set; } = new();
    public HallManagerScheduleSlotViewModel Dinner { get; set; } = new();
}

public sealed class HallManagerScheduleSlotViewModel
{
    public string Shift { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public string? BookingCode { get; set; }
    public string? CustomerName { get; set; }
    public bool RequiresReview { get; set; }
}

public sealed class HallManagerUpcomingBookingViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public string BookingStatusCode { get; set; } = string.Empty;
    public string BookingStatusName { get; set; } = string.Empty;
    public string? CoordinatorName { get; set; }
    public string CoordinationState { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
}

public sealed class HallManagerTaskViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime RelevantAt { get; set; }
    public int? BookingId { get; set; }
    public int? IncidentId { get; set; }
}

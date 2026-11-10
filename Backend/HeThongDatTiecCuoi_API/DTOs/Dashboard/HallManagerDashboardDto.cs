namespace HeThongDatTiecCuoi_API.DTOs.Dashboard;

public sealed class HallManagerDashboardDto
{
    public DateTime GeneratedAt { get; init; }
    public HallManagerDashboardKpiDto Kpi { get; init; } = new();
    public HallManagerScheduleDto Schedule { get; init; } = new();
    public List<HallManagerUpcomingBookingDto> UpcomingBookings { get; init; } = [];
    public List<HallManagerTaskDto> Tasks { get; init; } = [];
}

public sealed class HallManagerDashboardKpiDto
{
    public int ManagedHalls { get; init; }
    public int TodayBookings { get; init; }
    public int NextSevenDaysBookings { get; init; }
    public int UnassignedBookings { get; init; }
    public int CoordinationConflicts { get; init; }
    public int OpenIncidents { get; init; }
}

public sealed class HallManagerScheduleDto
{
    public DateTime Date { get; init; }
    public List<HallManagerScheduleRowDto> Halls { get; init; } = [];
}

public sealed class HallManagerScheduleRowDto
{
    public int HallId { get; init; }
    public string HallName { get; init; } = string.Empty;
    public HallManagerScheduleSlotDto Lunch { get; init; } = new();
    public HallManagerScheduleSlotDto Dinner { get; init; } = new();
}

public sealed class HallManagerScheduleSlotDto
{
    public string Shift { get; init; } = string.Empty;
    public string StateCode { get; init; } = string.Empty;
    public string StateName { get; init; } = string.Empty;
    public string? BookingCode { get; init; }
    public string? CustomerName { get; init; }
    public bool RequiresReview { get; init; }
}

public sealed class HallManagerUpcomingBookingDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string Shift { get; init; } = string.Empty;
    public int TableCount { get; init; }
    public string BookingStatusCode { get; init; } = string.Empty;
    public string BookingStatusName { get; init; } = string.Empty;
    public string? CoordinatorName { get; init; }
    public string CoordinationState { get; init; } = string.Empty;
    public bool HasConflict { get; init; }
}

public sealed class HallManagerTaskDto
{
    public string Type { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string ReferenceCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public DateTime RelevantAt { get; init; }
    public int? BookingId { get; init; }
    public int? IncidentId { get; init; }
}

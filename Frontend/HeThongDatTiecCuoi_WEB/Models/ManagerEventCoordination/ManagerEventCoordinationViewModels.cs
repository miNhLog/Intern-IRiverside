namespace HeThongDatTiecCuoi_WEB.Models.ManagerEventCoordination;

public sealed class ManagerEventCoordinationPageViewModel
{
    public EventCoordinationListResponseViewModel Data { get; set; } = new();
    public string? Search { get; set; }
    public string? BookingStatus { get; set; }
    public string? CoordinationStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class EventCoordinationListResponseViewModel
{
    public List<EventCoordinationListItemViewModel> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public EventCoordinationKpiViewModel Kpi { get; set; } = new();
    public List<EventCoordinationOptionViewModel> BookingStatuses { get; set; } = [];
    public List<EventCoordinationOptionViewModel> Halls { get; set; } = [];
    public List<EventCoordinationOptionViewModel> Coordinators { get; set; } = [];
}

public sealed class EventCoordinationKpiViewModel
{
    public int UpcomingCount { get; set; }
    public int UnassignedCount { get; set; }
    public int PreparingCount { get; set; }
    public int IncidentAttentionCount { get; set; }
}

public sealed class EventCoordinationOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class EventCoordinationListItemViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BookingStatusCode { get; set; } = string.Empty;
    public string BookingStatusName { get; set; } = string.Empty;
    public string? HallManagerName { get; set; }
    public int? CoordinatorEmployeeId { get; set; }
    public string? CoordinatorName { get; set; }
    public string CoordinationState { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
}

public sealed class EventCoordinationDetailViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public string BookingStatusCode { get; set; } = string.Empty;
    public string BookingStatusName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int GuestCount { get; set; }
    public string? HallManagerName { get; set; }
    public string? CoordinatorName { get; set; }
    public string CoordinationState { get; set; } = string.Empty;
    public DateTime? AssignedAt { get; set; }
    public bool HasConflict { get; set; }
    public int OpenIncidentCount { get; set; }
    public int ScheduleChangeCount { get; set; }
    public string? MenuName { get; set; }
    public decimal? FinalMenuPrice { get; set; }
    public string? DecorationName { get; set; }
    public decimal? FinalDecorationPrice { get; set; }
    public decimal? FinalHallPrice { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string? SpecialRequests { get; set; }
    public List<EventCoordinationServiceViewModel> Services { get; set; } = [];
    public List<EventCoordinationAssignmentViewModel> Assignments { get; set; } = [];
    public List<EventScheduleChangeViewModel> ScheduleChanges { get; set; } = [];
    public List<EventIncidentViewModel> Incidents { get; set; } = [];
}

public sealed class EventCoordinationServiceViewModel { public string ServiceName { get; set; } = string.Empty; public int Quantity { get; set; } public decimal FinalUnitPrice { get; set; } }
public sealed class EventCoordinationAssignmentViewModel { public string CoordinatorName { get; set; } = string.Empty; public string CoordinatorCode { get; set; } = string.Empty; public string AssignedByName { get; set; } = string.Empty; public string StatusCode { get; set; } = string.Empty; public string StatusName { get; set; } = string.Empty; public DateTime AssignedAt { get; set; } }
public sealed class EventScheduleChangeViewModel { public string OldHallName { get; set; } = string.Empty; public DateTime OldDate { get; set; } public string OldShift { get; set; } = string.Empty; public string NewHallName { get; set; } = string.Empty; public DateTime NewDate { get; set; } public string NewShift { get; set; } = string.Empty; public string? Reason { get; set; } public string StatusName { get; set; } = string.Empty; public DateTime RequestedAt { get; set; } public DateTime? ProcessedAt { get; set; } }
public sealed class EventIncidentViewModel { public int IncidentId { get; set; } public string IncidentType { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public string Severity { get; set; } = string.Empty; public string StatusCode { get; set; } = string.Empty; public string StatusName { get; set; } = string.Empty; public string? ReportedByName { get; set; } public string? Resolution { get; set; } public DateTime OccurredAt { get; set; } public DateTime? ResolvedAt { get; set; } }

public sealed class EventCoordinationCalendarViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<EventCoordinationListItemViewModel> Items { get; set; } = [];
}


public sealed class ManagerAvailableCoordinatorViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsCurrent { get; set; }
    public string StatusNote { get; set; } = string.Empty;
}

public sealed class ManagerEmergencyAssignRequestViewModel
{
    public int CoordinatorEmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ManagerAssignResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public int? AssignmentId { get; set; }
}

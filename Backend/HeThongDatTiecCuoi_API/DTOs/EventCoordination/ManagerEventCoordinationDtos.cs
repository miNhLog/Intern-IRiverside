namespace HeThongDatTiecCuoi_API.DTOs.EventCoordination;

public sealed class EventCoordinationListResponseDto
{
    public List<EventCoordinationListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public EventCoordinationKpiDto Kpi { get; set; } = new();
    public List<EventCoordinationOptionDto> BookingStatuses { get; set; } = [];
    public List<EventCoordinationOptionDto> Halls { get; set; } = [];
    public List<EventCoordinationOptionDto> Coordinators { get; set; } = [];
}

public sealed class EventCoordinationKpiDto
{
    public int UpcomingCount { get; set; }
    public int UnassignedCount { get; set; }
    public int PreparingCount { get; set; }
    public int IncidentAttentionCount { get; set; }
}

public sealed class EventCoordinationOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class EventCoordinationListItemDto
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

public sealed class EventCoordinationDetailDto
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
    public List<EventCoordinationServiceDto> Services { get; set; } = [];
    public List<EventCoordinationAssignmentDto> Assignments { get; set; } = [];
    public List<EventScheduleChangeDto> ScheduleChanges { get; set; } = [];
    public List<EventIncidentDto> Incidents { get; set; } = [];
}

public sealed class EventCoordinationServiceDto
{
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal FinalUnitPrice { get; set; }
}

public sealed class EventCoordinationAssignmentDto
{
    public string CoordinatorName { get; set; } = string.Empty;
    public string CoordinatorCode { get; set; } = string.Empty;
    public string AssignedByName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public sealed class EventScheduleChangeDto
{
    public string OldHallName { get; set; } = string.Empty;
    public DateTime OldDate { get; set; }
    public string OldShift { get; set; } = string.Empty;
    public string NewHallName { get; set; } = string.Empty;
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public sealed class EventIncidentDto
{
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? ReportedByName { get; set; }
    public string? Resolution { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class EventCoordinationCalendarDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<EventCoordinationCalendarItemDto> Items { get; set; } = [];
}

public sealed class EventCoordinationCalendarItemDto : EventCoordinationListItemDto
{
}

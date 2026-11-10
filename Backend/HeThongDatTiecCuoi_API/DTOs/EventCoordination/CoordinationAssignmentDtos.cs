using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.EventCoordination;

public sealed class HallManagerCoordinationQuery
{
    public string? Search { get; set; }
    public int? HallId { get; set; }
    public DateTime? EventDate { get; set; }
    public string? CoordinationStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class HallManagerCoordinationListDto
{
    public List<HallManagerCoordinationItemDto> Items { get; set; } = [];
    public List<EventCoordinationOptionDto> Halls { get; set; } = [];
    public HallManagerCoordinationKpiDto Kpi { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class HallManagerCoordinationKpiDto
{
    public int TotalRequired { get; set; }
    public int Unassigned { get; set; }
    public int Assigned { get; set; }
    public int Conflicts { get; set; }
}

public sealed class HallManagerCoordinationItemDto : EventCoordinationListItemDto
{
    public string? IneligibilityReason { get; set; }
    public int TableCount { get; set; }
    public bool CanAssign { get; set; }
    public bool CanChangeOrCancel { get; set; }
}

public sealed class CoordinatorAvailabilityDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string AvailabilityMessage { get; set; } = string.Empty;
}

public sealed class CoordinationAssignmentHistoryDto
{
    public int CoordinationAssignmentId { get; set; }
    public int CoordinatorEmployeeId { get; set; }
    public string CoordinatorCode { get; set; } = string.Empty;
    public string CoordinatorName { get; set; } = string.Empty;
    public string AssignedByName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public sealed class HallManagerCoordinationDetailDto
{
    public HallManagerCoordinationItemDto Booking { get; set; } = new();
    public List<CoordinationAssignmentHistoryDto> History { get; set; } = [];
    public List<CoordinationAuditDto> AuditTrail { get; set; } = [];
}

public sealed class CoordinationAuditDto
{
    public string Action { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
}

public sealed class CreateCoordinationAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public int CoordinatorEmployeeId { get; set; }
}

public sealed class ChangeCoordinationAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public int CoordinatorEmployeeId { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CancelCoordinationAssignmentRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CoordinationMutationResultDto
{
    public int BookingId { get; set; }
    public int? AssignmentId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CoordinatorAssignedPartyDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public string? MenuName { get; set; }
    public string? DecorationName { get; set; }
    public List<string> Services { get; set; } = [];
    public string? SpecialRequests { get; set; }
    public string BookingStatusCode { get; set; } = string.Empty;
    public string BookingStatusName { get; set; } = string.Empty;
}

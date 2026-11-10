using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Coordination;

public sealed class HallManagerCoordinationPageViewModel
{
    public HallManagerCoordinationListViewModel Data { get; set; } = new();
    public string? Search { get; set; }
    public int? HallId { get; set; }
    public DateTime? EventDate { get; set; }
    public string? CoordinationStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class HallManagerCoordinationListViewModel
{
    public List<HallManagerCoordinationItemViewModel> Items { get; set; } = [];
    public List<CoordinationOptionViewModel> Halls { get; set; } = [];
    public HallManagerCoordinationKpiViewModel Kpi { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class HallManagerCoordinationKpiViewModel
{
    public int TotalRequired { get; set; }
    public int Unassigned { get; set; }
    public int Assigned { get; set; }
    public int Conflicts { get; set; }
}

public sealed class HallManagerCoordinationItemViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public string BookingStatusCode { get; set; } = string.Empty;
    public string BookingStatusName { get; set; } = string.Empty;
    public int? CoordinatorEmployeeId { get; set; }
    public string? CoordinatorName { get; set; }
    public string CoordinationState { get; set; } = string.Empty;
    public string? IneligibilityReason { get; set; }
    public bool HasConflict { get; set; }
    public bool CanAssign { get; set; }
    public bool CanChangeOrCancel { get; set; }
}

public sealed class CoordinationOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class CoordinatorAvailabilityViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string AvailabilityMessage { get; set; } = string.Empty;
}

public sealed class CoordinationAssignmentHistoryViewModel
{
    public int CoordinationAssignmentId { get; set; }
    public string CoordinatorCode { get; set; } = string.Empty;
    public string CoordinatorName { get; set; } = string.Empty;
    public string AssignedByName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public sealed class CoordinationAuditViewModel
{
    public string Action { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
}

public sealed class HallManagerCoordinationDetailViewModel
{
    public HallManagerCoordinationItemViewModel Booking { get; set; } = new();
    public List<CoordinationAssignmentHistoryViewModel> History { get; set; } = [];
    public List<CoordinationAuditViewModel> AuditTrail { get; set; } = [];
}

public sealed class CoordinationMutationViewModel
{
    public int BookingId { get; set; }
    public int? AssignmentId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CoordinationFormViewModel
{
    [Range(1, int.MaxValue)] public int BookingId { get; set; }
    [Range(1, int.MaxValue)] public int CoordinatorEmployeeId { get; set; }
    public string? Reason { get; set; }
}

public sealed class CoordinatorAssignedPartyViewModel
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

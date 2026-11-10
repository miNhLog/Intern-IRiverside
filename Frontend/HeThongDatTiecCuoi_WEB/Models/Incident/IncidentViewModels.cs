using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Incident;

public sealed class CoordinatorIncidentPageViewModel
{
    public IncidentListResponseViewModel Data { get; set; } = new();
    public List<IncidentBookingOptionViewModel> BookingOptions { get; set; } = [];
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class HallManagerIncidentPageViewModel
{
    public IncidentListResponseViewModel Data { get; set; } = new();
    public List<IncidentBookingOptionViewModel> BookingOptions { get; set; } = [];
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public int? HallId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class HallManagerIncidentDetailViewModel
{
    public IncidentDetailViewModel Incident { get; set; } = new();
    public List<IncidentHistoryItemViewModel> History { get; set; } = [];
}

public sealed class ManagerIncidentPageViewModel
{
    public IncidentListResponseViewModel Data { get; set; } = new();
    public List<IncidentBookingOptionViewModel> BookingOptions { get; set; } = [];
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public int? HallId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ManagerIncidentDetailViewModel
{
    public IncidentDetailViewModel Incident { get; set; } = new();
    public List<IncidentHistoryItemViewModel> History { get; set; } = [];
}

public sealed class IncidentListResponseViewModel
{
    public List<IncidentListItemViewModel> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public IncidentKpiViewModel Kpi { get; set; } = new();
    public List<IncidentHallOptionViewModel> Halls { get; set; } = [];
}

public sealed class IncidentKpiViewModel
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Resolved { get; set; }
    public int Critical { get; set; }
}

public class IncidentListItemViewModel
{
    public int IncidentId { get; set; }
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public bool CanEdit { get; set; }
    public string ReporterName { get; set; } = string.Empty;
}

public sealed class IncidentDetailViewModel : IncidentListItemViewModel
{
    public string Description { get; set; } = string.Empty;
    public string ReporterCode { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public List<string> HallManagerNames { get; set; } = [];
}

public sealed class IncidentBookingOptionViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
}

public sealed class IncidentHallOptionViewModel
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
}

public sealed class IncidentHistoryItemViewModel
{
    public long AuditLogId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
}

public sealed class IncidentResolutionViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập hướng giải quyết.")]
    [StringLength(4000, MinimumLength = 3, ErrorMessage = "Hướng giải quyết phải từ 3 đến 4.000 ký tự.")]
    public string Resolution { get; set; } = string.Empty;
}

public sealed class IncidentFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tiệc được phân công.")]
    public int BookingId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập loại sự cố.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Loại sự cố phải từ 2 đến 100 ký tự.")]
    public string IncidentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả chi tiết sự cố.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 4.000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ nghiêm trọng.")]
    public string Severity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thời gian phát sinh.")]
    public DateTime OccurredAt { get; set; }
}

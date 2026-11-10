using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Incident;

public sealed class IncidentListResponseDto
{
    public List<IncidentListItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public IncidentKpiDto Kpi { get; init; } = new();
    public List<IncidentHallOptionDto> Halls { get; init; } = [];
}

public sealed class IncidentKpiDto
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Processing { get; init; }
    public int Resolved { get; init; }
    public int Critical { get; init; }
}

public class IncidentListItemDto
{
    public int IncidentId { get; init; }
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string StatusCode { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public bool CanEdit { get; init; }
    public string ReporterName { get; init; } = string.Empty;
}

public sealed class IncidentDetailDto : IncidentListItemDto
{
    public string Description { get; init; } = string.Empty;
    public string ReporterCode { get; init; } = string.Empty;
    public string? Resolution { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime EventDate { get; init; }
    public string Shift { get; init; } = string.Empty;
    public List<string> HallManagerNames { get; init; } = [];
}

public sealed class IncidentBookingOptionDto
{
    public int BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string Shift { get; init; } = string.Empty;
}

public sealed class IncidentHallOptionDto
{
    public int HallId { get; init; }
    public string HallName { get; init; } = string.Empty;
}

public sealed class IncidentHistoryItemDto
{
    public long AuditLogId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActionName { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? Notes { get; init; }
    public string? OldData { get; init; }
    public string? NewData { get; init; }
}

public sealed class CreateIncidentRequest
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

    public DateTime OccurredAt { get; set; }
}

public sealed class UpdateIncidentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập loại sự cố.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Loại sự cố phải từ 2 đến 100 ký tự.")]
    public string IncidentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả chi tiết sự cố.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 4.000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ nghiêm trọng.")]
    public string Severity { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}

public sealed class UpdateIncidentStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Resolution { get; set; }
}

public sealed class UpdateIncidentResolutionRequest
{
    [Required(ErrorMessage = "Vui lòng nhập hướng giải quyết.")]
    [StringLength(4000, MinimumLength = 3, ErrorMessage = "Hướng giải quyết phải từ 3 đến 4.000 ký tự.")]
    public string Resolution { get; set; } = string.Empty;
}

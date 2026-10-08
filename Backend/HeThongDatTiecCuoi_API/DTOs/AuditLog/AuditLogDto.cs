namespace HeThongDatTiecCuoi_API.DTOs.AuditLog;

public sealed class AuditLogDto
{
    public long AuditLogId { get; init; }
    public DateTime Timestamp { get; init; }
    public int? ActorUserId { get; init; }
    public string Actor { get; init; } = "Hệ thống";
    public string ActorName { get; init; } = "Hệ thống";
    public string? ActorEmail { get; init; }
    public string Action { get; init; } = string.Empty;
    public long TargetUserId { get; init; }
    public string Target { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public long EntityId { get; init; }
    public string? OldData { get; init; }
    public string? NewData { get; init; }
    public string? Notes { get; init; }
}

public sealed class AuditLogPageDto
{
    public List<AuditLogDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

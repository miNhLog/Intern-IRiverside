namespace HeThongDatTiecCuoi_API.DTOs.AuditLog;

public sealed class AuditLogDto
{
    public long AuditLogId { get; init; }
    public DateTime Timestamp { get; init; }
    public int? ActorUserId { get; init; }
    public string Actor { get; init; } = "Hệ thống";
    public string ActorName { get; init; } = "Hệ thống";
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public string Action { get; init; } = string.Empty;
    public long? TargetUserId { get; init; }
    public string Target { get; init; } = string.Empty;
    public string? TargetRole { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public long EntityId { get; init; }
    public string? OldData { get; init; }
    public string? NewData { get; init; }
    public string? Notes { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? AuthMethod { get; init; }
    public string? Outcome { get; init; }
    public string? CorrelationId { get; init; }
    public string Group { get; init; } = string.Empty;
    public string Source { get; init; } = "Internal";
}

public sealed class AuditLogPageDto
{
    public List<AuditLogDto> Items { get; init; } = [];
    public AuditLogStatisticsDto Statistics { get; init; } = new();
    public List<string> AvailableActions { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public string? Mode { get; init; }
    public DateTime? EffectiveFromDate { get; init; }
    public DateTime? EffectiveToDate { get; init; }
    public bool AppliedDefaultPeriod { get; init; }
}

public sealed class AuditLogStatisticsDto
{
    public int NeedsReview { get; init; }
    public int RoleChanges { get; init; }
    public int FinancialEvents { get; init; }
}

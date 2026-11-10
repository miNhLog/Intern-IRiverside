namespace HeThongDatTiecCuoi_API.Models;

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? AuthMethod { get; set; }
    public string? Outcome { get; set; }
    public string? CorrelationId { get; set; }

    public User? User { get; set; }
}

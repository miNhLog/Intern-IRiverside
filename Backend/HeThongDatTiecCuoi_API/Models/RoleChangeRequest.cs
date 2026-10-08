namespace HeThongDatTiecCuoi_API.Models;

public sealed class RoleChangeRequest
{
    public int RoleChangeRequestId { get; set; }
    public int EmployeeId { get; set; }
    public byte CurrentRoleId { get; set; }
    public byte RequestedRoleId { get; set; }
    public int RequestedByUserId { get; set; }
    public string? Reason { get; set; }
    public string? EmployeeCodeBefore { get; set; }
    public string? EmployeeCodeAfter { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Employee Employee { get; set; } = null!;
    public Role CurrentRole { get; set; } = null!;
    public Role RequestedRole { get; set; } = null!;
    public User RequestedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}

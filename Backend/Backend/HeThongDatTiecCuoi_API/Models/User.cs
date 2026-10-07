namespace HeThongDatTiecCuoi_API.Models;

public sealed class User
{
    public int UserId { get; set; }
    public byte RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte DataStatusId { get; set; }
    public bool MustChangePassword { get; set; }

    public Role Role { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public Customer? Customer { get; set; }
    public Employee? Employee { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}

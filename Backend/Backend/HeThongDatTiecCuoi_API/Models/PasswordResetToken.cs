namespace HeThongDatTiecCuoi_API.Models;

public sealed class PasswordResetToken
{
    public long PasswordResetTokenId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

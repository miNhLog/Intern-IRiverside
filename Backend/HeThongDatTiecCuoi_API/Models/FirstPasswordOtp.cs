namespace HeThongDatTiecCuoi_API.Models;

public sealed class FirstPasswordOtp
{
    public long FirstPasswordOtpId { get; set; }
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

namespace HeThongDatTiecCuoi_API.Models;

public sealed class Notification
{
    public int NotificationId { get; set; }
    public int RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public User RecipientUser { get; set; } = null!;
}

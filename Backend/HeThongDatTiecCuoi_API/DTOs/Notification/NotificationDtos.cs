namespace HeThongDatTiecCuoi_API.DTOs.Notification;

public sealed class NotificationDto
{
    public int NotificationId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? RelatedEntityType { get; init; }
    public int? RelatedEntityId { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string NavigationUrl { get; init; } = "/thong-bao";
}

public sealed class NotificationPageDto
{
    public List<NotificationDto> Items { get; init; } = [];
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public int UnreadCount { get; init; }
}

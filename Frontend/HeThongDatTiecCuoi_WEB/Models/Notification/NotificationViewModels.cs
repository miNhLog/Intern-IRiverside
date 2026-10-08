namespace HeThongDatTiecCuoi_WEB.Models.Notification;

public sealed class NotificationViewModel
{
    public int NotificationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string NavigationUrl { get; set; } = "/thong-bao";
}

public sealed class NotificationPageViewModel
{
    public List<NotificationViewModel> Items { get; set; } = [];
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int UnreadCount { get; set; }
    public string? ReadStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class NotificationCountViewModel { public int Count { get; set; } }
public sealed class NotificationUpdateViewModel { public string? Message { get; set; } public int Updated { get; set; } }

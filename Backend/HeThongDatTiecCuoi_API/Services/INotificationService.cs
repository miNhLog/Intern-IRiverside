using HeThongDatTiecCuoi_API.DTOs.Notification;

namespace HeThongDatTiecCuoi_API.Services;

public interface INotificationService
{
    Task<bool> AddAsync(int recipientUserId, int? actorUserId, string type, string title, string message, string? relatedEntityType, int? relatedEntityId, CancellationToken cancellationToken);
    Task<int?> FindActiveAdminUserIdAsync(CancellationToken cancellationToken);
    Task<List<NotificationDto>> GetRecentAsync(int userId, int limit, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken);
    Task<NotificationPageDto> GetPageAsync(int userId, int page, int pageSize, bool? isRead, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken);
}

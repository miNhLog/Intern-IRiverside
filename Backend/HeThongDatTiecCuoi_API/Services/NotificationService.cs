using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Notification;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    public NotificationService(ApplicationDbContext context) => _context = context;

    public Task<int?> FindActiveAdminUserIdAsync(CancellationToken cancellationToken) =>
        _context.Users.AsNoTracking().Where(user => user.Role.RoleName == RoleNames.Admin &&
            user.Status.StatusCode == AccountStatusCodes.Active && user.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(user => (int?)user.UserId).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AddAsync(int recipientUserId, int? actorUserId, string type, string title, string message,
        string? relatedEntityType, int? relatedEntityId, CancellationToken cancellationToken)
    {
        if (actorUserId == recipientUserId) return Task.FromResult(false);
        _context.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId, Type = type, Title = title, Message = message,
            RelatedEntityType = relatedEntityType, RelatedEntityId = relatedEntityId,
            IsRead = false, CreatedAt = DateTime.Now
        });
        return Task.FromResult(true);
    }

    public Task<List<NotificationDto>> GetRecentAsync(int userId, int limit, CancellationToken cancellationToken) =>
        _context.Notifications.AsNoTracking().Where(item => item.RecipientUserId == userId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.NotificationId)
            .Take(Math.Clamp(limit, 1, 5)).Select(ToDto()).ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken) =>
        _context.Notifications.CountAsync(item => item.RecipientUserId == userId && !item.IsRead, cancellationToken);

    public async Task<NotificationPageDto> GetPageAsync(int userId, int page, int pageSize, bool? isRead, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.Notifications.AsNoTracking().Where(item => item.RecipientUserId == userId);
        if (isRead.HasValue) query = query.Where(item => item.IsRead == isRead.Value);
        var total = await query.CountAsync(cancellationToken);
        var pages = (int)Math.Ceiling(total / (double)pageSize);
        if (pages > 0 && page > pages) page = pages;
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.NotificationId)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto()).ToListAsync(cancellationToken);
        return new NotificationPageDto { Items = items, CurrentPage = page, PageSize = pageSize, TotalItems = total,
            TotalPages = pages, UnreadCount = await GetUnreadCountAsync(userId, cancellationToken) };
    }

    public async Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken)
    {
        var item = await _context.Notifications.SingleOrDefaultAsync(x => x.NotificationId == notificationId && x.RecipientUserId == userId, cancellationToken);
        if (item is null) return false;
        if (!item.IsRead) { item.IsRead = true; item.ReadAt = DateTime.Now; await _context.SaveChangesAsync(cancellationToken); }
        return true;
    }

    public Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken) =>
        _context.Notifications.Where(item => item.RecipientUserId == userId && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true).SetProperty(item => item.ReadAt, DateTime.Now), cancellationToken);

    private static System.Linq.Expressions.Expression<Func<Notification, NotificationDto>> ToDto() => item => new NotificationDto
    {
        NotificationId = item.NotificationId, Type = item.Type, Title = item.Title, Message = item.Message,
        RelatedEntityType = item.RelatedEntityType, RelatedEntityId = item.RelatedEntityId, IsRead = item.IsRead,
        ReadAt = item.ReadAt, CreatedAt = item.CreatedAt,
        NavigationUrl = item.Type == NotificationTypeCodes.RoleChangeRequestCreated ? "/quan-tri/yeu-cau-thay-doi-vai-tro" :
            item.Type == NotificationTypeCodes.RoleChangeApproved || item.Type == NotificationTypeCodes.RoleChangeRejected ? "/quan-ly/yeu-cau-thay-doi-vai-tro" :
            item.Type == NotificationTypeCodes.FirstPasswordChanged ? "/admin/quan-ly-tai-khoan" : "/thong-bao"
    };
}

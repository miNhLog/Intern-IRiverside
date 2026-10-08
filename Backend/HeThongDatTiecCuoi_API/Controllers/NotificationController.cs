using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Notification;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationController(INotificationService notifications) => _notifications = notifications;

    [HttpGet("recent")]
    public async Task<ActionResult<List<NotificationDto>>> Recent(int limit = 5, CancellationToken cancellationToken = default) =>
        Ok(await _notifications.GetRecentAsync(CurrentUserId(), limit, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new { count = await _notifications.GetUnreadCountAsync(CurrentUserId(), cancellationToken) });

    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> GetPage(int page = 1, int pageSize = 20, string? readStatus = null, CancellationToken cancellationToken = default)
    {
        bool? isRead = readStatus?.Trim().ToUpperInvariant() switch { "READ" => true, "UNREAD" => false, null or "" => null, _ => null };
        if (!string.IsNullOrWhiteSpace(readStatus) && isRead is null) return BadRequest(new { message = "Trạng thái đọc không hợp lệ." });
        return Ok(await _notifications.GetPageAsync(CurrentUserId(), page, pageSize, isRead, cancellationToken));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken) =>
        await _notifications.MarkReadAsync(CurrentUserId(), id, cancellationToken) ? Ok(new { message = "Đã đánh dấu thông báo là đã đọc." }) : NotFound(new { message = "Không tìm thấy thông báo." });

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        Ok(new { updated = await _notifications.MarkAllReadAsync(CurrentUserId(), cancellationToken) });

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Không xác định được tài khoản.");
}

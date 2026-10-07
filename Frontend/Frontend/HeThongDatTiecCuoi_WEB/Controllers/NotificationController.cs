using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Notification;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
[Route("thong-bao")]
public sealed class NotificationController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public NotificationController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, string? readStatus = null, CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[TokenCookie]; if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _api.GetNotificationsAsync(token, page, 20, readStatus, cancellationToken);
        var model = result.Value ?? new NotificationPageViewModel { CurrentPage = Math.Max(1, page), PageSize = 20 };
        model.ReadStatus = readStatus; if (!result.Succeeded) model.ErrorMessage = result.Error;
        return View(model);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken)
    { var token = Request.Cookies[TokenCookie]; if (string.IsNullOrWhiteSpace(token)) return Unauthorized(); var result = await _api.GetRecentNotificationsAsync(token, 5, cancellationToken); return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 500, new { message = result.Error }); }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    { var token = Request.Cookies[TokenCookie]; if (string.IsNullOrWhiteSpace(token)) return Unauthorized(); var result = await _api.GetUnreadNotificationCountAsync(token, cancellationToken); return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 500, new { message = result.Error }); }

    [HttpPost("{id:int}/read")][ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    { var token = Request.Cookies[TokenCookie]; if (string.IsNullOrWhiteSpace(token)) return Unauthorized(); var result = await _api.MarkNotificationReadAsync(token, id, cancellationToken); return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 500, new { message = result.Error }); }

    [HttpPost("read-all")][ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    { var token = Request.Cookies[TokenCookie]; if (string.IsNullOrWhiteSpace(token)) return Unauthorized(); var result = await _api.MarkAllNotificationsReadAsync(token, cancellationToken); return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 500, new { message = result.Error }); }
}

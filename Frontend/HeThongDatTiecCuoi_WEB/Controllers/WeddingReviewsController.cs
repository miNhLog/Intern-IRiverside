using System.Security.Cryptography;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Review;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("danh-gia")]
public sealed class WeddingReviewsController(IRiversideApiClient api, ILogger<WeddingReviewsController> logger) : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private const string ReviewSessionCookie = "rp_review_session";

    // UC22: anonymous guest form opened from the review QR; no account required.
    [AllowAnonymous]
    [HttpGet("khach-moi/{token}")]
    public async Task<IActionResult> Guest(string token, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        var result = await api.GetGuestReviewAsync(token, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusPage(result.StatusCode ?? StatusCodes.Status502BadGateway,
                "Không thể mở phiếu đánh giá", result.Error ?? "Vui lòng thử lại sau ít phút.");

        var model = result.Value;
        model.Token = token;
        return View("Guest", model);
    }

    [AllowAnonymous]
    [HttpPost("khach-moi/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guest(string token, GuestReviewScoresViewModel scores, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        var form = await api.GetGuestReviewAsync(token, cancellationToken);
        if (!form.Succeeded || form.Value is null)
            return StatusPage(form.StatusCode ?? StatusCodes.Status502BadGateway,
                "Không thể mở phiếu đánh giá", form.Error ?? "Vui lòng thử lại sau ít phút.");

        var model = form.Value;
        model.Token = token;
        model.Scores = scores;

        if (!ModelState.IsValid)
        {
            model.Submitted = true;
            model.Message = "Vui lòng chấm đủ 6 tiêu chí từ 1 đến 5.";
            return View("Guest", model);
        }

        var submitted = await api.SubmitGuestReviewAsync(token, scores, EnsureReviewSession(), cancellationToken);
        if (submitted.Succeeded && submitted.Value is not null)
        {
            return View("Status", new ReviewStatusViewModel
            {
                Title = submitted.Value.Duplicate ? "Đánh giá đã được ghi nhận" : "Cảm ơn bạn đã đánh giá",
                Message = submitted.Value.Message ?? "Cảm ơn bạn đã chia sẻ trải nghiệm về buổi tiệc."
            });
        }

        logger.LogWarning("Gửi đánh giá khách mời thất bại. traceId={TraceId} status={Status}",
            HttpContext.TraceIdentifier, submitted.StatusCode);
        model.Submitted = true;
        model.Message = submitted.Error ?? "Không thể gửi đánh giá lúc này. Vui lòng thử lại.";
        return View("Guest", model);
    }

    // UC21: only the coordinator assigned to this booking may issue or preview its review QR.
    [Authorize(Roles = RoleNames.Coordinator)]
    [HttpPost("dieu-phoi/{bookingId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueForCoordinator(int bookingId, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(new { message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
        var result = await api.IssueCoordinatorReviewQrAsync(token, bookingId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway,
                new { message = result.Error ?? "Không thể phát hành mã QR đánh giá." });

        var qr = result.Value;
        return Json(new
        {
            success = true,
            bookingId = qr.BookingId,
            bookingCode = qr.BookingCode,
            reviewUrl = $"{Request.Scheme}://{Request.Host}/danh-gia/khach-moi/{Uri.EscapeDataString(qr.Token)}",
            qrImageUrl = $"/danh-gia/dieu-phoi/{qr.BookingId}/qr.png",
            expiresAt = qr.ExpiresAt,
            created = qr.Created
        });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    [HttpGet("dieu-phoi/{bookingId:int}/qr.png")]
    public async Task<IActionResult> CoordinatorQrImage(int bookingId, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await api.GetCoordinatorReviewQrPngAsync(token, bookingId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway);
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return File(result.Value, "image/png");
    }

    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("quan-ly/{bookingId:int}/qr.png")]
    public async Task<IActionResult> ManagementQrImage(int bookingId, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await api.GetManagementReviewQrPngAsync(token, bookingId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway);
        return File(result.Value, "image/png");
    }

    private ViewResult StatusPage(int statusCode, string title, string message)
    {
        Response.StatusCode = statusCode;
        return View("Status", new ReviewStatusViewModel { Title = title, Message = message });
    }

    // A random, stable per-browser identifier; only its hash reaches the API.
    private string EnsureReviewSession()
    {
        var existing = Request.Cookies[ReviewSessionCookie];
        if (!string.IsNullOrWhiteSpace(existing) && existing.Length is >= 16 and <= 80) return existing;

        var created = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Response.Cookies.Append(ReviewSessionCookie, created, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(180),
            IsEssential = true
        });
        return created;
    }
}

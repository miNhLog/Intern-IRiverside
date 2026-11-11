using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Review;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class WeddingReviewsController(
    ApplicationDbContext context,
    IReviewQrService qrService,
    IReviewStatisticsService statisticsService,
    IOptions<ReviewQrOptions> options) : ControllerBase
{
    private const string DuplicateMessage = "Đánh giá đã được ghi nhận. Cảm ơn bạn!";
    [AllowAnonymous]
    [HttpGet("guest/{token}")]
    public async Task<IActionResult> GuestForm(string token, CancellationToken ct) =>
        await BuildFormAsync(token, null, false, ct) is { } form ? Ok(form) : NotFound(new { message = "Mã đánh giá không tồn tại." });

    [AllowAnonymous]
    [EnableRateLimiting("wedding-review")]
    [HttpPost("guest/{token}")]
    public async Task<IActionResult> SubmitGuest(string token, SubmitWeddingReviewRequest request, CancellationToken ct)
    {
        var qr = await QueryQr().SingleOrDefaultAsync(q => q.QrCode == token, ct);
        if (qr is null) return NotFound(new { message = "Mã đánh giá không tồn tại." });
        var denied = ValidateWindow(qr, guest: true);
        if (denied is not null) return denied;
        if (!TryGuestScores(request, out var scores))
            return BadRequest(new { code = "REVIEW_SCORES_REQUIRED", message = "Vui lòng chấm đủ 6 tiêu chí từ 1 đến 5." });
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest(new { code = "REVIEW_SESSION_REQUIRED", message = "Không xác định được phiên đánh giá. Vui lòng tải lại trang." });

        var sessionHash = HashSession(token, request.SessionId);
        if (await IsDuplicateAsync(qr.ReviewQrCodeId, sessionHash, ct))
            return Ok(new { success = true, duplicate = true, message = DuplicateMessage });

        var review = ToGuestEntity(qr.ReviewQrCodeId, sessionHash, scores, request);
        context.WeddingReviews.Add(review);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (await IsDuplicateAsync(qr.ReviewQrCodeId, sessionHash, ct))
                return Ok(new { success = true, duplicate = true, message = DuplicateMessage });
            throw;
        }
        return Ok(new { success = true, message = "Cảm ơn bạn đã đánh giá buổi tiệc!" });
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpGet("host/{bookingCode}")]
    public async Task<IActionResult> HostForm(string bookingCode, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var form = await BuildFormAsync(null, bookingCode, true, ct, userId);
        return form is null ? NotFound(new { message = "Không tìm thấy tiệc thuộc tài khoản của bạn." }) : Ok(form);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [EnableRateLimiting("wedding-review")]
    [HttpPost("host/{bookingCode}")]
    public async Task<IActionResult> SubmitHost(string bookingCode, SubmitWeddingReviewRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var qr = await QueryQr().SingleOrDefaultAsync(q =>
            q.Booking.BookingCode == bookingCode && q.Booking.Customer.UserId == userId, ct);
        if (qr is null) return NotFound(new { message = "Không tìm thấy tiệc thuộc tài khoản của bạn." });
        var denied = ValidateWindow(qr, guest: false);
        if (denied is not null) return denied;
        if (request.OverallScore is < 1 or > 5)
            return BadRequest(new { code = "REVIEW_SCORES_REQUIRED", message = "Vui lòng chấm điểm tổng thể từ 1 đến 5." });

        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await context.WeddingReviews.AnyAsync(r =>
            r.ReviewQrCodeId == qr.ReviewQrCodeId && r.ReviewerType == ReviewTypes.Host, ct))
            return Conflict(new { message = "Chủ tiệc đã đánh giá buổi tiệc này." });
        context.WeddingReviews.Add(ToEntity(qr.ReviewQrCodeId, ReviewTypes.Host, request));
        try
        {
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { message = "Chủ tiệc đã đánh giá buổi tiệc này." });
        }
        return Ok(new { success = true, message = "Cảm ơn bạn đã đánh giá buổi tiệc!" });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    [HttpPost("coordinator/{bookingId:int}/issue")]
    public async Task<ActionResult<ReviewQrIssueDto>> IssueQr(int bookingId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var result = await qrService.IssueForCoordinatorAsync(userId, bookingId, ct);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode, new { message = result.Error });
        var dto = result.Value;
        return Ok(dto with { ReviewUrl = GuestUrl(dto.Token) });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    [HttpGet("coordinator/{bookingId:int}/qr.png")]
    public async Task<IActionResult> CoordinatorQrImage(int bookingId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var result = await qrService.GetForCoordinatorAsync(userId, bookingId, ct);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode, new { message = result.Error });
        return QrPng(result.Value.Token, result.Value.BookingCode);
    }

    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("management")]
    public async Task<ActionResult<IReadOnlyList<ReviewQrListItemDto>>> Management(CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var query = context.WeddingBookings.AsNoTracking()
            .Include(b => b.Status).Include(b => b.Customer)
            .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Status)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Reviews)
            .AsQueryable();
        if (User.IsInRole(RoleNames.HallManager))
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(context, userId, RiversideBusinessClock.Today);
            query = query.Where(b => hallIds.Contains(b.HallSchedule.HallId));
        }
        var rows = await query.OrderByDescending(b => b.HallSchedule.Date).ToListAsync(ct);
        return Ok(rows.Select(ToListItem).ToList());
    }

    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("management/{bookingId:int}/qr.png")]
    public async Task<IActionResult> QrImage(int bookingId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var query = QueryQr().AsNoTracking().Where(q => q.BookingId == bookingId);
        if (User.IsInRole(RoleNames.HallManager))
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(context, userId, RiversideBusinessClock.Today);
            query = query.Where(q => hallIds.Contains(q.Booking.HallSchedule.HallId));
        }
        var qr = await query.SingleOrDefaultAsync(ct);
        return qr is null ? NotFound() : QrPng(qr.QrCode, qr.Booking.BookingCode);
    }

    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("management/{bookingId:int}")]
    public async Task<ActionResult<ReviewStatisticsDto>> Statistics(int bookingId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var query = context.WeddingBookings.AsNoTracking()
            .Include(b => b.Status).Include(b => b.Customer)
            .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Status)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Reviews)
            .Where(b => b.BookingId == bookingId);
        if (User.IsInRole(RoleNames.HallManager))
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(context, userId, RiversideBusinessClock.Today);
            query = query.Where(b => hallIds.Contains(b.HallSchedule.HallId));
        }
        var booking = await query.SingleOrDefaultAsync(ct);
        if (booking is null) return NotFound(new { message = "Không tìm thấy dữ liệu đánh giá trong phạm vi phụ trách." });
        var locked = booking.ReviewQrCode?.Status.StatusCode == "LOCKED" || booking.Status.StatusCode == BookingStatusCodes.Cancelled;
        var window = qrService.GetWindow(booking.HallSchedule, locked);
        var reviews = booking.ReviewQrCode?.Reviews.OrderByDescending(r => r.ReviewedAt).ToList() ?? [];
        double? Average(IEnumerable<int?> values)
        {
            var scores = values.Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
            return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
        }
        var distribution = Enumerable.Range(1, 5).ToDictionary(
            score => score, score => reviews.Count(r => r.OverallScore == score));
        return Ok(new ReviewStatisticsDto(
            booking.BookingId, booking.BookingCode, booking.Customer.FullName,
            booking.HallSchedule.Hall.HallName, booking.HallSchedule.Date, booking.HallSchedule.Shift,
            window.State.ToString(), window.OpensAt, window.ExpiresAt, reviews.Count,
            reviews.Count(r => r.ReviewerType == ReviewTypes.Guest),
            reviews.Count(r => r.ReviewerType == ReviewTypes.Host),
            reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.OverallScore), 2),
            Average(reviews.Select(r => r.HallScore)), Average(reviews.Select(r => r.FoodScore)),
            Average(reviews.Select(r => r.ServiceScore)), Average(reviews.Select(r => r.SoundScore)),
            Average(reviews.Select(r => r.LightingScore)), Average(reviews.Select(r => r.HygieneScore)), distribution,
            reviews.Select(r => new ReviewCommentDto(r.ReviewerType, r.OverallScore, r.Comment, r.ReviewedAt)).ToList()));
    }

    private IQueryable<ReviewQrCode> QueryQr() => context.ReviewQrCodes
        .Include(q => q.Status).Include(q => q.Reviews)
        .Include(q => q.Booking).ThenInclude(b => b.Status)
        .Include(q => q.Booking).ThenInclude(b => b.Customer)
        .Include(q => q.Booking).ThenInclude(b => b.HallSchedule).ThenInclude(s => s.Hall);

    private IActionResult? ValidateWindow(ReviewQrCode qr, bool guest)
    {
        var locked = qr.Status.StatusCode == "LOCKED" || qr.Booking.Status.StatusCode == BookingStatusCodes.Cancelled;
        var window = guest
            ? qrService.GetGuestWindow(qr.Booking.HallSchedule, qr.CreatedAt, locked)
            : qrService.GetWindow(qr.Booking.HallSchedule, locked);
        return window.State switch
        {
            ReviewQrState.Active => null,
            ReviewQrState.Pending => Conflict(new { code = "REVIEW_NOT_OPEN", message = "Chức năng đánh giá sẽ mở khi buổi tiệc bắt đầu.", opensAt = window.OpensAt }),
            ReviewQrState.Expired => StatusCode(StatusCodes.Status410Gone, new { code = "REVIEW_EXPIRED", message = "Mã đánh giá đã hết hạn." }),
            _ => StatusCode(StatusCodes.Status423Locked, new { code = "REVIEW_LOCKED", message = "Mã đánh giá đã bị khóa." })
        };
    }

    private async Task<ReviewFormDto?> BuildFormAsync(string? token, string? bookingCode, bool host, CancellationToken ct, int userId = 0)
    {
        var query = QueryQr().AsNoTracking();
        var qr = host
            ? await query.SingleOrDefaultAsync(q => q.Booking.BookingCode == bookingCode && q.Booking.Customer.UserId == userId, ct)
            : await query.SingleOrDefaultAsync(q => q.QrCode == token, ct);
        if (qr is null) return null;
        var locked = qr.Status.StatusCode == "LOCKED" || qr.Booking.Status.StatusCode == BookingStatusCodes.Cancelled;
        var window = host
            ? qrService.GetWindow(qr.Booking.HallSchedule, locked)
            : qrService.GetGuestWindow(qr.Booking.HallSchedule, qr.CreatedAt, locked);
        var reviewed = host && qr.Reviews.Any(r => r.ReviewerType == ReviewTypes.Host);
        var message = window.State switch
        {
            ReviewQrState.Pending => "Chức năng đánh giá sẽ mở khi buổi tiệc bắt đầu.",
            ReviewQrState.Expired => "Thời gian đánh giá đã kết thúc.",
            ReviewQrState.Locked => "Mã đánh giá đã bị khóa.",
            _ when reviewed => "Bạn đã đánh giá buổi tiệc này.",
            _ => "Hãy chia sẻ trải nghiệm của bạn."
        };
        return new(qr.BookingId, qr.Booking.BookingCode, qr.Booking.HallSchedule.Hall.HallName,
            qr.Booking.HallSchedule.Date, qr.Booking.HallSchedule.Shift, window.State.ToString(), window.OpensAt,
            window.ExpiresAt, window.State == ReviewQrState.Active && !reviewed, message, reviewed);
    }

    private WeddingReview ToEntity(int qrId, string reviewerType, SubmitWeddingReviewRequest r) => new()
    {
        ReviewQrCodeId = qrId, ReviewerType = reviewerType, HallScore = r.HallScore, FoodScore = r.FoodScore,
        ServiceScore = r.ServiceScore, SoundScore = r.SoundScore, LightingScore = r.LightingScore,
        HygieneScore = r.HygieneScore, OverallScore = r.OverallScore, Comment = CleanComment(r.Comment),
        ReviewedAt = RiversideBusinessClock.Now
    };

    private WeddingReview ToGuestEntity(int qrId, string sessionHash, int[] scores, SubmitWeddingReviewRequest r) => new()
    {
        ReviewQrCodeId = qrId, ReviewerType = ReviewTypes.Guest,
        HallScore = scores[0], FoodScore = scores[1], ServiceScore = scores[2], SoundScore = scores[3],
        LightingScore = scores[4], HygieneScore = scores[5],
        OverallScore = (int)Math.Round(scores.Average(score => (double)score), MidpointRounding.AwayFromZero),
        Comment = CleanComment(r.Comment), SessionHash = sessionHash, ReviewedAt = RiversideBusinessClock.Now
    };

    private static bool TryGuestScores(SubmitWeddingReviewRequest r, out int[] scores)
    {
        scores =
        [
            r.HallScore ?? 0, r.FoodScore ?? 0, r.ServiceScore ?? 0,
            r.SoundScore ?? 0, r.LightingScore ?? 0, r.HygieneScore ?? 0
        ];
        return r.HallScore is >= 1 and <= 5 && r.FoodScore is >= 1 and <= 5 &&
            r.ServiceScore is >= 1 and <= 5 && r.SoundScore is >= 1 and <= 5 &&
            r.LightingScore is >= 1 and <= 5 && r.HygieneScore is >= 1 and <= 5;
    }

    private Task<bool> IsDuplicateAsync(int qrId, string sessionHash, CancellationToken ct) =>
        context.WeddingReviews.AsNoTracking().AnyAsync(r => r.ReviewQrCodeId == qrId &&
            r.ReviewerType == ReviewTypes.Guest && r.SessionHash == sessionHash, ct);

    private static string HashSession(string token, string sessionId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token + "|" + sessionId))).ToLowerInvariant();

    private IActionResult QrPng(string token, string bookingCode)
    {
        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(GuestUrl(token), QRCoder.QRCodeGenerator.ECCLevel.Q);
        var png = new QRCoder.PngByteQRCode(data).GetGraphic(12);
        return File(png, "image/png", $"QR-danh-gia-{bookingCode}.png");
    }

    private string? CleanComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        return clean.Length <= options.Value.CommentMaxLength ? clean : clean[..options.Value.CommentMaxLength];
    }

    private ReviewQrListItemDto ToListItem(WeddingBooking booking)
    {
        var qr = booking.ReviewQrCode;
        var locked = qr?.Status.StatusCode == "LOCKED" || booking.Status.StatusCode == BookingStatusCodes.Cancelled;
        var w = qrService.GetWindow(booking.HallSchedule, locked);
        var reviews = qr?.Reviews ?? [];
        return new(booking.BookingId, booking.BookingCode, booking.HallSchedule.Hall.HallName,
            booking.HallSchedule.Date, booking.HallSchedule.Shift, qr?.QrCode, qr is null ? null : GuestUrl(qr.QrCode),
            w.State.ToString(), w.OpensAt, w.ExpiresAt, reviews.Count,
            reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.OverallScore), 2),
            reviews.OrderByDescending(r => r.ReviewedAt)
                .Select(r => new ReviewCommentDto(r.ReviewerType, r.OverallScore, r.Comment, r.ReviewedAt)).ToList());
    }

    private string GuestUrl(string token) => $"{options.Value.PublicBaseUrl.TrimEnd('/')}/danh-gia/khach-moi/{token}";
    private bool TryUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("statistics/overview")]
    public async Task<ActionResult<ReviewOverviewDto>> Overview(
        [FromQuery] string? search, [FromQuery] int? hallId, [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate, [FromQuery] string? qrState, [FromQuery] string? reviewerType,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        return Ok(await statisticsService.GetOverviewAsync(userId, User.IsInRole(RoleNames.HallManager),
            search, hallId, fromDate, toDate, qrState, reviewerType, page, pageSize, ct));
    }

    [Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
    [HttpGet("statistics/bookings/{bookingId:int}")]
    public async Task<ActionResult<ReviewBookingDetailStatisticsDto>> BookingStatistics(int bookingId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var result = await statisticsService.GetBookingAsync(
            bookingId, userId, User.IsInRole(RoleNames.HallManager), ct);
        return result is null ? NotFound(new { message = "Không tìm thấy Booking trong phạm vi phụ trách." }) : Ok(result);
    }

}

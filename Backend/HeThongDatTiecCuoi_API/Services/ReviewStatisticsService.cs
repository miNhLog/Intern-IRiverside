using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Review;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public interface IReviewStatisticsService
{
    Task<ReviewOverviewDto> GetOverviewAsync(int userId, bool hallManager, string? search, int? hallId,
        DateTime? fromDate, DateTime? toDate, string? qrState, string? reviewerType,
        int page, int pageSize, CancellationToken ct);
    Task<ReviewBookingDetailStatisticsDto?> GetBookingAsync(int bookingId, int userId, bool hallManager, CancellationToken ct);
}

public sealed class ReviewStatisticsService(ApplicationDbContext context, IReviewQrService qrService)
    : IReviewStatisticsService
{
    public async Task<ReviewOverviewDto> GetOverviewAsync(int userId, bool hallManager, string? search, int? hallId,
        DateTime? fromDate, DateTime? toDate, string? qrState, string? reviewerType,
        int page, int pageSize, CancellationToken ct)
    {
        var scopedQuery = ScopedBookings(userId, hallManager);
        List<ReviewHallOptionDto> halls;
        if (hallManager)
        {
            var assignedHallIds = HallManagerQueryScope.CurrentHallIds(context, userId, RiversideBusinessClock.Today);
            halls = await context.Halls.AsNoTracking()
                .Where(h => assignedHallIds.Contains(h.HallId))
                .OrderBy(h => h.HallName)
                .Select(h => new ReviewHallOptionDto(h.HallId, h.HallName))
                .ToListAsync(ct);
        }
        else
        {
            halls = await scopedQuery
                .Select(b => new ReviewHallOptionDto(b.HallSchedule.HallId, b.HallSchedule.Hall.HallName))
                .Distinct().OrderBy(h => h.HallName).ToListAsync(ct);
        }

        if (hallManager && (hallId is not > 0 || halls.All(h => h.HallId != hallId.Value)))
            hallId = halls.FirstOrDefault()?.HallId;

        var query = scopedQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.BookingCode.Contains(term) || b.HallSchedule.Hall.HallName.Contains(term));
        }
        if (hallId is > 0) query = query.Where(b => b.HallSchedule.HallId == hallId);
        if (fromDate.HasValue) query = query.Where(b => b.HallSchedule.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(b => b.HallSchedule.Date < toDate.Value.Date.AddDays(1));

        var bookings = await query.OrderByDescending(b => b.HallSchedule.Date).AsSplitQuery().ToListAsync(ct);
        var projected = bookings.Select(b => new
        {
            Booking = b,
            Window = Window(b),
            Reviews = (b.ReviewQrCode?.Reviews ?? [])
                .Where(r => string.IsNullOrWhiteSpace(reviewerType) || r.ReviewerType == reviewerType).ToList()
        }).ToList();
        if (!string.IsNullOrWhiteSpace(qrState))
            projected = projected.Where(x => x.Window.State.ToString().Equals(qrState, StringComparison.OrdinalIgnoreCase)).ToList();

        var reviews = projected.SelectMany(x => x.Reviews).ToList();
        var total = reviews.Count;
        double? Rate(Func<WeddingReview, bool> predicate) => CalculateRate(reviews.Count(predicate), total);
        var distribution = Enumerable.Range(1, 5).ToDictionary(score => score, score => reviews.Count(r => r.OverallScore == score));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 50);
        var bookingRows = projected.Select(x => new ReviewOverviewBookingDto(
                x.Booking.BookingId, x.Booking.BookingCode, x.Booking.HallSchedule.Hall.HallName,
                x.Booking.HallSchedule.HallId, x.Booking.HallSchedule.Date, x.Booking.HallSchedule.Shift,
                x.Booking.Status.StatusCode, x.Window.State.ToString(), x.Reviews.Count,
                x.Reviews.Count > 0 ? x.Reviews.Average(r => r.OverallScore) : null)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(bookingRows.Count / (double)pageSize));
        page = Math.Min(page, totalPages);
        return new ReviewOverviewDto(
            projected.Count, projected.Count(x => x.Reviews.Count > 0), total,
            total == 0 ? null : Math.Round(reviews.Average(r => r.OverallScore), 2),
            Rate(r => r.OverallScore >= 4), Rate(r => r.OverallScore == 3), Rate(r => r.OverallScore <= 2),
            distribution, Criteria(reviews), bookingRows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            projected.SelectMany(x => x.Reviews
                    .Where(r => r.OverallScore <= 2)
                    .Select(r => new AttentionReviewDto(x.Booking.BookingId, x.Booking.BookingCode,
                        x.Booking.HallSchedule.Hall.HallName, r.OverallScore, r.Comment, r.ReviewedAt)))
                .OrderByDescending(r => r.ReviewedAt).Take(5).ToList(), halls, hallId, page, pageSize, totalPages);
    }

    public async Task<ReviewBookingDetailStatisticsDto?> GetBookingAsync(
        int bookingId, int userId, bool hallManager, CancellationToken ct)
    {
        var booking = await ScopedBookings(userId, hallManager)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId, ct);
        if (booking is null) return null;
        var reviews = booking.ReviewQrCode?.Reviews.OrderByDescending(r => r.ReviewedAt).ToList() ?? [];
        var total = reviews.Count;
        var positive = reviews.Count(r => r.OverallScore >= 4);
        var neutral = reviews.Count(r => r.OverallScore == 3);
        var negative = reviews.Count(r => r.OverallScore <= 2);
        return new ReviewBookingDetailStatisticsDto(
            booking.BookingId, booking.BookingCode, booking.HallSchedule.Hall.HallName,
            booking.HallSchedule.Date, booking.HallSchedule.Shift, Window(booking).State.ToString(),
            total, reviews.Count(r => r.ReviewerType == ReviewTypes.Guest),
            reviews.Count(r => r.ReviewerType == ReviewTypes.Host), positive, neutral, negative,
            total == 0 ? null : Math.Round(reviews.Average(r => r.OverallScore), 2),
            total == 0 ? null : Math.Round(positive * 100d / total, 1),
            Enumerable.Range(1, 5).ToDictionary(score => score, score => reviews.Count(r => r.OverallScore == score)),
            Criteria(reviews), reviews.Select(r => new ReviewFeedbackDto(
                r.ReviewId, r.ReviewerType, r.OverallScore, r.HallScore, r.FoodScore, r.ServiceScore,
                r.SoundScore, r.LightingScore, r.Comment, r.ReviewedAt, ClassifyScore(r.OverallScore))).ToList());
    }

    private IQueryable<WeddingBooking> ScopedBookings(int userId, bool hallManager)
    {
        var query = context.WeddingBookings.AsNoTracking()
            .Include(b => b.Status).Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Status)
            .Include(b => b.ReviewQrCode!).ThenInclude(q => q.Reviews).AsQueryable();
        if (hallManager)
        {
            var hallIds = HallManagerQueryScope.CurrentHallIds(context, userId, RiversideBusinessClock.Today);
            query = query.Where(b => hallIds.Contains(b.HallSchedule.HallId));
        }
        return query;
    }

    private ReviewWindow Window(WeddingBooking booking) => qrService.GetWindow(booking.HallSchedule,
        booking.Status.StatusCode == BookingStatusCodes.Cancelled || booking.ReviewQrCode?.Status.StatusCode == "LOCKED");

    private static ReviewCriterionAveragesDto Criteria(IReadOnlyCollection<WeddingReview> reviews) => new(
        Average(reviews.Select(r => r.HallScore)), Average(reviews.Select(r => r.FoodScore)),
        Average(reviews.Select(r => r.ServiceScore)), Average(reviews.Select(r => r.SoundScore)),
        Average(reviews.Select(r => r.LightingScore)));

    private static double? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : Math.Round(present.Average(), 2);
    }

    public static string ClassifyScore(int score) => score >= 4 ? "Positive" : score == 3 ? "Neutral" : "Negative";
    public static double? CalculateRate(int matching, int total) => total <= 0 ? null : Math.Round(matching * 100d / total, 1);
}

using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public interface IReviewQrEligibilityPolicy
{
    bool IsEligible(string bookingStatusCode, HallSchedule schedule);
}

public sealed class ReviewQrEligibilityPolicy(IOptions<ReviewQrOptions> options) : IReviewQrEligibilityPolicy
{
    public bool IsEligible(string bookingStatusCode, HallSchedule schedule)
    {
        // Extension point: when enabled later, evaluate successful payments here via a dedicated provider.
        // The current release deliberately has no PayOS/payment dependency.
        if (options.Value.PaymentGateEnabled) return false; // fail closed until a payment obligation provider is wired here
        return bookingStatusCode != BookingStatusCodes.Cancelled && ChecklistTimingRules.Window(schedule) is not null;
    }
}

public enum ReviewQrState { Pending, Active, Expired, Locked }

public sealed record ReviewWindow(DateTime OpensAt, DateTime ExpiresAt, ReviewQrState State);

public interface IReviewQrService
{
    ReviewWindow GetWindow(HallSchedule schedule, bool locked);
    Task<ReviewQrCode?> EnsureForBookingAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<int> BackfillAsync(CancellationToken cancellationToken = default);
}

public sealed class ReviewQrService(
    ApplicationDbContext context,
    IReviewQrEligibilityPolicy policy,
    ILogger<ReviewQrService> logger) : IReviewQrService
{
    public ReviewWindow GetWindow(HallSchedule schedule, bool locked)
    {
        var raw = ChecklistTimingRules.Window(schedule)
            ?? throw new InvalidOperationException("Ca tổ chức không hợp lệ.");
        var expires = raw.End.AddHours(24);
        var now = RiversideBusinessClock.Now;
        var state = locked ? ReviewQrState.Locked
            : now < raw.Start ? ReviewQrState.Pending
            : now <= expires ? ReviewQrState.Active
            : ReviewQrState.Expired;
        return new(raw.Start, expires, state);
    }

    public async Task<ReviewQrCode?> EnsureForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var existing = await context.ReviewQrCodes.SingleOrDefaultAsync(q => q.BookingId == bookingId, cancellationToken);
        if (existing is not null) return existing;

        var booking = await context.WeddingBookings.AsNoTracking()
            .Include(b => b.Status).Include(b => b.HallSchedule)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        if (booking is null || !policy.IsEligible(booking.Status.StatusCode, booking.HallSchedule)) return null;

        var window = GetWindow(booking.HallSchedule, false);
        var now = RiversideBusinessClock.Now;
        if (window.ExpiresAt <= now) return null; // never extend an already expired historical booking

        var activeStatusId = await context.Statuses.AsNoTracking()
            .Where(s => s.StatusGroup == StatusGroups.QrReview && s.StatusCode == "ACTIVE")
            .Select(s => s.StatusId).SingleAsync(cancellationToken);
        var qr = new ReviewQrCode
        {
            BookingId = bookingId,
            QrCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            CreatedAt = now,
            ExpiresAt = window.ExpiresAt,
            StatusId = activeStatusId
        };
        context.ReviewQrCodes.Add(qr);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return qr;
        }
        catch (DbUpdateException ex)
        {
            context.Entry(qr).State = EntityState.Detached;
            var winner = await context.ReviewQrCodes.SingleOrDefaultAsync(q => q.BookingId == bookingId, cancellationToken);
            if (winner is not null) return winner;
            logger.LogError(ex, "Không thể tạo QR đánh giá cho booking {BookingId}", bookingId);
            throw;
        }
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var ids = await context.WeddingBookings.AsNoTracking()
            .Where(b => b.ReviewQrCode == null && b.Status.StatusCode != BookingStatusCodes.Cancelled)
            .Where(b => b.HallSchedule.Date >= RiversideBusinessClock.Today.AddDays(-2))
            .Select(b => b.BookingId).ToListAsync(cancellationToken);
        var created = 0;
        foreach (var id in ids)
        {
            try { if (await EnsureForBookingAsync(id, cancellationToken) is not null) created++; }
            catch (Exception ex) { logger.LogError(ex, "Backfill QR thất bại cho booking {BookingId}", id); }
        }
        return created;
    }
}

public sealed class ReviewQrBackfillWorker(IServiceScopeFactory scopeFactory, ILogger<ReviewQrBackfillWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IReviewQrService>().BackfillAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Đã tạo bù {Count} QR đánh giá.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Tác vụ tạo bù QR đánh giá thất bại."); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

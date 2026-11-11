using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Review;
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
        return bookingStatusCode == BookingStatusCodes.Preparing && ChecklistTimingRules.Window(schedule) is not null;
    }
}

public enum ReviewQrState { Pending, Active, Expired, Locked }

public sealed record ReviewWindow(DateTime OpensAt, DateTime ExpiresAt, ReviewQrState State);

public interface IReviewQrService
{
    ReviewWindow GetWindow(HallSchedule schedule, bool locked);
    ReviewWindow GetGuestWindow(HallSchedule schedule, DateTime issuedAt, bool locked);
    Task<ServiceResult<ReviewQrIssueDto>> IssueForCoordinatorAsync(int userId, int bookingId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ReviewQrIssueDto>> GetForCoordinatorAsync(int userId, int bookingId, CancellationToken cancellationToken = default);
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

    public ReviewWindow GetGuestWindow(HallSchedule schedule, DateTime issuedAt, bool locked)
    {
        var raw = ChecklistTimingRules.Window(schedule)
            ?? throw new InvalidOperationException("Ca tổ chức không hợp lệ.");
        var expires = raw.End.AddHours(24);
        var now = RiversideBusinessClock.Now;
        var state = locked ? ReviewQrState.Locked
            : now < issuedAt ? ReviewQrState.Pending
            : now <= expires ? ReviewQrState.Active
            : ReviewQrState.Expired;
        return new(issuedAt, expires, state);
    }

    public async Task<ServiceResult<ReviewQrIssueDto>> IssueForCoordinatorAsync(int userId, int bookingId,
        CancellationToken cancellationToken = default)
    {
        var scoped = await ScopedBookingAsync(userId, bookingId, cancellationToken);
        if (scoped.Error is not null)
            return ServiceResult<ReviewQrIssueDto>.Failure(scoped.Error, scoped.StatusCode);
        var booking = scoped.Booking!;
        if (booking.Status.StatusCode != BookingStatusCodes.Preparing)
            return ServiceResult<ReviewQrIssueDto>.Failure(
                "Chỉ phát hành mã QR đánh giá khi tiệc đang chuẩn bị.", StatusCodes.Status409Conflict);
        if (!policy.IsEligible(booking.Status.StatusCode, booking.HallSchedule))
            return ServiceResult<ReviewQrIssueDto>.Failure(
                "Tiệc không đủ điều kiện phát hành mã QR đánh giá.", StatusCodes.Status409Conflict);

        var window = GetWindow(booking.HallSchedule, false);
        if (window.ExpiresAt <= RiversideBusinessClock.Now)
            return ServiceResult<ReviewQrIssueDto>.Failure(
                "Thời gian đánh giá của tiệc đã kết thúc.", StatusCodes.Status409Conflict);

        var existing = await context.ReviewQrCodes.AsNoTracking()
            .SingleOrDefaultAsync(q => q.BookingId == bookingId, cancellationToken);
        if (existing is not null) return Issue(existing, booking.BookingCode, false);

        var activeStatusId = await context.Statuses.AsNoTracking()
            .Where(s => s.StatusGroup == StatusGroups.QrReview && s.StatusCode == "ACTIVE")
            .Select(s => s.StatusId).SingleAsync(cancellationToken);
        var now = RiversideBusinessClock.Now;
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
            return Issue(qr, booking.BookingCode, true);
        }
        catch (DbUpdateException ex)
        {
            context.Entry(qr).State = EntityState.Detached;
            var winner = await context.ReviewQrCodes.AsNoTracking()
                .SingleOrDefaultAsync(q => q.BookingId == bookingId, cancellationToken);
            if (winner is not null) return Issue(winner, booking.BookingCode, false);
            logger.LogError(ex, "Không thể tạo QR đánh giá cho booking {BookingId}", bookingId);
            throw;
        }
    }

    public async Task<ServiceResult<ReviewQrIssueDto>> GetForCoordinatorAsync(int userId, int bookingId,
        CancellationToken cancellationToken = default)
    {
        var scoped = await ScopedBookingAsync(userId, bookingId, cancellationToken);
        if (scoped.Error is not null)
            return ServiceResult<ReviewQrIssueDto>.Failure(scoped.Error, scoped.StatusCode);
        var booking = scoped.Booking!;

        var qr = await context.ReviewQrCodes.AsNoTracking()
            .SingleOrDefaultAsync(q => q.BookingId == bookingId, cancellationToken);
        return qr is null
            ? ServiceResult<ReviewQrIssueDto>.Failure(
                "Tiệc chưa có mã QR đánh giá.", StatusCodes.Status404NotFound)
            : Issue(qr, booking.BookingCode, false);
    }

    private static ServiceResult<ReviewQrIssueDto> Issue(ReviewQrCode qr, string bookingCode, bool created) =>
        ServiceResult<ReviewQrIssueDto>.Success(
            new(qr.BookingId, bookingCode, qr.QrCode, string.Empty, qr.ExpiresAt, created));

    private async Task<(WeddingBooking? Booking, string? Error, int StatusCode)> ScopedBookingAsync(
        int userId, int bookingId, CancellationToken cancellationToken)
    {
        var employeeId = await ActiveCoordinatorEmployeeIdAsync(userId, cancellationToken);
        if (employeeId is null)
            return (null, "Không tìm thấy hồ sơ Điều phối viên hợp lệ.", StatusCodes.Status403Forbidden);

        var booking = await context.WeddingBookings.AsNoTracking()
            .Include(b => b.Status)
            .Include(b => b.HallSchedule)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId &&
                b.CoordinationAssignments.Any(a => a.CoordinatorEmployeeId == employeeId.Value &&
                    a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned), cancellationToken);
        return booking is null
            ? (null, "Không tìm thấy tiệc trong phạm vi phân công của bạn.", StatusCodes.Status404NotFound)
            : (booking, null, StatusCodes.Status200OK);
    }

    private Task<int?> ActiveCoordinatorEmployeeIdAsync(int userId, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking().Where(e => e.UserId == userId &&
                e.User.Role.RoleName == RoleNames.Coordinator &&
                e.User.Status.StatusCode == AccountStatusCodes.Active &&
                e.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                e.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(e => (int?)e.EmployeeId).SingleOrDefaultAsync(cancellationToken);
}

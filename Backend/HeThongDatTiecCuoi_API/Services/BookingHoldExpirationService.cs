using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>
/// UC13/BR07: chỉ tự động hết hạn booking PENDING quá 48 giờ khi chưa có hợp đồng
/// và chưa có hoạt động thanh toán. CONFIRMED, booking có hợp đồng/thanh toán và
/// lịch LOCKED không bị tác vụ này thay đổi.
/// </summary>
public sealed class BookingHoldExpirationService : BackgroundService
{
    public const int HoldHours = 48;
    private const string CancellationReason = "Hết hạn giữ chỗ 48 giờ (chưa có hợp đồng/thanh toán).";
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingHoldExpirationService> _logger;

    public BookingHoldExpirationService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingHoldExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var expired = await ExpireDueAsync(context, DateTime.Now, stoppingToken);
                if (expired > 0)
                    _logger.LogInformation("Đã tự động hết hạn {Count} booking PENDING quá 48 giờ.", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tác vụ hết hạn giữ chỗ 48 giờ thất bại.");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Hết hạn có điều kiện, an toàn khi chạy lặp hoặc đồng thời với xác nhận/cọc.
    /// Trả về số booking thực sự được chuyển sang CANCELLED.
    /// </summary>
    public static async Task<int> ExpireDueAsync(
        ApplicationDbContext context,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlServer())
            return 0;

        var cutoff = now.AddHours(-HoldHours);
        var candidates = await context.WeddingBookings.AsNoTracking()
            .Where(booking =>
                booking.BookedAt <= cutoff &&
                booking.Status.StatusGroup == StatusGroups.Booking &&
                booking.Status.StatusCode == BookingStatusCodes.Pending &&
                booking.Contract == null)
            .Select(booking => new { booking.BookingId, booking.HallScheduleId })
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var candidate in candidates)
        {
            // Cập nhật có điều kiện: kiểm tra lại trạng thái, hạn 48 giờ, hợp đồng và
            // thanh toán ngay tại thời điểm ghi để không đè lên xác nhận/cọc vừa phát sinh.
            var cancelled = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE booking
                SET booking.TrangThaiID = (
                        SELECT TOP (1) status.TrangThaiID
                        FROM TrangThai AS status
                        WHERE status.NhomTrangThai = {StatusGroups.Booking}
                          AND status.MaTrangThai = {BookingStatusCodes.Cancelled}),
                    booking.LyDoHuy = {CancellationReason}
                FROM DatTiec AS booking
                WHERE booking.DatTiecID = {candidate.BookingId}
                  AND booking.TrangThaiID = (
                        SELECT TOP (1) status.TrangThaiID
                        FROM TrangThai AS status
                        WHERE status.NhomTrangThai = {StatusGroups.Booking}
                          AND status.MaTrangThai = {BookingStatusCodes.Pending})
                  AND booking.NgayDat <= {cutoff}
                  AND NOT EXISTS (
                        SELECT 1 FROM HopDong AS contract
                        WHERE contract.DatTiecID = booking.DatTiecID)
                  AND NOT EXISTS (
                        SELECT 1
                        FROM ThanhToan AS payment
                        INNER JOIN HopDong AS contract ON contract.HopDongID = payment.HopDongID
                        WHERE contract.DatTiecID = booking.DatTiecID)
                """, cancellationToken);
            if (cancelled == 0)
                continue;

            expiredCount++;

            // Chỉ mở lại lịch đang BOOKED và không còn booking chiếm chỗ; LOCKED được giữ nguyên.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE schedule
                SET schedule.TrangThaiID = (
                        SELECT TOP (1) status.TrangThaiID
                        FROM TrangThai AS status
                        WHERE status.NhomTrangThai = {StatusGroups.HallSchedule}
                          AND status.MaTrangThai = {HallScheduleStatusCodes.Available})
                FROM LichSanh AS schedule
                WHERE schedule.LichSanhID = {candidate.HallScheduleId}
                  AND schedule.TrangThaiID = (
                        SELECT TOP (1) status.TrangThaiID
                        FROM TrangThai AS status
                        WHERE status.NhomTrangThai = {StatusGroups.HallSchedule}
                          AND status.MaTrangThai = {HallScheduleStatusCodes.Booked})
                  AND NOT EXISTS (
                        SELECT 1
                        FROM DatTiec AS other
                        INNER JOIN TrangThai AS status ON status.TrangThaiID = other.TrangThaiID
                        WHERE other.LichSanhID = schedule.LichSanhID
                          AND status.NhomTrangThai = {StatusGroups.Booking}
                          AND status.MaTrangThai IN ({BookingStatusCodes.Pending}, {BookingStatusCodes.Confirmed}, {BookingStatusCodes.Preparing}))
                """, cancellationToken);
        }

        return expiredCount;
    }
}

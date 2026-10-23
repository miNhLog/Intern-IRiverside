using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.RateLimiting;

namespace HeThongDatTiecCuoi_API.Controllers;

/// <summary>
/// UC14 &amp; UC15: Tra cứu + hủy giữ chỗ tiệc cưới trực tuyến (public, không cần đăng nhập).
/// Route mới /api/v1/public/bookings/* — giữ nguyên route cũ /api/my-bookings để tương thích.
/// </summary>
[ApiController]
[Route("api/v1/public/bookings")]
[AllowAnonymous]
public sealed class PublicBookingController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailedWindow = TimeSpan.FromMinutes(15);
    private static readonly object FailedLookupSync = new();

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PublicBookingController> _logger;

    public PublicBookingController(
        ApplicationDbContext context,
        IStatusService statusService,
        IMemoryCache cache,
        ILogger<PublicBookingController> logger)
    {
        _context = context;
        _statusService = statusService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>UC14: POST /api/v1/public/bookings/lookup — tra cứu bằng mã + SĐT.</summary>
    [HttpPost("lookup")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EnableRateLimiting("public-booking-lookup")]
    public async Task<IActionResult> Lookup([FromBody] PublicBookingLookupRequestDto? request)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        string? rateKey = null;
        FailedLookupState? attempt = null;
        var attemptCompleted = false;

        try
        {
            if (request is null)
                return BadRequest(new { success = false, message = "Thiếu dữ liệu tra cứu." });

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu tra cứu không hợp lệ." });

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            rateKey = $"public-lookup-fail:{clientIp}";
            attempt = TryReserveLookupAttempt(rateKey);
            if (attempt is null)
                return TooManyAttempts();

            var code = request.BookingCode.Trim().ToUpperInvariant();
            var phone = request.PhoneNumber.Trim();
            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync();

            var booking = await _context.WeddingBookings.AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.Contract!).ThenInclude(c => c!.Payments).ThenInclude(p => p.Status)
                .SingleOrDefaultAsync(b =>
                    b.BookingCode == code &&
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
            {
                CompleteLookupAttempt(rateKey, attempt, matched: false);
                attemptCompleted = true;
                _logger.LogInformation("Public booking lookup did not match. traceId={TraceId}", HttpContext.TraceIdentifier);
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });
            }

            // A successful lookup releases its in-flight reservation but does not erase prior failures.
            CompleteLookupAttempt(rateKey, attempt, matched: true);
            attemptCompleted = true;

            var detail = BuildDetailDto(booking);
            _logger.LogInformation("Public booking lookup matched. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Ok(new { success = true, data = detail });
        }
        catch (Exception ex)
        {
            if (!attemptCompleted && rateKey is not null && attempt is not null)
                CompleteLookupAttempt(rateKey, attempt, matched: null);
            _logger.LogError(ex, "PublicBooking lookup thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể tra cứu lúc này, vui lòng thử lại."
            });
        }
    }

    /// <summary>UC15: POST /api/v1/public/bookings/{id}/cancel-hold — hủy giữ chỗ online.</summary>
    [HttpPost("{id:int}/cancel-hold")]
    public async Task<IActionResult> CancelHold(int id, [FromBody] PublicBookingCancelHoldRequestDto? request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (request is null)
                return BadRequest(new { success = false, message = "Thiếu dữ liệu hủy giữ chỗ." });
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);
            if (request.Reason?.Trim().Length > 500)
                return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

            var code = request.BookingCode.Trim().ToUpperInvariant();
            var phone = request.PhoneNumber.Trim();
            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync();

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .SingleOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.BookingCode == code &&
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                booking.Status.StatusCode != BookingStatusCodes.Pending)
                return BadRequest(new
                {
                    success = false,
                    code = "ERR_BOOKING_CANNOT_CANCEL",
                    message = "Chỉ có thể hủy online khi tiệc đang ở trạng thái Chờ xác nhận. Tiệc đã xác nhận/cọc vui lòng liên hệ nhân viên."
                });

            var oldStatusName = booking.Status.StatusName;
            booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled);
            booking.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách hủy giữ chỗ online"
                : request.Reason.Trim();

            // Giải phóng lịch sảnh về TRỐNG (AVAILABLE) theo BR03 trong cùng transaction.
            // Dùng AVAILABLE qua IStatusService đúng domain HALL_SCHEDULE (không hardcode số).
            // Bỏ qua nếu schedule null (default-available: không có dòng vẫn coi trống).
            // LƯU Ý: TrangThaiID=401 là MENU/Đang áp dụng, KHÔNG phải LichSanh —
            // dùng 401 cho LichSanh sẽ sai domain, nên dùng AVAILABLE qua IStatusService.
            var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
                item.BookingId != booking.BookingId &&
                item.HallScheduleId == booking.HallScheduleId &&
                item.Status != null &&
                item.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
            if (!hasOtherActiveBooking && booking.HallSchedule is not null)
                booking.HallSchedule.StatusId =
                    await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "CANCEL",
                EntityName = "DatTiec",
                EntityId = booking.BookingId,
                OldData = $"MaDatTiec={booking.BookingCode};TrangThai={oldStatusName}",
                NewData = $"MaDatTiec={booking.BookingCode};TrangThai=Đã hủy;LyDoHuy={booking.CancellationReason}",
                Timestamp = DateTime.Now,
                Notes = "Khách hủy giữ chỗ online (public cancel-hold)"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new
            {
                success = true,
                message = "Đã hủy giữ chỗ thành công và giải phóng lịch sảnh."
            });
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { }
            _logger.LogError(ex, "PublicBooking cancel-hold thất bại (id={Id}). traceId={TraceId}. Chi tiết: {Detail}",
                id, HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể hủy giữ chỗ lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private ObjectResult TooManyAttempts() => StatusCode(StatusCodes.Status429TooManyRequests, new
    {
        success = false,
        message = "Bạn đã tra cứu sai quá 5 lần trong 15 phút. Vui lòng thử lại sau."
    });

    private FailedLookupState? TryReserveLookupAttempt(string rateKey)
    {
        lock (FailedLookupSync)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_cache.TryGetValue<FailedLookupState>(rateKey, out var state) || state is null ||
                (state.HasFailures && state.WindowStartedAt + FailedWindow <= now))
            {
                state = new FailedLookupState();
                _cache.Set(rateKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = now + FailedWindow
                });
            }

            if (state.FailedCount + state.InFlightCount >= MaxFailedAttempts)
                return null;

            state.InFlightCount++;
            return state;
        }
    }

    private void CompleteLookupAttempt(string rateKey, FailedLookupState attempt, bool? matched)
    {
        lock (FailedLookupSync)
        {
            if (!_cache.TryGetValue<FailedLookupState>(rateKey, out var current) || !ReferenceEquals(current, attempt))
                return;

            attempt.InFlightCount--;
            if (matched == false)
            {
                if (!attempt.HasFailures)
                {
                    attempt.HasFailures = true;
                    attempt.WindowStartedAt = DateTimeOffset.UtcNow;
                }
                attempt.FailedCount++;
                _cache.Set(rateKey, attempt, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = attempt.WindowStartedAt + FailedWindow
                });
            }
            else if (!attempt.HasFailures && attempt.InFlightCount == 0)
            {
                _cache.Remove(rateKey);
            }
        }
    }

    private sealed class FailedLookupState
    {
        public int FailedCount { get; set; }
        public int InFlightCount { get; set; }
        public bool HasFailures { get; set; }
        public DateTimeOffset WindowStartedAt { get; set; }
    }

    private static PublicBookingDetailDto BuildDetailDto(WeddingBooking booking)
    {
        var successful = booking.Contract?.Payments
            .Where(payment => payment.Status is not null &&
                payment.Status.StatusGroup == StatusGroups.Payment &&
                payment.Status.StatusCode == PaymentStatusCodes.Success)
            .OrderBy(payment => payment.PaymentDate)
            .Select(payment => new PublicBookingPaymentDto
            {
                Type = payment.PaymentType,
                Amount = payment.Amount,
                PaidAt = payment.PaymentDate
            }).ToList() ?? [];
        var paid = successful.Sum(payment => payment.Amount);
        var deposit = successful.Where(payment => payment.Type == PaymentTypes.Deposit).Sum(payment => payment.Amount);
        var contractTotal = booking.Contract?.TotalValue;

        return new PublicBookingDetailDto
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer.FullName,
            EventDate = booking.HallSchedule.Date,
            EventDateString = booking.HallSchedule.Date.ToString("yyyy-MM-dd"),
            Shift = booking.HallSchedule.Shift,
            HallName = booking.HallSchedule.Hall.HallName,
            GuestCount = booking.GuestCount,
            StatusCode = booking.Status.StatusCode,
            StatusName = booking.Status.StatusName,
            CanCancelOnline = booking.Status.StatusGroup == StatusGroups.Booking &&
                booking.Status.StatusCode == BookingStatusCodes.Pending,
            HoldExpiresAt = booking.BookedAt.AddHours(48),
            HasContract = booking.Contract is not null,
            ContractTotal = contractTotal,
            EstimatedTotal = booking.Contract is null ? booking.EstimatedTotal : null,
            RequiredDepositAmount = booking.Contract?.RequiredDepositAmount,
            PaymentTerms = booking.Contract?.PaymentTerms,
            DepositPaid = deposit,
            TotalPaid = paid,
            RemainingBalance = contractTotal.HasValue ? contractTotal.Value - paid : null,
            SuccessfulPayments = successful
        };
    }
}

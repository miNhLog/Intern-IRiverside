using System.Data;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace HeThongDatTiecCuoi_API.Controllers;

/// <summary>
/// UC14 &amp; UC15: Tra cứu, hủy giữ chỗ và đổi ngày/ca tiệc cưới trực tuyến (public, không cần đăng nhập).
/// Route mới /api/v1/public/bookings/*. Legacy /api/my-bookings đã ngừng hoạt động (410).
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
    private readonly IEmailService? _emailService;

    public PublicBookingController(
        ApplicationDbContext context,
        IStatusService statusService,
        IMemoryCache cache,
        ILogger<PublicBookingController> logger,
        IEmailService? emailService = null)
    {
        _context = context;
        _statusService = statusService;
        _cache = cache;
        _logger = logger;
        _emailService = emailService;
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
    [EnableRateLimiting("public-booking-mutation")]
    public async Task<IActionResult> CancelHold(
        int id,
        [FromBody] PublicBookingCancelHoldRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Thiếu dữ liệu hủy giữ chỗ." });
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (request.Reason?.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

        var code = request.BookingCode.Trim().ToUpperInvariant();
        var phone = request.PhoneNumber.Trim();

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await AcquireBookingRowLockAsync(id, cancellationToken);

            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync(cancellationToken);

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.Contract)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .SingleOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.BookingCode == code &&
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId,
                    cancellationToken);

            if (booking is null)
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                booking.Status.StatusCode != BookingStatusCodes.Pending)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_NOT_PENDING",
                    message = "Chỉ có thể hủy online khi tiệc đang ở trạng thái Chờ xác nhận. Tiệc đã xác nhận/cọc vui lòng liên hệ nhân viên."
                });

            if (booking.Contract is not null ||
                await _context.Payments.AnyAsync(payment => payment.Contract.BookingId == booking.BookingId, cancellationToken))
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT",
                    message = "Đơn tiệc đã phát sinh hợp đồng hoặc thanh toán nên không thể hủy trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace để được hỗ trợ."
                });

            var now = DateTime.Now;
            if (booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= now)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_HOLD_EXPIRED",
                    message = "Đơn giữ chỗ đã hết hạn 48 giờ và không thể hủy trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace."
                });

            var oldStatusName = booking.Status.StatusName;
            var oldScheduleId = booking.HallScheduleId;
            var cancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách hủy giữ chỗ online"
                : request.Reason.Trim();

            booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled, cancellationToken);
            booking.CancellationReason = cancellationReason;

            // BR03: chỉ giải phóng lịch đang BOOKED, không còn booking chiếm giữ và không phải LOCKED.
            if (booking.HallSchedule is not null &&
                booking.HallSchedule.Status is not null &&
                booking.HallSchedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                booking.HallSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked)
            {
                var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
                    item.BookingId != booking.BookingId &&
                    item.HallScheduleId == oldScheduleId &&
                    item.Status != null &&
                    item.Status.StatusGroup == StatusGroups.Booking &&
                    BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode),
                    cancellationToken);
                if (!hasOtherActiveBooking)
                    booking.HallSchedule.StatusId =
                        await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "BOOKING_CANCELLED",
                EntityName = "DatTiec",
                EntityId = booking.BookingId,
                OldData = $"MaDatTiec={booking.BookingCode};TrangThai={oldStatusName};LichSanhID={oldScheduleId}",
                NewData = $"MaDatTiec={booking.BookingCode};TrangThai=Đã hủy;LyDoHuy={cancellationReason}",
                Timestamp = now,
                Notes = "Khách hủy giữ chỗ online (public cancel-hold)"
            });

            // Snapshot trước commit để email không phụ thuộc trạng thái tracking sau này.
            var email = booking.Customer.Email?.Trim();
            var customerName = booking.Customer.FullName;
            var bookingCode = booking.BookingCode;
            var eventDate = booking.HallSchedule?.Date ?? DateTime.MinValue;
            var shift = booking.HallSchedule?.Shift ?? string.Empty;
            var hallName = booking.HallSchedule?.Hall?.HallName ?? string.Empty;

            // Recheck after any schedule/status queries that may have waited on locks.
            if (booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
                return Conflict(new { success = false, code = "ERR_BOOKING_HOLD_EXPIRED", message = "Đơn giữ chỗ đã hết hạn 48 giờ và không thể hủy trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace." });
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            var emailSent = await TrySendBookingCancelledEmailAsync(
                email, customerName, bookingCode, eventDate, shift, hallName, cancellationReason, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Đã hủy giữ chỗ thành công.",
                statusCode = BookingStatusCodes.Cancelled,
                emailSent
            });
        }
        catch (Exception ex) when (IsReservationConflict(ex))
        {
            try { if (transaction is not null) await transaction.RollbackAsync(cancellationToken); } catch { }
            _logger.LogWarning(ex, "PublicBooking cancel-hold xung đột dữ liệu (id={Id}). traceId={TraceId}", id, HttpContext.TraceIdentifier);
            return Conflict(new
            {
                success = false,
                code = "ERR_BOOKING_STATE_CONFLICT",
                message = "Đơn tiệc vừa được thay đổi bởi thao tác khác. Vui lòng tra cứu lại trước khi thử lại."
            });
        }
        catch (Exception ex)
        {
            try { if (transaction is not null) await transaction.RollbackAsync(cancellationToken); } catch { }
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

    /// <summary>
    /// UC15: POST /api/v1/public/bookings/{id}/reschedule — đổi ngày và/hoặc ca, giữ nguyên sảnh.
    /// Có hiệu lực ngay sau transaction và ghi lịch sử ThayDoiLichTiec ở trạng thái APPROVED.
    /// </summary>
    [HttpPost("{id:int}/reschedule")]
    [EnableRateLimiting("public-booking-mutation")]
    public async Task<IActionResult> Reschedule(
        int id,
        [FromBody] PublicBookingRescheduleRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Thiếu dữ liệu đổi lịch." });
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (!request.NewDate.HasValue)
            return BadRequest(new { success = false, message = "Vui lòng chọn ngày tổ chức mới." });
        if (request.Reason?.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Lý do đổi lịch không được vượt quá 500 ký tự." });

        var newShift = NormalizeShift(request.NewShift);
        if (newShift is null)
            return BadRequest(new { success = false, message = "Ca tổ chức không hợp lệ. Vui lòng chọn Ca trưa hoặc Ca tối." });

        var newDate = request.NewDate.Value.Date;
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (newDate < minDate)
            return BadRequest(new { success = false, message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày." });
        if (newDate > maxDate)
            return BadRequest(new { success = false, message = "Hệ thống chỉ nhận đổi lịch trước tối đa 365 ngày (1 năm)." });

        var code = request.BookingCode.Trim().ToUpperInvariant();
        var phone = request.PhoneNumber.Trim();
        var nextDay = newDate.AddDays(1);
        var shiftLower = newShift.ToLowerInvariant();

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await AcquireBookingRowLockAsync(id, cancellationToken);

            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync(cancellationToken);

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.Contract)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                    .ThenInclude(h => h.Status)
                .SingleOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.BookingCode == code &&
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId,
                    cancellationToken);

            if (booking is null)
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                booking.Status.StatusCode != BookingStatusCodes.Pending)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_NOT_PENDING",
                    message = "Chỉ có thể đổi lịch online khi tiệc đang ở trạng thái Chờ xác nhận. Vui lòng liên hệ nhân viên Riverside Palace."
                });

            if (booking.Contract is not null ||
                await _context.Payments.AnyAsync(payment => payment.Contract.BookingId == booking.BookingId, cancellationToken))
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT",
                    message = "Đơn tiệc đã phát sinh hợp đồng hoặc thanh toán nên không thể đổi lịch trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace để được hỗ trợ."
                });

            var now = DateTime.Now;
            if (booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= now)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_BOOKING_HOLD_EXPIRED",
                    message = "Đơn giữ chỗ đã hết hạn 48 giờ và không thể đổi lịch trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace."
                });

            var oldSchedule = booking.HallSchedule;
            var hall = oldSchedule.Hall;
            if (hall.DataStatusId != existingId || hall.Status is null ||
                hall.Status.StatusGroup != StatusGroups.Hall || hall.Status.StatusCode != HallStatusCodes.Active)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_HALL_UNAVAILABLE",
                    message = "Sảnh tiệc không còn hoạt động. Vui lòng liên hệ nhân viên để được hỗ trợ."
                });
            var oldDate = oldSchedule.Date;
            var oldShift = oldSchedule.Shift;

            if (newDate == oldDate.Date && string.Equals(NormalizeShift(oldShift), newShift, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Ngày và ca mới trùng với lịch hiện tại của đơn tiệc." });

            if (booking.GuestCount > hall.MaximumCapacity ||
                (hall.MinimumCapacity.HasValue && booking.GuestCount < hall.MinimumCapacity.Value))
                return BadRequest(new { success = false, message = "Số khách của đơn tiệc không phù hợp sức chứa sảnh." });

            // Chặn mọi booking đang chiếm giữ cùng sảnh/ngày/ca (kể cả khi chưa có dòng LichSanh tương ứng).
            var occupiedByBooking = await _context.WeddingBookings.AnyAsync(item =>
                item.BookingId != booking.BookingId &&
                item.HallSchedule.HallId == hall.HallId &&
                item.HallSchedule.Date >= newDate && item.HallSchedule.Date < nextDay &&
                item.HallSchedule.Shift.ToLower() == shiftLower &&
                item.HallSchedule.DataStatusId == existingId &&
                item.Status != null &&
                item.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode),
                cancellationToken);
            if (occupiedByBooking)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_SCHEDULE_UNAVAILABLE",
                    message = $"Sảnh này vào ngày {newDate:dd/MM/yyyy} ({newShift}) đã có khách đặt. Vui lòng chọn ngày hoặc ca khác."
                });

            var targetScheduleId = await _context.HallSchedules.AsNoTracking()
                .Where(item => item.HallId == hall.HallId &&
                    item.Date >= newDate && item.Date < nextDay &&
                    item.Shift.ToLower() == shiftLower &&
                    item.DataStatusId == existingId)
                .Select(item => (int?)item.HallScheduleId)
                .SingleOrDefaultAsync(cancellationToken);

            if (targetScheduleId.HasValue)
                await AcquireScheduleRowLockAsync(targetScheduleId.Value, cancellationToken);

            var targetSchedule = targetScheduleId.HasValue
                ? await _context.HallSchedules
                    .Include(item => item.Status)
                    .SingleAsync(item => item.HallScheduleId == targetScheduleId.Value, cancellationToken)
                : null;

            var bookedStatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked, cancellationToken);
            if (targetSchedule is null)
            {
                targetSchedule = new HallSchedule
                {
                    HallId = hall.HallId,
                    Hall = hall,
                    Date = newDate,
                    Shift = newShift,
                    StatusId = bookedStatusId,
                    DataStatusId = existingId
                };
                _context.HallSchedules.Add(targetSchedule);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (targetSchedule.Status is null ||
                    targetSchedule.Status.StatusGroup != StatusGroups.HallSchedule)
                    return Conflict(new { success = false, message = "Lịch sảnh không ở trạng thái có thể đặt." });
                if (targetSchedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
                    return Conflict(new
                    {
                        success = false,
                        code = "ERR_SCHEDULE_LOCKED",
                        message = "Khung giờ này đang bị khóa vận hành. Vui lòng chọn ngày hoặc ca khác."
                    });
                if (targetSchedule.Status.StatusCode != HallScheduleStatusCodes.Available &&
                    targetSchedule.Status.StatusCode != HallScheduleStatusCodes.Booked)
                    return Conflict(new { success = false, message = "Lịch sảnh không ở trạng thái có thể đặt." });

                targetSchedule.StatusId = bookedStatusId;
            }

            booking.HallScheduleId = targetSchedule.HallScheduleId;
            booking.HallSchedule = targetSchedule;

            // Giải phóng lịch cũ nếu không còn booking chiếm giữ; LOCKED luôn được giữ nguyên.
            if (oldSchedule.Status is not null &&
                oldSchedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                oldSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked)
            {
                var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
                    item.BookingId != booking.BookingId &&
                    item.HallScheduleId == oldSchedule.HallScheduleId &&
                    item.Status != null &&
                    item.Status.StatusGroup == StatusGroups.Booking &&
                    BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode),
                    cancellationToken);
                if (!hasOtherActiveBooking)
                    oldSchedule.StatusId =
                        await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);
            }

            var approvedStatusId = await _statusService.GetStatusIdAsync(StatusGroups.ScheduleChange, BusinessStatusCodes.Approved, cancellationToken);
            var changeReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách đổi lịch online"
                : request.Reason.Trim();
            _context.WeddingScheduleChanges.Add(new WeddingScheduleChange
            {
                BookingId = booking.BookingId,
                OldHallScheduleId = oldSchedule.HallScheduleId,
                NewHallScheduleId = targetSchedule.HallScheduleId,
                Reason = changeReason,
                StatusId = approvedStatusId,
                RequestedAt = now,
                ProcessedAt = now
            });

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "BOOKING_RESCHEDULED",
                EntityName = "DatTiec",
                EntityId = booking.BookingId,
                OldData = $"MaDatTiec={booking.BookingCode};Ngay={oldDate:yyyy-MM-dd};Ca={oldShift};LichSanhID={oldSchedule.HallScheduleId}",
                NewData = $"MaDatTiec={booking.BookingCode};Ngay={newDate:yyyy-MM-dd};Ca={newShift};LichSanhID={targetSchedule.HallScheduleId};LyDo={changeReason}",
                Timestamp = now,
                Notes = "Khách đổi lịch online (public reschedule)"
            });

            var email = booking.Customer.Email?.Trim();
            var customerName = booking.Customer.FullName;
            var bookingCode = booking.BookingCode;
            var hallName = hall.HallName;

            // Target-slot locking and schedule creation can wait after the first check.
            // A return here rolls back any schedule row inserted in this transaction.
            if (booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
                return Conflict(new { success = false, code = "ERR_BOOKING_HOLD_EXPIRED", message = "Đơn giữ chỗ đã hết hạn 48 giờ và không thể đổi lịch trực tuyến. Vui lòng liên hệ nhân viên Riverside Palace." });
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            var emailSent = await TrySendBookingRescheduledEmailAsync(
                email, customerName, bookingCode,
                oldDate, oldShift, hallName,
                newDate, newShift, hallName,
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Đã đổi lịch tiệc thành công.",
                statusCode = BookingStatusCodes.Pending,
                newDate = newDate.ToString("yyyy-MM-dd"),
                newShift,
                hallName,
                emailSent
            });
        }
        catch (Exception ex) when (IsReservationConflict(ex))
        {
            try { if (transaction is not null) await transaction.RollbackAsync(cancellationToken); } catch { }
            _logger.LogWarning(ex, "PublicBooking reschedule xung đột dữ liệu (id={Id}). traceId={TraceId}", id, HttpContext.TraceIdentifier);
            return Conflict(new
            {
                success = false,
                code = "ERR_SCHEDULE_CONFLICT",
                message = "Khung giờ này vừa được khách khác giữ chỗ. Vui lòng chọn ngày hoặc ca khác."
            });
        }
        catch (Exception ex)
        {
            try { if (transaction is not null) await transaction.RollbackAsync(cancellationToken); } catch { }
            _logger.LogError(ex, "PublicBooking reschedule thất bại (id={Id}). traceId={TraceId}. Chi tiết: {Detail}",
                id, HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể đổi lịch lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken) =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    /// <summary>Khóa dòng DatTiec để hủy/đổi lịch không chạy song song với worker 48h hoặc thao tác khác.</summary>
    private async Task AcquireBookingRowLockAsync(int bookingId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsSqlServer())
            return;
        await _context.Database
            .SqlQuery<int>($"SELECT DatTiecID AS Value FROM DatTiec WITH (UPDLOCK, ROWLOCK) WHERE DatTiecID = {bookingId}")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task AcquireScheduleRowLockAsync(int hallScheduleId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsSqlServer())
            return;
        await _context.Database
            .SqlQuery<int>($"SELECT LichSanhID AS Value FROM LichSanh WITH (UPDLOCK, ROWLOCK) WHERE LichSanhID = {hallScheduleId}")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> TrySendBookingCancelledEmailAsync(
        string? recipientEmail,
        string customerName,
        string bookingCode,
        DateTime eventDate,
        string shift,
        string hallName,
        string cancellationReason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Không gửi email hủy giữ chỗ {BookingCode}: khách không có email.", bookingCode);
            return false;
        }
        if (_emailService is null)
        {
            _logger.LogWarning("Không gửi email hủy giữ chỗ {BookingCode}: IEmailService chưa được đăng ký DI.", bookingCode);
            return false;
        }

        try
        {
            await _emailService.SendBookingCancelledAsync(
                recipientEmail, customerName, bookingCode, eventDate, shift, hallName,
                cancellationReason, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email hủy giữ chỗ thất bại cho mã đơn {BookingCode} đến {Email}",
                bookingCode, recipientEmail);
            return false;
        }
    }

    private async Task<bool> TrySendBookingRescheduledEmailAsync(
        string? recipientEmail,
        string customerName,
        string bookingCode,
        DateTime oldEventDate,
        string oldShift,
        string oldHallName,
        DateTime newEventDate,
        string newShift,
        string newHallName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Không gửi email đổi lịch {BookingCode}: khách không có email.", bookingCode);
            return false;
        }
        if (_emailService is null)
        {
            _logger.LogWarning("Không gửi email đổi lịch {BookingCode}: IEmailService chưa được đăng ký DI.", bookingCode);
            return false;
        }

        try
        {
            await _emailService.SendBookingRescheduledAsync(
                recipientEmail, customerName, bookingCode,
                oldEventDate, oldShift, oldHallName,
                newEventDate, newShift, newHallName,
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email đổi lịch thất bại cho mã đơn {BookingCode} đến {Email}",
                bookingCode, recipientEmail);
            return false;
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

    private static string? NormalizeShift(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var value = raw.Trim();
        if (value.Contains("trưa", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("trua", StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Lunch;
        if (value.Contains("tối", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("toi", StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Dinner;
        return null;
    }

    private static bool IsReservationConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number is 1205 or 1222 or 2601 or 2627 or 3960)
                return true;
        }
        return false;
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
        var holdExpiresAt = booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours);
        var isHoldExpired = holdExpiresAt <= DateTime.Now;
        var isSelfServiceEligible =
            booking.Status.StatusGroup == StatusGroups.Booking &&
            booking.Status.StatusCode == BookingStatusCodes.Pending &&
            booking.Contract is null &&
            successful.Count == 0 &&
            !isHoldExpired;

        return new PublicBookingDetailDto
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer.FullName,
            EventDate = booking.HallSchedule.Date,
            EventDateString = booking.HallSchedule.Date.ToString("yyyy-MM-dd"),
            Shift = booking.HallSchedule.Shift,
            HallId = booking.HallSchedule.HallId,
            HallName = booking.HallSchedule.Hall.HallName,
            GuestCount = booking.GuestCount,
            StatusCode = booking.Status.StatusCode,
            StatusName = booking.Status.StatusName,
            CanCancelOnline = isSelfServiceEligible,
            CanRescheduleOnline = isSelfServiceEligible,
            IsHoldExpired = isHoldExpired,
            HoldExpiresAt = holdExpiresAt,
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

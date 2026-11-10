using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/my-bookings")]
public sealed class MyBookingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly ICoordinationAssignmentService _coordinationAssignments;
    private readonly IPaymentService? _paymentService;

    public MyBookingsController(ApplicationDbContext context, IStatusService statusService,
        ICoordinationAssignmentService coordinationAssignments, IPaymentService? paymentService = null)
    {
        _context = context;
        _statusService = statusService;
        _coordinationAssignments = coordinationAssignments;
        _paymentService = paymentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyBookings([FromQuery] int? userId, [FromQuery] string? bookingCode, [FromQuery] string? phone)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = int.TryParse(rawUserId, out var currentUserId);
        var isStaff = isAuthenticated && (
            User.IsInRole(RoleNames.Admin) ||
            User.IsInRole(RoleNames.Manager) ||
            User.IsInRole(RoleNames.Coordinator) ||
            User.IsInRole(RoleNames.HallManager));

        var existingId = await _context.DataStatuses
            .Where(item => item.DataStatusCode == DataStatusCodes.Existing)
            .Select(item => item.DataStatusId).SingleAsync();
        var query = _context.WeddingBookings.AsNoTracking().AsQueryable();

        // 1. Tra cứu theo userId
        if (userId is > 0)
        {
            if (!isAuthenticated)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, message = "Yêu cầu đăng nhập để tra cứu theo mã tài khoản." });
            }
            if (!isStaff && currentUserId != userId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền truy cập dữ liệu đơn tiệc của người khác." });
            }
            query = query.Where(item => item.Customer.UserId == userId && item.Customer.DataStatusId == existingId);
        }
        // 2. Người dùng đã đăng nhập tự xem danh sách tiệc của mình
        else if (isAuthenticated && !isStaff && string.IsNullOrWhiteSpace(bookingCode) && string.IsNullOrWhiteSpace(phone))
        {
            query = query.Where(item => item.Customer.UserId == currentUserId && item.Customer.DataStatusId == existingId);
        }
        // 3. Khách tra cứu công khai: BẮT BUỘC có cả mã đặt tiệc VÀ số điện thoại để chống rò rỉ dữ liệu (SEC-03)
        else if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(bookingCode))
        {
            var normalizedPhone = phone.Trim();
            var normalizedCode = bookingCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.BookingCode == normalizedCode &&
                item.Customer.PhoneNumber == normalizedPhone && item.Customer.DataStatusId == existingId);
        }
        else
        {
            return BadRequest(new { success = false, message = "Vui lòng cung cấp cả mã đặt tiệc và số điện thoại để tra cứu." });
        }

        var dtoList = await query
            .AsSplitQuery()
            .Select(item => new MyBookingDto
            {
                BookingId = item.BookingId,
                BookingCode = item.BookingCode,
                CustomerName = item.Customer.FullName,
                PhoneNumber = item.Customer.PhoneNumber,
                HallId = item.HallSchedule.HallId,
                HallName = item.HallSchedule.Hall.HallName,
                EventDate = item.HallSchedule.Date,
                Shift = item.HallSchedule.Shift,
                GuestCount = item.GuestCount,
                TableCount = (int)Math.Ceiling(item.GuestCount / 10m),
                Status = item.Status.StatusName,
                MenuName = item.Menu != null ? item.Menu.MenuName : null,
                FinalMenuPrice = item.FinalMenuPrice,
                DecorName = item.DecorationPackage != null ? item.DecorationPackage.TenGoi : null,
                FinalDecorationPrice = item.FinalDecorationPrice,
                FinalHallPrice = item.FinalHallPrice,
                EstimatedTotal = item.EstimatedTotal,
                StatusCode = item.Status.StatusCode,
                StatusName = item.Status.StatusName,
                BookedAt = item.BookedAt,
                SpecialRequests = item.SpecialRequests,
                ContractId = item.Contract != null ? item.Contract.ContractId : (int?)null,
                ContractCode = item.Contract != null ? item.Contract.ContractCode : null,
                ContractStatusCode = item.Contract != null ? item.Contract.Status.StatusCode : null,
                ContractStatusName = item.Contract != null ? item.Contract.Status.StatusName : null
            }).ToListAsync();

        var result = dtoList.OrderByDescending(item => item.BookedAt).ToList();

        return Ok(new { success = true, data = result });
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(
        int id,
        [FromQuery] string? cancellationReason,
        [FromQuery] string? bookingCode = null,
        [FromQuery] string? phone = null)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = int.TryParse(rawUserId, out var currentUserId);
        var isStaff = isAuthenticated && (
            User.IsInRole(RoleNames.Admin) ||
            User.IsInRole(RoleNames.Manager) ||
            User.IsInRole(RoleNames.Coordinator) ||
            User.IsInRole(RoleNames.HallManager));

        using var bookingLock = await BookingPaymentConcurrency.AcquireAsync(id, HttpContext.RequestAborted);
        await using var transaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(
            _context, HttpContext.RequestAborted);
        try
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, id, HttpContext.RequestAborted);
            var booking = await _context.WeddingBookings
                .Include(item => item.Status)
                .Include(item => item.HallSchedule)
                .Include(item => item.Customer)
                .SingleOrDefaultAsync(item => item.BookingId == id);
            if (booking == null)
                return NotFound(new { success = false, message = "Không tìm thấy đơn tiệc cưới này." });

            // Kiểm tra phân quyền hủy tiệc (SEC-01: BOLA/IDOR protection)
            if (!isStaff)
            {
                if (isAuthenticated)
                {
                    if (booking.Customer?.UserId != currentUserId)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền hủy đơn tiệc này." });
                    }
                }
                else
                {
                    var normalizedCode = bookingCode?.Trim().ToUpperInvariant();
                    var normalizedPhone = phone?.Trim();
                    if (string.IsNullOrEmpty(normalizedCode) || string.IsNullOrEmpty(normalizedPhone))
                    {
                        return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, message = "Vui lòng đăng nhập hoặc cung cấp mã đơn và số điện thoại để thực hiện hủy giữ chỗ." });
                    }

                    if (!string.Equals(booking.BookingCode, normalizedCode, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(booking.Customer?.PhoneNumber, normalizedPhone, StringComparison.Ordinal))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Thông tin xác thực đơn tiệc không chính xác." });
                    }
                }
            }

            if (booking.Status.StatusGroup != StatusGroups.Booking || booking.Status.StatusCode != BookingStatusCodes.Pending)
                return BadRequest(new { success = false, message = "Chỉ có thể hủy booking đang ở trạng thái PENDING." });
            if (cancellationReason?.Trim().Length > 500)
                return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

            var paymentAlreadyReceived = await BookingPaymentConcurrency.HasRecordedDepositAsync(
                _context, booking.BookingId, booking.PaidAmount, HttpContext.RequestAborted);
            if (paymentAlreadyReceived)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_PAID_BOOKING_REQUIRES_MANAGED_CANCELLATION",
                    message = "Khoản cọc đã được ghi nhận. Vui lòng thực hiện quy trình hủy booking đã thanh toán."
                });

            var oldStatusCode = booking.Status.StatusCode;
            booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled);
            booking.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason)
                ? "Hủy theo yêu cầu khách hàng"
                : cancellationReason.Trim();
            await _coordinationAssignments.SynchronizeBookingAssignmentsAsync(booking.BookingId,
                BookingStatusCodes.Cancelled, null, booking.CancellationReason, HttpContext.RequestAborted);

            // Giải phóng LichSanh về AVAILABLE theo BR03 trong cùng transaction.
            var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
                item.BookingId != booking.BookingId && item.HallScheduleId == booking.HallScheduleId &&
                item.Status != null &&
                item.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
            var hasActiveScheduleHold = await _context.ScheduleChangeHolds.AnyAsync(hold =>
                hold.HallScheduleId == booking.HallScheduleId &&
                hold.Status == ScheduleChangeHoldStatusCodes.Active &&
                hold.ExpiresAt > RiversideBusinessClock.Now);
            if (!hasOtherActiveBooking && !hasActiveScheduleHold && booking.HallSchedule is not null)
                booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = isAuthenticated ? currentUserId : null,
                Action = AuditActions.BookingHoldCancelled,
                EntityName = AuditEntityNames.Booking,
                EntityId = booking.BookingId,
                OldData = $"BookingCode={booking.BookingCode};Status={oldStatusCode}",
                NewData = $"BookingCode={booking.BookingCode};Status={BookingStatusCodes.Cancelled};Reason={booking.CancellationReason}",
                Timestamp = DateTime.Now,
                Outcome = AuditOutcomes.Success,
                Notes = "Hủy giữ chỗ từ API my-bookings."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            bookingLock.Dispose();
            if (_paymentService is not null)
                await _paymentService.CancelPendingDepositLinksAsync(
                    booking.BookingId, booking.CancellationReason, CancellationToken.None);
            return Ok(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }
}

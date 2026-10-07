using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
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

    public MyBookingsController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyBookings([FromQuery] int? userId, [FromQuery] string? bookingCode, [FromQuery] string? phone)
    {
        var existingId = await _context.DataStatuses
            .Where(item => item.DataStatusCode == DataStatusCodes.Existing)
            .Select(item => item.DataStatusId).SingleAsync();
        var query = _context.WeddingBookings.AsNoTracking().AsQueryable();

        if (userId is > 0)
            query = query.Where(item => item.Customer.UserId == userId && item.Customer.DataStatusId == existingId);
        else
        {
            if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { success = false, message = "Tra cứu khách cần cả mã đặt tiệc và số điện thoại." });
            var normalizedCode = bookingCode.Trim();
            var normalizedPhone = phone.Trim();
            query = query.Where(item => item.BookingCode == normalizedCode &&
                item.Customer.PhoneNumber == normalizedPhone && item.Customer.DataStatusId == existingId);
        }

        var result = await query.OrderByDescending(item => item.BookedAt)
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
                MenuName = item.Menu != null ? item.Menu.MenuName : null,
                FinalMenuPrice = item.FinalMenuPrice,
                DecorName = item.DecorationPackage != null ? item.DecorationPackage.TenGoi : null,
                FinalDecorationPrice = item.FinalDecorationPrice,
                FinalHallPrice = item.FinalHallPrice,
                EstimatedTotal = item.EstimatedTotal,
                StatusCode = item.Status.StatusCode,
                StatusName = item.Status.StatusName,
                BookedAt = item.BookedAt,
                SpecialRequests = item.SpecialRequests
            }).ToListAsync();

        return Ok(new { success = true, data = result });
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int id, [FromQuery] string? cancellationReason)
    {
        var booking = await _context.WeddingBookings
            .Include(item => item.Status)
            .Include(item => item.HallSchedule)
            .SingleOrDefaultAsync(item => item.BookingId == id);
        if (booking == null)
            return NotFound(new { success = false, message = "Không tìm thấy đơn tiệc cưới này." });
        if (booking.Status.StatusGroup != StatusGroups.Booking || booking.Status.StatusCode != BookingStatusCodes.Pending)
            return BadRequest(new { success = false, message = "Chỉ có thể hủy booking đang ở trạng thái PENDING." });
        if (cancellationReason?.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

        booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled);
        booking.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason)
            ? "Hủy theo yêu cầu khách hàng"
            : cancellationReason.Trim();

        var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
            item.BookingId != booking.BookingId && item.HallScheduleId == booking.HallScheduleId &&
            BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
        if (!hasOtherActiveBooking)
            booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
    }
}

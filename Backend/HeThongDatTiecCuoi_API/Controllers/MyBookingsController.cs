using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
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
    public IActionResult GetMyBookings()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            success = false,
            message = "Tra cứu công khai yêu cầu mã đặt tiệc và số điện thoại đã đăng ký."
        });
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int id, [FromQuery] string? cancellationReason)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
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

        // Giải phóng LichSanh về AVAILABLE theo BR03 trong cùng transaction.
        // Bỏ qua nếu schedule null (default-available). Chỉ giữ BOOKED khi còn DatTiec hoạt động khác.
        var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
            item.BookingId != booking.BookingId && item.HallScheduleId == booking.HallScheduleId &&
            item.Status != null &&
            item.Status.StatusGroup == StatusGroups.Booking &&
            BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
        if (!hasOtherActiveBooking && booking.HallSchedule is not null)
            booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }
}

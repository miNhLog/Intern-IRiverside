using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

/// <summary>
/// Legacy route đã ngừng hoạt động (UC15). Không còn thao tác hủy chỉ bằng ID.
/// Khách dùng /api/v1/public/bookings/* với mã đặt tiệc + SĐT (xem PublicBookingController).
/// </summary>
[ApiController]
[Route("api/my-bookings")]
public sealed class MyBookingsController : ControllerBase
{
    private const string RetiredMessage =
        "Tính năng này đã ngừng hoạt động. Vui lòng dùng mục \"Tiệc của tôi\" (/MyBooking) và xác thực bằng mã đặt tiệc cùng số điện thoại đã đăng ký.";

    [HttpGet]
    public IActionResult GetMyBookings()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            success = false,
            message = RetiredMessage
        });
    }

    [HttpPut("{id:int}/cancel")]
    public IActionResult CancelBooking(int id)
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            success = false,
            message = RetiredMessage
        });
    }
}

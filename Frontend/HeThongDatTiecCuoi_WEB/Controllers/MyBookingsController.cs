using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

/// <summary>
/// Legacy route đã ngừng hoạt động (UC15). Chuyển hướng tra cứu sang /MyBooking.
/// </summary>
public class MyBookingsController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "MyBooking");

    [HttpPost]
    public IActionResult CancelBooking(int bookingId)
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            success = false,
            message = "Tính năng hủy đơn cũ đã ngừng hoạt động. Vui lòng dùng mục \"Tiệc của tôi\" (/MyBooking) và xác thực bằng mã đặt tiệc cùng số điện thoại đã đăng ký."
        });
    }
}

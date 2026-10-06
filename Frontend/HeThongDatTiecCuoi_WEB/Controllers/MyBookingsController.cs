using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class MyBookingsController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public MyBookingsController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? phone, CancellationToken cancellationToken)
    {
        ViewData["PublicActivePage"] = "my-bookings";

        int? currentUserId = null;
        string targetPhone = phone ?? "";

        // 1. Lấy UserId và SĐT từ phiên đăng nhập nếu có
        if (User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out var uid))
            {
                currentUserId = uid;
            }

            if (string.IsNullOrEmpty(targetPhone))
            {
                targetPhone = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone") ?? "";
            }
        }

        // 2. Nếu chưa có SĐT, đọc từ Cookie "customer_phone" đã lưu khi đặt tiệc
        if (string.IsNullOrEmpty(targetPhone))
        {
            targetPhone = Request.Cookies["customer_phone"] ?? "";
        }

        // 3. Gửi truy vấn kết hợp cả phone và currentUserId
        var result = await _apiClient.GetMyBookingsAsync(targetPhone, currentUserId, cancellationToken);
        var bookings = result.Value ?? new List<MyBookingViewModel>();

        // 4. Nếu tìm thấy tiệc theo UserId, tự động điền SĐT thực tế vào ô tra cứu và cập nhật Cookie
        if (string.IsNullOrEmpty(targetPhone) && bookings.Count > 0)
        {
            targetPhone = bookings[0].PhoneNumber;
            Response.Cookies.Append("customer_phone", targetPhone, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(30),
                IsEssential = true
            });
        }

        ViewBag.CurrentPhone = targetPhone;

        return View(bookings);
    }

    [HttpPost]
    public async Task<IActionResult> CancelBooking(int bookingId, CancellationToken cancellationToken)
    {
        var result = await _apiClient.CancelBookingAsync(bookingId, cancellationToken);
        if (result.Succeeded)
        {
            return Json(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
        }
        return Json(new { success = false, message = result.Error ?? "Không thể hủy đơn tiệc lúc này." });
    }
}
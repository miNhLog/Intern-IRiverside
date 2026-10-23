using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
    public IActionResult Index() => RedirectToAction("Index", "MyBooking");

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

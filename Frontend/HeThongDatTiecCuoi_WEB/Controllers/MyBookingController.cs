// Alias route /MyBooking theo yêu cầu UC14/UC15 (giữ nguyên /MyBookings/Index tương thích).
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("MyBooking")]
public class MyBookingController : Controller
{
    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<MyBookingController> _logger;

    public MyBookingController(IRiversideApiClient apiClient, ILogger<MyBookingController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    // GET /MyBooking renders only the public proof form; proof is never read from a URL or cookie.
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        ViewData["PublicActivePage"] = "my-bookings";
        return View(new PublicBookingLookupInputViewModel());
    }

    // POST /MyBooking/Lookup — gọi Backend lookup, không throw 500.
    [HttpPost("Lookup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(PublicBookingLookupInputViewModel input, CancellationToken cancellationToken)
    {
        try
        {
            Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            ViewData["PublicActivePage"] = "my-bookings";
            input.BookingCode = (input.BookingCode ?? string.Empty).Trim().ToUpperInvariant();
            input.PhoneNumber = (input.PhoneNumber ?? string.Empty).Trim();
            ViewBag.LookupInput = input;

            if (!ModelState.IsValid)
            {
                ViewBag.LookupError = "Vui lòng nhập đúng mã đặt tiệc (DT-yyyyMMdd-XXXX) và SĐT 10 số.";
                return View("Index", input);
            }

            var result = await _apiClient.LookupPublicBookingAsync(input.BookingCode, input.PhoneNumber, cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                ViewBag.LookupDetail = result.Value;
            }
            else
            {
                ViewBag.LookupError = result.Error ?? "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác.";
                if (result.StatusCode == 429)
                    Response.StatusCode = 429;
            }

            return View("Index", input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking Lookup thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            ViewBag.LookupError = "Không thể tra cứu lúc này, vui lòng thử lại.";
            ViewBag.LookupInput = input;
            return View("Index", input);
        }
    }

    // POST /MyBooking/CancelHold — gọi Backend cancel-hold, trả JSON cho modal.
    [HttpPost("CancelHold")]
    public async Task<IActionResult> CancelHold(
        [FromForm] int bookingId,
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            bookingCode = (bookingCode ?? string.Empty).Trim().ToUpperInvariant();
            phoneNumber = (phoneNumber ?? string.Empty).Trim();
            if (bookingId <= 0 || !IsLookupShapeValid(bookingCode, phoneNumber))
                return Json(new { success = false, message = "Dữ liệu hủy giữ chỗ chưa hợp lệ." });

            var result = await _apiClient.CancelPublicBookingHoldAsync(
                bookingId, bookingCode, phoneNumber, reason?.Trim(), cancellationToken);
            if (result.Succeeded)
                return Json(new { success = true, message = result.Value ?? "Đã hủy giữ chỗ thành công và giải phóng lịch sảnh." });
            return Json(new { success = false, message = result.Error ?? "Không thể hủy giữ chỗ lúc này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking CancelHold thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return Json(new { success = false, message = "Không thể hủy giữ chỗ lúc này, vui lòng thử lại." });
        }
    }

    private static bool IsLookupShapeValid(string code, string phone) =>
        Regex.IsMatch(code ?? string.Empty, @"^DT-\d{8}-[A-Z0-9]{4,}$")
        && Regex.IsMatch(phone ?? string.Empty, @"^0\d{9}$");
}

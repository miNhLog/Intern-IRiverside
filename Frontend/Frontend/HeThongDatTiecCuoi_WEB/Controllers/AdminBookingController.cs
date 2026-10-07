using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class AdminBookingController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public AdminBookingController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // 1. GET: /AdminBooking - Tải trang danh sách đặt tiệc lần đầu
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] BookingFilterRequestViewModel filter, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        filter ??= new BookingFilterRequestViewModel();
        var result = await _apiClient.GetBookingsAsync(filter, accessToken, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            ViewBag.ErrorMessage = result.Error ?? "Không thể tải danh sách đặt tiệc cưới.";
            return View(new BookingListResponseViewModel());
        }

        ViewBag.CurrentFilter = filter;
        return View(result.Value);
    }

    // 2. GET: /AdminBooking/GetBookingsData - AJAX lọc, tìm kiếm & phân trang
    [HttpGet]
    public async Task<IActionResult> GetBookingsData([FromQuery] BookingFilterRequestViewModel filter, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.GetBookingsAsync(filter, accessToken, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể lấy dữ liệu đơn tiệc." });
        }

        return Json(new { success = true, data = result.Value });
    }

    // 3. GET: /AdminBooking/GetDetail/{id} - AJAX xem chi tiết đơn tiệc / hợp đồng
    [HttpGet]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.GetBookingDetailAsync(id, accessToken, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể lấy thông tin chi tiết đơn tiệc." });
        }

        return Json(new { success = true, data = result.Value });
    }

    // 4. POST: /AdminBooking/Create - AJAX tạo mới đơn đặt tiệc
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Dữ liệu nhập vào chưa hợp lệ, vui lòng kiểm tra lại." });
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.CreateBookingAsync(request, accessToken, cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể tạo mới đơn tiệc cưới." });
        }

        return Json(new { success = true, message = "Tạo mới đơn tiệc cưới thành công!" });
    }

    // 5. POST: /AdminBooking/UpdateStatus/{id} - AJAX duyệt, ghi nhận cọc, hoặc hủy tiệc
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusRequestViewModel request, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.UpdateBookingStatusAsync(id, request, accessToken, cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể cập nhật trạng thái đơn tiệc." });
        }

        return Json(new { success = true, message = $"Cập nhật trạng thái sang '{request.NewStatus}' thành công!" });
    }
}

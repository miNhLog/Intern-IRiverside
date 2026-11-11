using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
    [HttpGet]
    public async Task<IActionResult> GetCreateOptions(DateTime eventDate, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetBookingCreateOptionsAsync(eventDate, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded && result.Value != null
            ? Json(new { success = true, data = result.Value })
            : Json(new { success = false, message = result.Error ?? "Không thể tải dữ liệu tạo booking." });
    }

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

        return Json(new { success = true, message = "Tạo mới đơn tiệc cưới thành công!", bookingId = result.Value?.BookingId });
    }

    // 5. POST: /AdminBooking/UpdateStatus/{id} - AJAX duyệt, ghi nhận cọc, hoặc hủy tiệc
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.NewStatus) ||
            !new[] { "CONFIRMED", "PREPARING", "COMPLETED" }.Contains(request.NewStatus.Trim().ToUpperInvariant()))
            return BadRequest(new { success = false, message = "Trạng thái không được phép qua thao tác cập nhật nhanh." });
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.UpdateBookingStatusAsync(id, request, accessToken, cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể cập nhật trạng thái đơn tiệc." });
        }

        return Json(new { success = true, message = "Cập nhật trạng thái booking thành công!" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetCancelQuote(int id, [FromBody] ManagerBookingCancelQuoteRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Thông tin báo giá hủy không hợp lệ." });
        var result = await _apiClient.GetManagerBookingCancelQuoteAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, [FromBody] ManagerBookingCancelRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Cần nhập lý do hủy (tối đa 500 ký tự)." });
        var result = await _apiClient.CancelManagerBookingAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    [HttpGet]
    public async Task<IActionResult> GetRescheduleQuote(int id, DateTime newDate, string newShift, int newHallId, bool changeMenu = false, int? menuId = null, [FromQuery] int[]? customDishIds = null, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        var request = new ManagerBookingRescheduleRequestViewModel { NewDate = newDate, NewShift = newShift, NewHallId = newHallId, ChangeMenu = changeMenu, MenuId = menuId, CustomDishIds = customDishIds?.ToList() };
        var result = await _apiClient.GetManagerBookingRescheduleQuoteAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, [FromBody] ManagerBookingRescheduleRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        var result = await _apiClient.RescheduleManagerBookingAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    [HttpGet]
    public async Task<IActionResult> GetRefundObligations(int id, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        var result = await _apiClient.GetManagerBookingRefundObligationsAsync(id, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordRefundConfirmation(int id, [FromBody] ManagerBookingRefundConfirmationRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        if (request.RequestId == Guid.Empty || request.OperationRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.ExternalEvidenceReference) ||
            request.ExternalEvidenceReference.Length > 300 || !IsWholeNonnegativeVnd(request.PrincipalPaid) ||
            !IsWholeNonnegativeVnd(request.DepositPrincipalPaid) || !IsWholeNonnegativeVnd(request.CompensationPaid))
            return BadRequest(new { success = false, message = "Thông tin xác nhận hoàn tiền không hợp lệ." });
        var result = await _apiClient.RecordManagerBookingRefundConfirmationAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : Json(new { success = false, message = result.Error });
    }

    private static bool IsWholeNonnegativeVnd(decimal amount) => amount >= 0 && decimal.Truncate(amount) == amount;
}

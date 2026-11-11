using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.HallManager)]
public sealed class HallManagerBookingsController(IRiversideApiClient api) : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    [HttpGet]
    public async Task<IActionResult> Index(BookingFilterRequestViewModel filter, CancellationToken cancellationToken)
    {
        var result = await api.GetHallManagerOperationalBookingsAsync(filter, Request.Cookies[ApiTokenCookie], cancellationToken);
        ViewBag.Filter = filter;
        ViewBag.Error = result.Succeeded ? null : result.Error;
        return View(result.Value ?? new BookingListResponseViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var result = await api.GetHallManagerOperationalBookingAsync(id, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : StatusCode(result.StatusCode ?? 502, new { success = false, message = result.Error });
    }

    [HttpGet]
    public async Task<IActionResult> Quote(int id, DateTime newDate, string newShift, int newHallId, bool changeMenu = false, int? menuId = null, [FromQuery] int[]? customDishIds = null, CancellationToken cancellationToken = default)
    {
        var request = new ManagerBookingRescheduleRequestViewModel { NewDate = newDate, NewShift = newShift, NewHallId = newHallId, ChangeMenu = changeMenu, MenuId = menuId, CustomDishIds = customDishIds?.ToList() };
        var result = await api.GetManagerBookingRescheduleQuoteAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : StatusCode(result.StatusCode ?? 502, new { success = false, message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, [FromBody] ManagerBookingRescheduleRequestViewModel request, CancellationToken cancellationToken)
    {
        var result = await api.RescheduleHallManagerBookingAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : StatusCode(result.StatusCode ?? 502, new { success = false, message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Vui lòng nhập lý do hủy (tối đa 500 ký tự)." });
        var result = await api.CancelHallManagerBookingAsync(id, request.Reason.Trim(), Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : StatusCode(result.StatusCode ?? 502, new { success = false, message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Handoff(int id, [FromBody] ManagerBookingHandoffRequestViewModel request, CancellationToken cancellationToken)
    {
        var result = await api.HandoffManagerBookingAsync(id, request, Request.Cookies[ApiTokenCookie], cancellationToken);
        return result.Succeeded ? Json(new { success = true, data = result.Value }) : StatusCode(result.StatusCode ?? 502, new { success = false, message = result.Error });
    }

    public sealed class CancelRequest { public string Reason { get; set; } = string.Empty; }
}

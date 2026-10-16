using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.ManagerEventCoordination;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/tiec-dieu-phoi")]
public sealed class ManagerEventCoordinationController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public ManagerEventCoordinationController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? bookingStatus, string? coordinationStatus,
        DateTime? fromDate, DateTime? toDate, int page = 1, CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var response = await _api.GetManagerEventCoordinationAsync(token, search, bookingStatus, coordinationStatus, fromDate, toDate, page, 10, cancellationToken);
        return View(new ManagerEventCoordinationPageViewModel
        {
            Data = response.Value ?? new(), Search = search, BookingStatus = bookingStatus,
            CoordinationStatus = coordinationStatus, FromDate = fromDate, ToDate = toDate,
            ErrorMessage = response.Succeeded ? null : response.Error ?? "Không thể tải danh sách tiệc."
        });
    }

    [HttpGet("detail/{bookingId:int}")]
    public async Task<IActionResult> Detail(int bookingId, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized("Phiên đăng nhập đã hết hạn.");
        var response = await _api.GetManagerEventCoordinationDetailAsync(token, bookingId, cancellationToken);
        if (!response.Succeeded || response.Value is null)
            return StatusCode(response.StatusCode ?? StatusCodes.Status502BadGateway, response.Error ?? "Không thể tải chi tiết tiệc.");
        return PartialView("_DetailDrawer", response.Value);
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(DateTime fromDate, DateTime toDate, int? hallId,
        int? coordinatorEmployeeId, bool conflictsOnly = false, CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized("Phiên đăng nhập đã hết hạn.");
        var response = await _api.GetManagerEventCoordinationCalendarAsync(token, fromDate, toDate, hallId, coordinatorEmployeeId, conflictsOnly, cancellationToken);
        if (!response.Succeeded || response.Value is null)
            return StatusCode(response.StatusCode ?? StatusCodes.Status502BadGateway, response.Error ?? "Không thể tải lịch điều phối.");
        return PartialView("_Calendar", response.Value);
    }
}

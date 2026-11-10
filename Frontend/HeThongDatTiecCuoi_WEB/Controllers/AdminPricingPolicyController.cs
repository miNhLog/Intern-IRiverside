using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.ScheduleChange;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class AdminPricingPolicyController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public AdminPricingPolicyController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        // Truy vấn danh sách sảnh thực tế từ database thông qua API Client
        var hallsResult = await _apiClient.GetHallsAsync(accessToken, cancellationToken);
        var halls = hallsResult.Succeeded && hallsResult.Value is not null
            ? hallsResult.Value
            : new List<HallDto>();

        var policyTask = _apiClient.GetActiveScheduleChangePolicyAsync(accessToken, cancellationToken);
        var historyTask = _apiClient.GetScheduleChangePolicyHistoryAsync(accessToken, cancellationToken);
        await Task.WhenAll(policyTask, historyTask);
        ViewBag.ScheduleChangePolicy = new ScheduleChangePolicyPageViewModel
        {
            Active = policyTask.Result.Value,
            History = historyTask.Result.Value ?? []
        };
        ViewBag.ScheduleChangePolicyError = policyTask.Result.Error ?? historyTask.Result.Error;

        return View(halls);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateScheduleChangePolicyVersion(
        [FromBody] ScheduleChangePolicyVersionInput input, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken)) return Unauthorized(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        if (input.EffectiveFrom == default || input.Tiers.Count == 0 || input.Tiers.Any(x => x.FeeAmount < 0 || x.MinimumDays < 0))
            return BadRequest(new { success = false, message = "Dữ liệu chính sách không hợp lệ." });
        var ordered = input.Tiers.OrderBy(x => x.MinimumDays).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].MaximumDays.HasValue && ordered[i].MaximumDays < ordered[i].MinimumDays)
                return BadRequest(new { success = false, message = "Khoảng ngày của mức phí không hợp lệ." });
            if (i > 0 && (!ordered[i - 1].MaximumDays.HasValue || ordered[i].MinimumDays <= ordered[i - 1].MaximumDays))
                return BadRequest(new { success = false, message = "Các khoảng ngày không được chồng lấn." });
        }
        var result = await _apiClient.CreateScheduleChangePolicyVersionAsync(accessToken, input, cancellationToken);
        if (result.StatusCode.HasValue) Response.StatusCode = result.StatusCode.Value;
        return Json(new { success = result.Succeeded, data = result.Value, message = result.Error });
    }

    // Cập nhật giá sảnh trực tiếp vào bảng SanhTiec
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHallPrice(int hallId, decimal newPrice, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        }

        if (newPrice < 1000000)
        {
            return Json(new { success = false, message = "Đơn giá thuê sảnh phải từ 1.000.000 VNĐ." });
        }

        var hallsResult = await _apiClient.GetHallsAsync(accessToken, cancellationToken);
        var hall = hallsResult.Value?.FirstOrDefault(h => h.HallId == hallId);
        if (hall == null)
        {
            return Json(new { success = false, message = "Không tìm thấy thông tin sảnh." });
        }

        hall.RentalPrice = newPrice;
        var updateResult = await _apiClient.UpdateHallAsync(hallId, hall, accessToken, cancellationToken);
        if (!updateResult.Succeeded)
        {
            return Json(new { success = false, message = updateResult.Error ?? "Không thể cập nhật giá sảnh." });
        }

        return Json(new { success = true, message = $"Cập nhật đơn giá sảnh {hall.HallName} thành công!" });
    }
}

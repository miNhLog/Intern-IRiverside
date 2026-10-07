using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Services;
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

        return View(halls);
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

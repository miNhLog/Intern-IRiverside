using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminReport;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class AdminReportController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public AdminReportController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int year = 2026, int quarter = 0, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var result = await _apiClient.GetRevenueBiReportAsync(year, quarter, accessToken, cancellationToken);
        if (!result.Succeeded || result.Value == null)
        {
            ViewBag.ErrorMessage = result.Error ?? "Không thể tải số liệu báo cáo doanh thu từ hệ thống.";
            return View(new RevenueBiViewModel { Year = year, Quarter = quarter });
        }

        return View(result.Value);
    }

    // Endpoint AJAX phục vụ đổi bộ lọc Năm/Quý không cần tải lại trang
    [HttpGet]
    public async Task<IActionResult> GetReportData(int year = 2026, int quarter = 0, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.GetRevenueBiReportAsync(year, quarter, accessToken, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể lấy dữ liệu báo cáo." });
        }

        return Json(new { success = true, data = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> GetHallMatrix(DateTime? startDate, CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        var result = await _apiClient.GetHallScheduleMatrixAsync(startDate, accessToken, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể tải ma trận lịch sảnh." });
        }

        return Json(new { success = true, data = result.Value });
    }
}

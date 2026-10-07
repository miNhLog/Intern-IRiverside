using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class RecommendationController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public RecommendationController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["PublicActivePage"] = "recommendation";

        var defaultModel = new RecommendationRequestViewModel
        {
            EventDate = DateTime.Today.AddMonths(2),
            Shift = "Ca tối",
            OfficialTableCount = 25,
            SpareTableCount = 2,
            GuestCount = 270,
            ExpectedBudget = 250000000,
            BudgetPriority = "BALANCED",
            DesiredStyle = "Hiện đại / lãng mạn"
        };

        // Nếu người dùng đã đăng nhập, tự động trích xuất thông tin
        if (User.Identity?.IsAuthenticated == true)
        {
            defaultModel.Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            defaultModel.CoupleName = User.FindFirstValue(ClaimTypes.Name) ?? "";
            defaultModel.PhoneNumber = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone") ?? "";

            // Lấy token từ Cookie an toàn (không gọi HttpContext.Session)
            var token = Request.Cookies["accessToken"];
            if (!string.IsNullOrEmpty(token) && string.IsNullOrEmpty(defaultModel.PhoneNumber))
            {
                var userRes = await _apiClient.GetCurrentUserAsync(token, cancellationToken);
                if (userRes.Succeeded && userRes.Value != null)
                {
                    defaultModel.CoupleName = string.IsNullOrEmpty(defaultModel.CoupleName) ? userRes.Value.FullName : defaultModel.CoupleName;
                    defaultModel.PhoneNumber = userRes.Value.PhoneNumber ?? defaultModel.PhoneNumber;
                    defaultModel.Email = userRes.Value.Email ?? defaultModel.Email;
                }
            }
        }

        return View(defaultModel);
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeTop3([FromBody] RecommendationRequestViewModel request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Dữ liệu khảo sát chưa hợp lệ, vui lòng kiểm tra lại." });
        }

        var result = await _apiClient.GetTop3RecommendationsAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return Json(new { success = false, message = result.Error ?? "Không thể phân tích gói tiệc lúc này." });
        }

        return Json(new { success = true, data = result.Value });
    }

    // Lấy ma trận lịch sảnh trống
    [HttpGet]
    public async Task<IActionResult> GetHallAvailability(string? date, CancellationToken cancellationToken)
    {
        var queryDate = string.IsNullOrWhiteSpace(date) ? DateTime.Today.AddMonths(2).ToString("yyyy-MM-dd") : date;
        var result = await _apiClient.GetHallAvailabilityRealtimeAsync(queryDate, cancellationToken);

        if (result.Succeeded && result.Value != null)
        {
            return Json(new { success = true, data = result.Value });
        }

        return Json(new { success = false, message = result.Error ?? "Không thể tải dữ liệu lịch sảnh từ cơ sở dữ liệu." });
    }

    [HttpPost]
    public async Task<IActionResult> RegisterBooking([FromBody] PublicBookingRequestViewModel request, CancellationToken cancellationToken)
    {
        // 1. Tự động lấy UserId của tài khoản đang đăng nhập
        if (User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out var uid))
            {
                request.UserId = uid;
            }
        }

        var result = await _apiClient.RegisterPublicBookingAsync(request, cancellationToken);

        if (result.Succeeded)
        {
            // 2. Tự động lưu SĐT vào Cookie để khi chuyển sang trang "Tiệc của tôi" sẽ tự nhận diện
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                Response.Cookies.Append("customer_phone", request.PhoneNumber.Trim(), new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30),
                    IsEssential = true
                });
            }

            return Json(new { success = true, message = "Đăng ký giữ chỗ thành công!", bookingCode = result.Value });
        }

        return Json(new { success = false, message = result.Error ?? "Không thể lưu đơn đăng ký tiệc." });
    }
}
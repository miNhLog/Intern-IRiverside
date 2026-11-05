using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class RecommendationController : Controller
{
    // SucChuaToiDa trong SanhTiec là SỐ KHÁCH (không phải bàn). Sảnh lớn nhất hiện tại
    // (Grand Ballroom) có SucChuaToiDa = 2000 khách, quy chuẩn 10 khách/bàn => 200 bàn.
    // Giới hạn dưới 150 khách lấy từ sức chứa tối thiểu nhỏ nhất trong catalog.
    private const int MinGuestCount = 150;
    private const int MaxGuestCount = 2000;
    private const int GuestsPerTable = 10;
    private const int MaxTotalTables = MaxGuestCount / GuestsPerTable;
    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedServiceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DV001", "DV002", "DV003", "DV004", "DV005"
    };

    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(IRiversideApiClient apiClient, ILogger<RecommendationController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
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
        // Try-catch bao bọc TOÀN BỘ action Frontend: log full ex.ToString(),
        // không để exception văng 500; trả JSON thân thiện cho JS alert.
        try
        {
        if (request is null)
            return Json(new { success = false, message = "Thiếu dữ liệu khảo sát, vui lòng nhập lại." });

        if (!ModelState.IsValid)
        {
            var firstError = FirstModelStateFieldError();
            if (firstError is { } modelError)
                return Json(new { success = false, field = modelError.Field, message = modelError.Message });
        }

        var surveyError = ValidateSurvey(request);
        if (surveyError is { } error)
            return Json(new { success = false, field = error.Field, message = error.Message });

        // Chuẩn hóa dữ liệu sau khi đã validation. Không tin TableCount client gửi:
        // số bàn chính thức luôn quy đổi từ GuestCount (10 khách/bàn) + bàn dự phòng.
        request.CoupleName = (request.CoupleName ?? string.Empty).Trim();
        request.PhoneNumber = (request.PhoneNumber ?? string.Empty).Trim();
        request.Email = (request.Email ?? string.Empty).Trim();
        request.Shift = NormalizeHallShift(request.Shift);
        request.EventDate = request.EventDate.Date;
        request.OfficialTableCount = (int)Math.Ceiling(request.GuestCount / (decimal)GuestsPerTable);
        request.SpareTableCount = Math.Max(0, request.SpareTableCount);
        request.TableCount = request.OfficialTableCount + request.SpareTableCount;

        var result = await _apiClient.GetTop3RecommendationsAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            _logger.LogWarning("AnalyzeTop3: Backend trả lỗi: {Error}", result.Error);
            ViewBag.RecommendationError = "Không thể phân tích gói tiệc lúc này, vui lòng thử lại sau.";
            return Json(new { success = false, message = "Không thể tải gợi ý lúc này. Bạn có thể xem danh sách sảnh hoặc thử lại sau.", fallbackUrl = "/halls" });
        }

        return Json(new { success = true, data = result.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalyzeTop3 thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            ViewBag.RecommendationError = "Hệ thống gợi ý đang bận, vui lòng thử lại sau.";
            return Json(new { success = false, message = "Hệ thống gợi ý đang bận. Bạn có thể xem danh sách sảnh hoặc thử lại sau.", fallbackUrl = "/halls", traceId = HttpContext.TraceIdentifier });
        }
    }

    // Lấy ma trận lịch sảnh trống
    [HttpGet]
    public async Task<IActionResult> GetHallAvailability(string? date, CancellationToken cancellationToken)
    {
        try
        {
            var queryDate = string.IsNullOrWhiteSpace(date) ? DateTime.Today.AddMonths(2).ToString("yyyy-MM-dd") : date;
            var result = await _apiClient.GetHallAvailabilityRealtimeAsync(queryDate, cancellationToken);

            if (result.Succeeded && result.Value != null)
            {
                result.Value.QueryDate ??= queryDate;
                result.Value.Halls ??= new List<HeThongDatTiecCuoi_WEB.Models.Recommendation.HallSlotItemViewModel>();
                return Json(new { success = true, data = result.Value });
            }

            return Json(new { success = false, message = result.Error ?? "Không thể tải dữ liệu lịch sảnh từ cơ sở dữ liệu." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHallAvailability thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            // Null-safe: trả ViewModel rỗng thay vì throw để modal không vỡ với lỗi convert Path $.
            return Json(new
            {
                success = true,
                data = new HallAvailabilityResponseViewModel
                {
                    QueryDate = date ?? string.Empty,
                    Halls = new List<HallSlotItemViewModel>()
                }
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableDates(string? from, string? to, string? shift, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _apiClient.GetAvailableDatesAsync(from, to, shift, cancellationToken);
            var list = result.Value ?? new List<AvailableDateItemViewModel>();
            return Json(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAvailableDates thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return Json(new { success = true, data = new List<AvailableDateItemViewModel>() });
        }
    }

    // Chi tiết thực đơn kèm giá từng món (dùng cho tùy chỉnh thực đơn trong Top 3).
    // Proxy tới Backend GET api/menus/public/{menuId} (AllowAnonymous, đã kèm Price).
    [HttpGet]
    public async Task<IActionResult> GetMenuDetail(int menuId, CancellationToken cancellationToken)
    {
        try
        {
            if (menuId <= 0)
                return Json(new { success = false, message = "Mã thực đơn không hợp lệ." });
            var result = await _apiClient.GetPublicMenuDetailAsync(menuId, cancellationToken);
            if (result.Succeeded && result.Value != null)
                return Json(new { success = true, data = result.Value });
            return Json(new { success = false, message = result.Error ?? "Không thể tải chi tiết thực đơn." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMenuDetail thất bại (menuId={MenuId}).", menuId);
            return Json(new { success = false, message = "Không thể tải chi tiết thực đơn lúc này." });
        }
    }

    // Toàn bộ món ăn kèm giá (dùng cho tùy chỉnh thực đơn trong Top 3).
    // Proxy tới Backend GET api/dishes/public (AllowAnonymous).
    [HttpGet]
    public async Task<IActionResult> GetPublicDishes(string? keyword, string? category, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _apiClient.GetPublicDishesAsync(keyword, category, cancellationToken: cancellationToken);
            var list = result.Value ?? new List<HeThongDatTiecCuoi_WEB.Models.Menus.PublicDishDto>();
            return Json(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPublicDishes thất bại.");
            return Json(new { success = true, data = new List<HeThongDatTiecCuoi_WEB.Models.Menus.PublicDishDto>() });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RegisterBooking([FromBody] PublicBookingRequestViewModel request, CancellationToken cancellationToken)
    {
        // Bọc toàn bộ để sự cố mạng/Backend không văng 500 HTML làm JS res.json() nổ.
        try
        {
            if (request is null)
                return Json(new { success = false, message = "Thiếu dữ liệu đăng ký giữ chỗ." });
        // Chuẩn hóa ca + ngày trước khi forward sang Backend để Backend luôn resolve
        // được LichSanh (tránh gửi "Ca Tối (17:30 – 21:30)" gây lỗi "Lịch sảnh không tồn tại").
        request.Shift = NormalizeHallShift(request.Shift);
        request.EventDate = request.EventDate.Date;

        var result = await _apiClient.RegisterPublicBookingAsync(request, cancellationToken);

        if (result.Succeeded)
        {
            var booking = result.Value;
            return Json(new
            {
                success = true,
                message = "Đăng ký giữ chỗ thành công!",
                bookingCode = booking?.BookingCode,
                statusCode = booking?.StatusCode,
                statusName = booking?.StatusName,
                holdExpiresAt = booking?.HoldExpiresAt,
                emailSent = booking?.EmailSent ?? false
            });
        }

        return Json(new { success = false, message = result.Error ?? "Không thể lưu đơn đăng ký tiệc." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterBooking thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return Json(new { success = false, message = "Không thể gửi giữ chỗ lúc này, vui lòng thử lại sau.", traceId = HttpContext.TraceIdentifier });
        }
    }

    private (string? Field, string Message)? FirstModelStateFieldError()
    {
        foreach (var entry in ModelState)
        {
            var message = entry.Value?.Errors
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            if (string.IsNullOrWhiteSpace(message))
                continue;

            var key = entry.Key.Split('.').LastOrDefault() ?? entry.Key;
            var field = key switch
            {
                nameof(RecommendationRequestViewModel.CoupleName) => "coupleName",
                nameof(RecommendationRequestViewModel.PhoneNumber) => "phoneNumber",
                nameof(RecommendationRequestViewModel.Email) => "email",
                nameof(RecommendationRequestViewModel.EventDate) => "eventDate",
                nameof(RecommendationRequestViewModel.Shift) => "shift",
                nameof(RecommendationRequestViewModel.GuestCount) => "guestCount",
                nameof(RecommendationRequestViewModel.SpareTableCount) => "spareTableCount",
                nameof(RecommendationRequestViewModel.ExpectedBudget) => "expectedBudget",
                nameof(RecommendationRequestViewModel.DesiredStyle) => "desiredStyle",
                _ => null
            };
            return (field, message!);
        }
        return null;
    }

    private static (string Field, string Message)? ValidateSurvey(RecommendationRequestViewModel request)
    {
        var coupleName = (request.CoupleName ?? string.Empty).Trim();
        if (coupleName.Length < 2 || coupleName.Length > 150)
            return ("coupleName", "Họ tên phải từ 2 đến 150 ký tự.");

        var phone = (request.PhoneNumber ?? string.Empty).Trim();
        if (!PhoneRegex.IsMatch(phone))
            return ("phoneNumber", "Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0 (VD: 0912345678).");

        var email = (request.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            return ("email", "Vui lòng nhập email để nhận bảng dự toán.");
        if (email.Length > 255 || !EmailRegex.IsMatch(email))
            return ("email", "Email không hợp lệ (tối đa 255 ký tự).");

        var eventDate = request.EventDate.Date;
        if (eventDate == default)
            return ("eventDate", "Vui lòng chọn ngày tổ chức dự kiến.");
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (eventDate < minDate)
            return ("eventDate", "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất.");
        if (eventDate > maxDate)
            return ("eventDate", "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn.");

        var shift = NormalizeHallShift(request.Shift);
        if (shift != "Ca trưa" && shift != "Ca tối")
            return ("shift", "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối.");

        if (request.GuestCount < MinGuestCount || request.GuestCount > MaxGuestCount)
            return ("guestCount", $"Số lượng khách từ {MinGuestCount} đến {MaxGuestCount} (quy chuẩn {GuestsPerTable} khách/bàn).");

        if (request.SpareTableCount < 0 || request.SpareTableCount > 10)
            return ("spareTableCount", "Bàn dự phòng từ 0 đến 10 bàn.");

        var officialTables = (int)Math.Ceiling(request.GuestCount / (decimal)GuestsPerTable);
        var totalTables = officialTables + Math.Max(0, request.SpareTableCount);
        if (totalTables > MaxTotalTables)
            return ("spareTableCount", $"Tổng {totalTables} bàn (kể cả bàn dự phòng) vượt sức chứa sảnh lớn nhất ({MaxTotalTables} bàn / {MaxGuestCount} khách). Vui lòng giảm số khách hoặc bàn dự phòng.");

        // Giữ nguyên giới hạn sản phẩm hiện có (100 triệu – 3 tỷ) và mức sàn theo số bàn;
        // đây là quy tắc đang được UC12 áp dụng, không phải giới hạn mới của UC11.
        const decimal MinBudgetAbsolute = 100_000_000m;
        const decimal MaxBudgetAbsolute = 3_000_000_000m;
        if (request.ExpectedBudget < MinBudgetAbsolute || request.ExpectedBudget > MaxBudgetAbsolute)
            return ("expectedBudget", "Tổng ngân sách dự trù tối thiểu 100 triệu (chi phí tối thiểu ~103,3 triệu cho 150 khách) đến tối đa 3 tỷ VNĐ.");
        var minForTables = 38_000_000m + totalTables * 4_350_000m;
        if (request.ExpectedBudget < minForTables)
            return ("expectedBudget", $"Với {totalTables} bàn ({request.GuestCount} khách), chi phí tối thiểu khoảng {minForTables:N0} đ (chưa gồm dịch vụ). Vui lòng tăng ngân sách lên tối thiểu {Math.Max(MinBudgetAbsolute, minForTables):N0} đ hoặc giảm số khách để tổng chi phí không vượt ngân sách.");

        var style = (request.DesiredStyle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(style))
            return ("desiredStyle", "Vui lòng chọn phong cách bài trí.");
        if (style.Length > 100)
            return ("desiredStyle", "Phong cách tối đa 100 ký tự.");

        var invalidService = request.SelectedServiceCodes?
            .FirstOrDefault(code => !AllowedServiceCodes.Contains((code ?? string.Empty).Trim()));
        if (invalidService != null)
            return ("selectedServiceCodes", "Dịch vụ ưu tiên không hợp lệ. Vui lòng chọn lại từ danh sách.");

        return null;
    }

    private static string NormalizeHallShift(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Ca tối";
        var s = raw.Trim();
        if (s.Contains("trưa", StringComparison.OrdinalIgnoreCase) || s.Contains("trua", StringComparison.OrdinalIgnoreCase))
            return "Ca trưa";
        if (s.Contains("tối", StringComparison.OrdinalIgnoreCase) || s.Contains("toi", StringComparison.OrdinalIgnoreCase))
            return "Ca tối";
        return s;
    }
}

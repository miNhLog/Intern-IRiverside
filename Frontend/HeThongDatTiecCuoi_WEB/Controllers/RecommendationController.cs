using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class RecommendationController : Controller
{
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
        {
            ViewBag.RecommendationError = "Thiếu dữ liệu khảo sát.";
            return Json(new { success = false, message = "Thiếu dữ liệu khảo sát, vui lòng nhập lại." });
        }
        if (!ModelState.IsValid)
        {
            // Trả message chi tiết field đầu tiên thay vì chung chung để JS alert đúng chỗ sai.
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            ViewBag.RecommendationError = "Dữ liệu khảo sát chưa hợp lệ.";
            return Json(new { success = false, message = firstError ?? "Dữ liệu khảo sát chưa hợp lệ, vui lòng kiểm tra lại." });
        }
        // Ràng buộc Riverside Palace: nhận đặt tiệc trước từ 30 ngày đến tối đa 365 ngày.
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (request.EventDate.Date < minDate)
            return Json(new { success = false, message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất." });
        if (request.EventDate.Date > maxDate)
            return Json(new { success = false, message = "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn." });
        // Chuẩn hóa ca/ngày trước khi gọi Backend (tránh lệch hoa/thường hoặc chuỗi dài kèm giờ).
        request.Shift = NormalizeHallShift(request.Shift);
        request.EventDate = request.EventDate.Date;
        // Chuẩn hóa GuestCount <-> TableCount trước khi gọi Backend.
        if (request.GuestCount <= 0 && (request.TableCount > 0 || request.OfficialTableCount > 0))
            request.GuestCount = Math.Max(request.TableCount, request.OfficialTableCount + request.SpareTableCount) * 10;
        if (request.OfficialTableCount <= 0 && request.GuestCount > 0)
            request.OfficialTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        request.TableCount = request.OfficialTableCount + Math.Max(0, request.SpareTableCount);
        // P0: Grand Ballroom tối đa ~140 bàn — chặn trước khi gọi Backend.
        if (request.TableCount > 140)
            return Json(new { success = false, message = "Quy mô vượt quá sức chứa sảnh lớn nhất Grand Ballroom (tối đa 140 bàn / 1400 khách). Hai bạn vui lòng giảm số khách hoặc liên hệ Riverside Palace để tách ca tổ chức nhé!" });
        // Ràng buộc ngân sách: min tuyệt đối 100tr (min thực tế ~103,3tr cho 150 khách:
        // sảnh Elbe 20tr + menu 4,35tr/bàn × 15 bàn + decor 18tr). Gợi ý chỉ vượt tối đa 10tr.
        const decimal MinBudgetAbsolute = 100_000_000m;
        const decimal MaxBudgetAbsolute = 3_000_000_000m;
        if (request.ExpectedBudget < MinBudgetAbsolute || request.ExpectedBudget > MaxBudgetAbsolute)
            return Json(new { success = false, message = "Tổng ngân sách dự trù tối thiểu 100 triệu (chi phí tối thiểu ~103,3 triệu cho 150 khách) đến tối đa 3 tỷ VNĐ." });
        var minForTables = 38_000_000m + Math.Max(0, request.TableCount) * 4_350_000m;
        if (request.ExpectedBudget < minForTables)
            return Json(new
            {
                success = false,
                message = $"Với {request.TableCount} bàn ({request.GuestCount} khách), chi phí tối thiểu khoảng {minForTables:N0} đ (chưa gồm dịch vụ). Vui lòng tăng ngân sách lên tối thiểu {Math.Max(MinBudgetAbsolute, minForTables):N0} đ hoặc giảm số khách để gợi ý không vượt quá 10 triệu."
            });

        var result = await _apiClient.GetTop3RecommendationsAsync(request, cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            _logger.LogWarning("AnalyzeTop3: Backend trả lỗi: {Error}", result.Error);
            ViewBag.RecommendationError = result.Error;
            return Json(new { success = false, message = result.Error ?? "Không thể phân tích gói tiệc lúc này, vui lòng thử lại sau." });
        }

        return Json(new { success = true, data = result.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalyzeTop3 thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            ViewBag.RecommendationError = "Hệ thống gợi ý đang bận, vui lòng thử lại sau.";
            return Json(new { success = false, message = "Hệ thống gợi ý đang bận, vui lòng thử lại sau.", traceId = HttpContext.TraceIdentifier });
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
            var result = await _apiClient.GetPublicDishesAsync(keyword, category, cancellationToken);
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
        // Chỉ liên kết hồ sơ đặt tiệc với tài khoản khách hàng. Tài khoản nội bộ
        // (quản lý/nhân viên) thử chức năng không được biến thành hồ sơ khách hàng.
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Customer))
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterBooking thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return Json(new { success = false, message = "Không thể gửi giữ chỗ lúc này, vui lòng thử lại sau.", traceId = HttpContext.TraceIdentifier });
        }
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

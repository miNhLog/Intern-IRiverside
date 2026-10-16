using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/recommendations")]
[ApiController]
public sealed class RecommendationController : ControllerBase
{
    // Trọng số chuẩn W* (tổng 1.0) — đồng bộ 4 chiều C# <-> Python (P0).
    // Khớp AI_Service/app/core/trained_weights.json.
    private const double BudgetWeight = 0.3004;
    private const double SpaceWeight = 0.3198;
    private const double StyleWeight = 0.1799;
    private const double ServiceWeight = 0.1999;
    private const decimal OverBudgetToleranceVnd = 10_000_000m;

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceOptions _aiOptions;
    private readonly ILogger<RecommendationController> _logger;
    private readonly IEmailService? _emailService;

    // Python FastAPI trả snake_case (top_3, hall_id, total_estimated_cost...).
    // C# dùng PascalCase -> bắt buộc case-insensitive + JsonPropertyName đầy đủ,
    // nếu không deserialize ra null và dễ gây 500 ở tầng trên.
    private static readonly JsonSerializerOptions AiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RecommendationController(
        ApplicationDbContext context,
        IStatusService statusService,
        IHttpClientFactory httpClientFactory,
        IOptions<AiServiceOptions> aiOptions,
        ILogger<RecommendationController> logger,
        IEmailService? emailService = null)
    {
        _context = context;
        _statusService = statusService;
        _httpClientFactory = httpClientFactory;
        _aiOptions = aiOptions.Value;
        _logger = logger;
        _emailService = emailService;
    }

    /// <summary>
    /// Gọi AI Microservice (FastAPI WSM, port 8000) để xếp hạng Top-3 gói tiệc.
    /// Truy vấn LichSanh (HallSchedules) lấy SanhTiecID đã đặt truyền occupied_hall_ids.
    /// Fallback heuristic nội bộ nếu AI timeout (&gt;5s) hoặc lỗi.
    /// Route alias "analyze" khớp Bảng 3.5 báo cáo (giữ route cũ cho FE hiện tại).
    /// </summary>
    [HttpPost("suggest-top3-ai")]
    [HttpPost("analyze")]
    public async Task<IActionResult> SuggestTop3ViaAi([FromBody] RecommendationRequestDto? request)
    {
        // Try-catch bao bọc TOÀN BỘ action: bất kỳ Exception nào cũng log full
        // ex.ToString() kèm traceId rồi trả fallback 200 hoặc 500 có traceId,
        // không để UseExceptionHandler trả 500 ProblemDetails rỗng.
        try
        {
        if (request is null)
            return BadRequest(new { message = "Thiếu dữ liệu khảo sát trong body." });

        // Null-safe: DTO mặc định string.Empty nhưng JSON có thể gửi null tường minh.
        var desiredShift = (request.DesiredShift ?? string.Empty).Trim();
        var desiredStyle = (request.DesiredStyle ?? string.Empty).Trim();
        if (desiredShift != HallShiftNames.Lunch && desiredShift != HallShiftNames.Dinner)
            return BadRequest(new { message = "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối." });
        if (string.IsNullOrWhiteSpace(desiredStyle))
            return BadRequest(new { message = "Phong cách mong muốn không được để trống." });
        // Fallback GuestCount từ TableCount trước mọi validation để JS cũ không bị 400.
        if (request.GuestCount <= 0 && (request.TableCount > 0 || request.OfficialTableCount > 0))
            request.GuestCount = Math.Max(request.TableCount, request.OfficialTableCount + request.SpareTableCount) * 10;
        if (request.GuestCount <= 0)
            return BadRequest(new { message = "Số lượng khách phải lớn hơn 0." });
        // Ràng buộc ngân sách: min tuyệt đối 100tr (min thực tế ~103,3tr cho 150 khách:
        // sảnh Elbe 20tr + menu 4,35tr/bàn × 15 bàn + decor 18tr). Gợi ý chỉ vượt tối đa 10tr.
        const decimal MinBudgetAbsolute = 100_000_000m;
        const decimal MaxBudgetAbsolute = 3_000_000_000m;
        if (request.ExpectedBudget < MinBudgetAbsolute || request.ExpectedBudget > MaxBudgetAbsolute)
            return BadRequest(new { message = "Tổng ngân sách dự trù tối thiểu 100 triệu (chi phí tối thiểu ~103,3 triệu cho 150 khách) đến tối đa 3 tỷ VNĐ." });

        // Parse ngày an toàn, chuẩn hóa về 00:00. So sánh theo khoảng [targetDate, nextDay)
        // để tránh lệch giờ SQL datetime và lỗi DateOnly trên Windows.
        DateTime targetDate;
        try
        {
            targetDate = request.DesiredDate.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DesiredDate không hợp lệ: {Raw}", request.DesiredDate);
            return BadRequest(new { message = "Ngày tổ chức không hợp lệ." });
        }
        // Ràng buộc Riverside Palace: nhận đặt tiệc trước từ 30 ngày đến tối đa 365 ngày (1 năm).
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (targetDate < minDate)
            return BadRequest(new { message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất." });
        if (targetDate > maxDate)
            return BadRequest(new { message = "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn." });
        var nextDay = targetDate.AddDays(1);

        byte existingDataStatusId;
        List<int> occupiedHallIds = new();
        try
        {
            existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);

            // LichSanh đã đặt trong ngày + ca -> occupied_hall_ids cho AI.
            // Default-to-Available: kín khi LichSanh != AVAILABLE HOẶC có DatTiec hoạt động (trừ Đã hủy).
            // So sánh Shift case-insensitive (DB cũ "Ca Tối" vs chuẩn "Ca tối").
            // Null-safe: Status/Hall navigation non-null trong model (null!) nhưng vẫn
            // bọc try-catch để SqlException/NullReference không văng 500 ra UseExceptionHandler.
            var desiredShiftNormAi = NormalizeHallShift(desiredShift).ToLower();
            occupiedHallIds = await _context.HallSchedules.AsNoTracking()
                .Where(schedule =>
                    schedule.Date >= targetDate && schedule.Date < nextDay &&
                    schedule.Shift.ToLower() == desiredShiftNormAi &&
                    schedule.DataStatusId == existingDataStatusId &&
                    schedule.Status != null &&
                    schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                    schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
                .Select(schedule => schedule.HallId)
                .Distinct()
                .ToListAsync();
            var bookingOccupiedHallIds = await _context.WeddingBookings.AsNoTracking()
                .Where(booking =>
                    booking.HallSchedule.Date >= targetDate && booking.HallSchedule.Date < nextDay &&
                    booking.HallSchedule.Shift.ToLower() == desiredShiftNormAi &&
                    booking.HallSchedule.DataStatusId == existingDataStatusId &&
                    booking.Status != null &&
                    booking.Status.StatusGroup == StatusGroups.Booking &&
                    BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                .Select(booking => booking.HallSchedule.HallId)
                .Distinct()
                .ToListAsync();
            occupiedHallIds = occupiedHallIds.Union(bookingOccupiedHallIds).Distinct().ToList();
        }
        catch (Exception ex)
        {
            // Lỗi DB trước khi gọi AI: log chi tiết + fallback nội bộ để vẫn trả 200.
            _logger.LogError(ex, "SuggestTop3ViaAi: lỗi truy vấn occupied_hall_ids (date={Date}, shift={Shift}, traceId={TraceId}). Chi tiết: {Detail}",
                targetDate.ToString("yyyy-MM-dd"), desiredShift, HttpContext.TraceIdentifier, ex.ToString());
            try
            {
                var safeStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
                var tableCountSafe = (int)Math.Ceiling((request.GuestCount <= 0 ? 1 : request.GuestCount) / 10m);
                var fallbackEarly = await BuildFallbackTop3Async(request, desiredShift, desiredStyle,
                    targetDate, nextDay, safeStatusId, tableCountSafe);
                return Ok(new
                {
                    success = true,
                    source = "fallback-internal",
                    aiBaseUrl = _aiOptions.BaseUrl,
                    aiError = "DB query failed before AI call: " + ex.Message,
                    traceId = HttpContext.TraceIdentifier,
                    occupiedHallIds,
                    data = fallbackEarly
                });
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "SuggestTop3ViaAi: fallback sớm cũng thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, inner.ToString());
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }

        var tableCount = request.TableCount > 0
            ? request.TableCount
            : (request.OfficialTableCount + request.SpareTableCount) > 0
                ? request.OfficialTableCount + request.SpareTableCount
                : (int)Math.Ceiling(request.GuestCount / 10m);
        var aiShift = desiredShift == HallShiftNames.Lunch ? "Ca trua" : "Ca toi";
        var priorityServices = (request.ServiceNeeds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var aiPayload = new
        {
            guest_count = request.GuestCount,
            table_count = tableCount,
            expected_budget = request.ExpectedBudget,
            event_date = targetDate.ToString("yyyy-MM-dd"),
            shift = aiShift,
            desired_style = desiredStyle,
            priority_services = priorityServices,
            occupied_hall_ids = occupiedHallIds
        };

        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_aiOptions.TimeoutSeconds));
            using var response = await client.PostAsJsonAsync("api/v1/ai/wsm/rank", aiPayload, cts.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            // Validate snake_case bằng DTO có [JsonPropertyName] + case-insensitive.
            // Nếu Python đổi contract mà parse ra null -> rơi xuống fallback thay vì trả null cho client.
            AiRankResponse? typed = null;
            try
            {
                typed = JsonSerializer.Deserialize<AiRankResponse>(json, AiJsonOptions);
            }
            catch (JsonException jex)
            {
                _logger.LogWarning(jex, "SuggestTop3ViaAi: AI trả JSON không đúng contract snake_case. traceId={TraceId}. Body: {Body}",
                    HttpContext.TraceIdentifier, json.Length > 2000 ? json[..2000] : json);
            }
            if (typed is null || typed.Top3 is null)
            {
                _logger.LogWarning("SuggestTop3ViaAi: AI deserialize ra null (snake_case mismatch?). traceId={TraceId}. Body: {Body}",
                    HttpContext.TraceIdentifier, json.Length > 2000 ? json[..2000] : json);
            }
            using var doc = JsonDocument.Parse(json);
            // P0 fallback 0 combo: AI trả rỗng -> BE tự tính suggestedNext, không trả lỗi cụt.
            var aiTop = typed?.Top3 ?? typed?.Top3Recommendations;
            object? suggestedNextAi = null;
            int aiCount = aiTop?.Count ?? 0;
            if (aiCount == 0)
            {
                try
                {
                    var sg = await BuildSuggestedNextAsync(targetDate, desiredShift, tableCount, request.ExpectedBudget, existingDataStatusId);
                    suggestedNextAi = new
                    {
                        minRequiredBudget = sg.MinRequiredBudget,
                        suggestedDates = sg.SuggestedDates.Select(d => new
                        {
                            date = d.Date,
                            shift = d.Shift,
                            availableHalls = d.AvailableHalls
                        }),
                        reason = sg.Reason
                    };
                }
                catch (Exception sgEx)
                {
                    _logger.LogWarning(sgEx, "SuggestTop3ViaAi: tính suggestedNext thất bại. traceId={TraceId}.", HttpContext.TraceIdentifier);
                }
            }
            return Ok(new
            {
                success = true,
                source = "ai-service",
                aiBaseUrl = _aiOptions.BaseUrl,
                occupiedHallIds,
                count = aiCount,
                suggestedNext = suggestedNextAi,
                data = JsonSerializer.Deserialize<object>(json, AiJsonOptions)
            });
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
        {
            _logger.LogWarning(ex, "SuggestTop3ViaAi: AI service lỗi ({BaseUrl}), dùng fallback nội bộ. traceId={TraceId}. Chi tiết: {Detail}",
                _aiOptions.BaseUrl, HttpContext.TraceIdentifier, ex.ToString());
            // Fallback heuristic nội bộ khi AI timeout (>5s) hoặc lỗi.
            try
            {
                var fallback = await BuildFallbackTop3Async(request, desiredShift, desiredStyle,
                    targetDate, nextDay, existingDataStatusId, tableCount);
                return Ok(new
                {
                    success = true,
                    source = "fallback-internal",
                    aiBaseUrl = _aiOptions.BaseUrl,
                    aiError = ex.Message,
                    traceId = HttpContext.TraceIdentifier,
                    occupiedHallIds,
                    data = fallback
                });
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "SuggestTop3ViaAi: fallback nội bộ thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, fallbackEx.ToString());
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }
        } // end outer try
        catch (Exception ex)
        {
            // Lưới an toàn cuối: mọi lỗi ngoài dự kiến (payload, HttpClient config, ...)
            // đều log full ex.ToString() + traceId và trả 500 có traceId, không để 500 rỗng.
            _logger.LogError(ex, "SuggestTop3ViaAi: lỗi ngoài dự kiến. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            try
            {
                if (request is not null)
                {
                    var desiredShiftFb = (request.DesiredShift ?? string.Empty).Trim();
                    var desiredStyleFb = (request.DesiredStyle ?? string.Empty).Trim();
                    DateTime targetDateFb;
                    try { targetDateFb = request.DesiredDate.Date; }
                    catch { targetDateFb = DateTime.Today; }
                    var nextDayFb = targetDateFb.AddDays(1);
                    var statusIdFb = await GetDataStatusIdAsync(DataStatusCodes.Existing);
                    var tableCountFb = (int)Math.Ceiling((request.GuestCount <= 0 ? 1 : request.GuestCount) / 10m);
                    var fallback = await BuildFallbackTop3Async(request, desiredShiftFb, desiredStyleFb,
                        targetDateFb, nextDayFb, statusIdFb, tableCountFb);
                    return Ok(new
                    {
                        success = true,
                        source = "fallback-internal",
                        aiBaseUrl = _aiOptions.BaseUrl,
                        aiError = "Unexpected: " + ex.Message,
                        traceId = HttpContext.TraceIdentifier,
                        data = fallback
                    });
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "SuggestTop3ViaAi: fallback cuối cũng thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                    HttpContext.TraceIdentifier, fallbackEx.ToString());
            }
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tạo gợi ý lúc này, vui lòng thử lại.",
                detail = ex.Message,
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Default-to-Available (BR03): sảnh hoạt động, đủ sức chứa, KHÔNG bị chặn
    /// (LichSanh != AVAILABLE hoặc DatTiec hoạt động chiếm giữ) đều là ứng viên.
    /// Ngày xa chưa có record LichSanh -> HallScheduleId = 0, tự INSERT khi đặt giữ chỗ.
    /// </summary>
    private async Task<List<CandidateHallItem>> ResolveAvailableCandidatesAsync(
        DateTime targetDate, DateTime nextDay, string desiredShift, int guestCount, byte existingDataStatusId)
    {
        // Chuẩn hóa Shift case-insensitive: DB cũ có thể lưu "Ca Tối"/"Ca Trưa" lệch hoa/thường
        // so với chuẩn HallShiftNames ("Ca tối"/"Ca trưa"). Dùng ToLower() để dịch được sang SQL LOWER().
        var desiredShiftNorm = NormalizeHallShift(desiredShift).ToLower();
        var blockedHallIds = (await _context.HallSchedules.AsNoTracking()
            .Where(schedule =>
                schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.Shift.ToLower() == desiredShiftNorm &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
            .Select(schedule => schedule.HallId)
            .Distinct()
            .ToListAsync())
            .Union(await _context.WeddingBookings.AsNoTracking()
            .Where(booking =>
                booking.HallSchedule.Date >= targetDate && booking.HallSchedule.Date < nextDay &&
                booking.HallSchedule.Shift.ToLower() == desiredShiftNorm &&
                booking.HallSchedule.DataStatusId == existingDataStatusId &&
                booking.Status != null &&
                booking.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            .Select(booking => booking.HallSchedule.HallId)
            .Distinct()
            .ToListAsync())
            .ToHashSet();
        var scheduleIdByHallId = await _context.HallSchedules.AsNoTracking()
            .Where(schedule =>
                schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.Shift.ToLower() == desiredShiftNorm &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available)
            .GroupBy(schedule => schedule.HallId)
            .Select(g => new { HallId = g.Key, HallScheduleId = g.Min(s => s.HallScheduleId) })
            .ToDictionaryAsync(x => x.HallId, x => x.HallScheduleId);
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall =>
                hall.DataStatusId == existingDataStatusId &&
                hall.Status != null &&
                hall.Status.StatusGroup == StatusGroups.Hall &&
                hall.Status.StatusCode == HallStatusCodes.Active &&
                guestCount <= hall.MaximumCapacity &&
                (!hall.MinimumCapacity.HasValue || guestCount >= hall.MinimumCapacity.Value))
            .ToListAsync();
        return halls
            .Where(hall => !blockedHallIds.Contains(hall.HallId))
            .Select(hall => new CandidateHallItem
            {
                HallScheduleId = scheduleIdByHallId.TryGetValue(hall.HallId, out var sid) ? sid : 0,
                Hall = hall
            })
            .ToList();
    }

    private sealed class CandidateHallItem
    {
        public int HallScheduleId { get; set; }
        public Hall Hall { get; set; } = null!;
    }

    private async Task<object> BuildFallbackTop3Async(
        RecommendationRequestDto request, string desiredShift, string desiredStyle,
        DateTime targetDate, DateTime nextDay, byte existingDataStatusId, int tableCount)
    {
        var style = (desiredStyle ?? request.DesiredStyle ?? string.Empty).Trim();
        // Dùng chung ResolveAvailableCandidatesAsync với flow chính để không lệch
        // logic Default-to-Available (tránh drift khi sửa 1 nơi quên nơi còn lại).
        var candidateSchedules = await ResolveAvailableCandidatesAsync(
            targetDate, nextDay, desiredShift, request.GuestCount, existingDataStatusId);

        var activeMenus = await _context.Menus.AsNoTracking()
            .Where(menu =>
                menu.MenuType == MenuTypes.Standard &&
                menu.DataStatusId == existingDataStatusId &&
                menu.Status != null &&
                menu.Status.StatusGroup == StatusGroups.Menu &&
                menu.Status.StatusCode == MenuStatusCodes.Active)
            .ToListAsync();

        var activeDecors = await _context.DecorPackages.AsNoTracking()
            .Where(decor =>
                decor.DataStatusId == existingDataStatusId &&
                decor.Status != null &&
                decor.Status.StatusGroup == StatusGroups.DecorationPackage &&
                decor.Status.StatusCode == BusinessStatusCodes.Active)
            .Select(decor => new
            {
                Decor = decor,
                ImageUrl = decor.Images
                    .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // Ảnh đại diện sảnh (primary) — fallback trước đây thiếu nên card toàn placeholder.
        var hallImageById = await _context.ImageAssets.AsNoTracking()
            .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId && image.HallId.HasValue)
            .GroupBy(image => image.HallId!.Value)
            .Select(g => new { HallId = g.Key, ImagePath = g.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).FirstOrDefault() })
            .ToDictionaryAsync(x => x.HallId, x => x.ImagePath);

        // Dịch vụ ưu tiên (ServiceNeeds "DV001,DV002..."): query trực tiếp bảng DichVu
        // (chỉ lấy ACTIVE) để cộng vào dự toán trọn gói, khớp từng đồng với thẻ hiển thị.
        var priorityServices = await ResolvePriorityServicesAsync(request.ServiceNeeds, existingDataStatusId);
        var servicesTotalFallback = priorityServices.Sum(s => s.Price);

        var priorityCodes = (request.ServiceNeeds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
        var scored = new List<ComboSuggestionDto>();
        foreach (var schedule in candidateSchedules)
        foreach (var menu in activeMenus)
        foreach (var decorCandidate in activeDecors)
        {
            var hall = schedule.Hall;
            var decor = decorCandidate.Decor;
            var menuTotal = menu.PricePerTable * tableCount;
            // Dự toán trọn gói = Giá thuê sảnh + (Đơn giá Menu × Tổng số bàn) + Giá Decor + Tổng giá Dịch vụ ưu tiên.
            var totalCost = hall.RentalPrice + menuTotal + decor.Gia + servicesTotalFallback;
            // Hard filter WSM: est_total <= budget + 10tr (đồng bộ py + C#).
            if (totalCost > request.ExpectedBudget + OverBudgetToleranceVnd)
                continue;
            var budgetDifference = request.ExpectedBudget - totalCost;
            // WSM chuẩn 4 chiều (r1..r4 0..1) — khớp wsm_engine.py.
            var r1 = CalculateBudgetScore(totalCost, request.ExpectedBudget);
            var optimalTables = GetOptimalTables(hall);
            var r2 = CalculateSpaceScore(tableCount, optimalTables, hall.CapacityMaxTables);
            var r3 = CalculateStyleScore(style, decor.PhongCach);
            var includedCodes = priorityServices
                .Select(s => (s.ServiceCode ?? string.Empty).Trim().ToUpperInvariant())
                .Where(c => c.Length > 0)
                .ToList();
            var r4 = CalculateServiceScore(priorityCodes, includedCodes);
            var hitCount = priorityCodes.Count == 0
                ? 0
                : priorityCodes.Count(c => includedCodes.Contains(c, StringComparer.OrdinalIgnoreCase));
            var score = BudgetWeight * r1 + SpaceWeight * r2 + StyleWeight * r3 + ServiceWeight * r4;
            var overallScore = Math.Round(score * 100, 1);
            var minT = hall.CapacityMinTables;
            var maxT = hall.CapacityMaxTables;
            var explanations = BuildExplanations(hall.HallName, minT, maxT, optimalTables,
                decor.TenGoi, decor.PhongCach, style, request.ExpectedBudget, totalCost,
                tableCount, r3, priorityCodes, hitCount);
            scored.Add(new ComboSuggestionDto
            {
                OverallMatchScore = overallScore,
                BudgetMatchScore = Math.Round(r1 * 100, 1),
                SpaceFitScore = Math.Round(r2 * 100, 1),
                StyleThemeScore = Math.Round(r3 * 100, 1),
                RatingCredibilityScore = Math.Round((r2 + r3) / 2 * 100, 1),
                HallScheduleId = schedule.HallScheduleId,
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                HallImageUrl = hallImageById.TryGetValue(hall.HallId, out var hallImgFb) ? hallImgFb : null,
                HallCapacity = hall.MaximumCapacity,
                CapacityMinGuests = hall.MinimumCapacity ?? 150,
                CapacityMaxGuests = hall.MaximumCapacity,
                CapacityMinTables = (hall.MinimumCapacity ?? 150) / 10,
                CapacityMaxTables = hall.MaximumCapacity / 10,
                HallRentalPrice = hall.RentalPrice,
                HallDescription = hall.Description ?? string.Empty,
                TotalEstimatedCost = totalCost,
                BudgetDifference = budgetDifference,
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                MenuPricePerTable = menu.PricePerTable,
                EstimatedTableCount = tableCount,
                MenuTotalPrice = menuTotal,
                MenuDescription = menu.Description ?? string.Empty,
                DecorationPackageId = decor.GoiTrangTriID,
                DecorationPackageName = decor.TenGoi,
                DecorationStyle = decor.PhongCach,
                DecorationPrice = decor.Gia,
                DecorationDescription = decor.MoTa ?? string.Empty,
                DecorationImageUrl = decorCandidate.ImageUrl,
                IncludedServices = priorityServices,
                ServicesTotalPrice = servicesTotalFallback,
                BudgetAnalysisText = budgetDifference >= 0
                    ? $"Trong ngân sách (Tiết kiệm {FormatVnd(budgetDifference)})"
                    : $"Vượt ngân sách ({FormatVnd(Math.Abs(budgetDifference))})",
                RecommendationReasons = explanations,
                Explanations = explanations
            });
        }

        // Diversity Filter: không trả 3 combo giống hệt (chỉ khác tên menu cùng giá).
        // Ưu tiên sảnh khác nhau, sau đó Menu/Decor mức giá khác nhau.
        // Truyền ExpectedBudget để ưu tiên combo trong ngân sách / vượt nhẹ ≤ 10tr.
        var top = SelectDiverseTop3(scored, request.ExpectedBudget);
        for (var index = 0; index < top.Count; index++)
        {
            top[index].Rank = index + 1;
            ApplyComboMeta(top[index], index + 1);
        }
        if (top.Count == 0)
        {
            var suggested = await BuildSuggestedNextAsync(targetDate, desiredShift, tableCount, request.ExpectedBudget, existingDataStatusId);
            return new
            {
                success = true,
                count = 0,
                data = new List<ComboSuggestionDto>(),
                totalCandidatesEvaluated = scored.Count,
                top3 = new List<ComboSuggestionDto>(),
                topCombos = new List<ComboSuggestionDto>(),
                suggestedNext = suggested,
                algorithmNotice = $"Fallback heuristic nội bộ: đã đánh giá {scored.Count} phương án, không có combo trong ngân sách +10tr."
            };
        }
        return new
        {
            success = true,
            count = top.Count,
            data = top,
            totalCandidatesEvaluated = scored.Count,
            top3 = top,
            topCombos = top,
            algorithmNotice = $"Fallback heuristic nội bộ: đã đánh giá {scored.Count} phương án (AI timeout >{_aiOptions.TimeoutSeconds}s hoặc lỗi)."
        };
    }

    private static void ApplyComboMeta(ComboSuggestionDto combo, int rank)
    {
        // Chuẩn hóa tên combo — Frontend render card không bao giờ undefined.
        var (type, title, badge) = rank switch
        {
            1 => ("BALANCED", "Combo Hoàn Hảo (Top 1)", "LỰA CHỌN TỐI ƯU"),
            2 => ("STANDARD", "Combo Tiêu Chuẩn (Top 2)", "CÂN BẰNG CHI PHÍ"),
            _ => ("LUXURY", "Combo Sang Trọng (Top 3)", "TRẢI NGHIỆM ĐẲNG CẤP"),
        };
        combo.ComboType = type;
        combo.ComboTitle = title;
        combo.ComboBadge = badge;
        combo.ComboName = title;
        combo.PackageName = title;
    }

    private static List<ComboSuggestionDto> SelectDiverseTop3(List<ComboSuggestionDto> scored, decimal? expectedBudget = null)
    {
        // Ưu tiên ngân sách: trong ngân sách trước, sau đó vượt nhẹ ≤ 10tr, cuối cùng mới tới vượt xa.
        // Tránh trả combo vượt quá xa gây chênh lệch lớn so với dự trù của khách.
        const decimal ToleranceVnd = 10_000_000m;
        List<ComboSuggestionDto> ordered;
        if (expectedBudget.HasValue && expectedBudget.Value > 0)
        {
            var budget = expectedBudget.Value;
            ordered = scored
                .OrderBy(c => c.TotalEstimatedCost - budget > ToleranceVnd ? 2
                    : c.TotalEstimatedCost - budget > 0 ? 1 : 0)
                .ThenByDescending(c => c.OverallMatchScore)
                .ThenBy(c => c.TotalEstimatedCost)
                .ToList();
        }
        else
        {
            ordered = scored.OrderByDescending(c => c.OverallMatchScore).ToList();
        }
        var picked = new List<ComboSuggestionDto>();
        var usedHalls = new HashSet<int>();
        var usedMenuDecor = new HashSet<string>();
        // Pass 1: sảnh khác nhau + cặp (menu,decor) khác nhau.
        foreach (var c in ordered)
        {
            if (picked.Count >= 3) break;
            var key = $"{c.MenuId}|{c.DecorationPackageId}|{(c.MenuPricePerTable / 1000000):F0}|{(c.DecorationPrice / 1000000):F0}";
            if (!usedHalls.Contains(c.HallId) && !usedMenuDecor.Contains(key))
            {
                picked.Add(c);
                usedHalls.Add(c.HallId);
                usedMenuDecor.Add(key);
            }
        }
        // Pass 2: nới lỏng — chỉ cần sảnh khác hoặc cặp menu/decor khác.
        foreach (var c in ordered)
        {
            if (picked.Count >= 3) break;
            if (picked.Contains(c)) continue;
            var key = $"{c.MenuId}|{c.DecorationPackageId}";
            if (!usedHalls.Contains(c.HallId) || !usedMenuDecor.Contains(key))
            {
                picked.Add(c);
                usedHalls.Add(c.HallId);
                usedMenuDecor.Add(key);
            }
        }
        // Pass 3: vét phần còn lại để luôn đủ 3 (khi DB ít dữ liệu).
        foreach (var c in ordered)
        {
            if (picked.Count >= 3) break;
            if (!picked.Contains(c)) picked.Add(c);
        }
        return picked.Take(3).ToList();
    }

    /// <summary>
    /// P0 fallback 0 combo: minRequiredBudget = sảnh rẻ nhất còn trống
    /// + 4_350_000 × Số bàn + 18_000_000; suggestedDates quét 3 ngày gần nhất
    /// còn sảnh trống phù hợp quy mô bàn (forward từ ngày yêu cầu, tối đa 60 ngày).
    /// </summary>
    private async Task<SuggestedNextDto> BuildSuggestedNextAsync(
        DateTime targetDate, string desiredShift, int tableCount, decimal budget, byte existingDataStatusId)
    {
        var guestCount = Math.Max(1, tableCount * 10);
        var cheapestMenu = await _context.Menus.AsNoTracking()
            .Where(m => m.MenuType == MenuTypes.Standard
                && m.DataStatusId == existingDataStatusId
                && m.Status != null
                && m.Status.StatusGroup == StatusGroups.Menu
                && m.Status.StatusCode == MenuStatusCodes.Active)
            .OrderBy(m => m.PricePerTable)
            .Select(m => m.PricePerTable)
            .FirstOrDefaultAsync();
        if (cheapestMenu <= 0) cheapestMenu = 4_350_000m;
        var cheapestDecor = await _context.DecorPackages.AsNoTracking()
            .Where(d => d.DataStatusId == existingDataStatusId
                && d.Status != null
                && d.Status.StatusGroup == StatusGroups.DecorationPackage
                && d.Status.StatusCode == BusinessStatusCodes.Active)
            .OrderBy(d => d.Gia)
            .Select(d => d.Gia)
            .FirstOrDefaultAsync();
        if (cheapestDecor <= 0) cheapestDecor = 18_000_000m;

        // Sảnh rẻ nhất còn trống ngày yêu cầu mà chứa được quy mô bàn;
        // không còn sảnh nào thì lấy sảnh rẻ nhất chứa được quy mô (kể cả kín) để ước lượng.
        var hallsByPrice = await _context.Halls.AsNoTracking()
            .Where(h => h.DataStatusId == existingDataStatusId
                && h.Status != null
                && h.Status.StatusGroup == StatusGroups.Hall
                && h.Status.StatusCode == HallStatusCodes.Active
                && guestCount <= h.MaximumCapacity
                && (!h.MinimumCapacity.HasValue || guestCount >= h.MinimumCapacity.Value))
            .OrderBy(h => h.RentalPrice)
            .Select(h => new { h.HallId, h.HallName, h.RentalPrice })
            .ToListAsync();
        decimal cheapestHallPrice = 20_000_000m;
        if (hallsByPrice.Count > 0)
        {
            var nextDay = targetDate.AddDays(1);
            var cands = await ResolveAvailableCandidatesAsync(targetDate, nextDay, desiredShift, guestCount, existingDataStatusId);
            var freeIds = cands.Select(c => c.Hall.HallId).ToHashSet();
            var freeCheapest = hallsByPrice.FirstOrDefault(h => freeIds.Contains(h.HallId));
            cheapestHallPrice = (freeCheapest ?? hallsByPrice[0]).RentalPrice;
        }
        var minRequired = cheapestHallPrice + cheapestMenu * Math.Max(0, tableCount) + cheapestDecor;

        var suggestedDates = new List<SuggestedDateDto>();
        var startDay = targetDate.Date;
        for (var day = startDay; day <= startDay.AddDays(60) && suggestedDates.Count < 3; day = day.AddDays(1))
        {
            var dayNext = day.AddDays(1);
            List<CandidateHallItem> free;
            try
            {
                free = await ResolveAvailableCandidatesAsync(day, dayNext, desiredShift, guestCount, existingDataStatusId);
            }
            catch
            {
                continue;
            }
            if (free.Count == 0) continue;
            suggestedDates.Add(new SuggestedDateDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Shift = NormalizeHallShift(desiredShift),
                AvailableHalls = free.Select(c => c.Hall.HallName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList()
            });
        }
        return new SuggestedNextDto
        {
            MinRequiredBudget = minRequired,
            SuggestedDates = suggestedDates,
            Reason = "Ngân sách thấp hơn tối thiểu hoặc ngày đã kín sảnh phù hợp."
        };
    }

    [HttpPost("suggest-top3")]
    public async Task<IActionResult> SuggestTop3Combos([FromBody] RecommendationRequestDto? request)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu dữ liệu khảo sát trong body." });

        // Null-safe: JSON có thể gửi null tường minh dù DTO có default string.Empty.
        var desiredShift = (request.DesiredShift ?? string.Empty).Trim();
        var desiredStyle = (request.DesiredStyle ?? string.Empty).Trim();
        if (desiredShift != HallShiftNames.Lunch && desiredShift != HallShiftNames.Dinner)
            return BadRequest(new { message = "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối." });
        if (string.IsNullOrWhiteSpace(desiredStyle))
            return BadRequest(new { message = "Phong cách mong muốn không được để trống." });
        // Fallback GuestCount từ TableCount/Official+Spare trước Model validation.
        if (request.GuestCount <= 0 && (request.TableCount > 0 || request.OfficialTableCount > 0))
            request.GuestCount = Math.Max(request.TableCount, request.OfficialTableCount + request.SpareTableCount) * 10;
        if (request.GuestCount <= 0)
            return BadRequest(new { message = "Số lượng khách phải lớn hơn 0." });
        // Ràng buộc ngân sách: min tuyệt đối 100tr (min thực tế ~103,3tr cho 150 khách).
        const decimal MinBudgetAbsolute2 = 100_000_000m;
        const decimal MaxBudgetAbsolute2 = 3_000_000_000m;
        if (request.ExpectedBudget < MinBudgetAbsolute2 || request.ExpectedBudget > MaxBudgetAbsolute2)
            return BadRequest(new { message = "Tổng ngân sách dự trù tối thiểu 100 triệu (chi phí tối thiểu ~103,3 triệu cho 150 khách) đến tối đa 3 tỷ VNĐ." });

        DateTime targetDate;
        try
        {
            targetDate = request.DesiredDate.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DesiredDate không hợp lệ.");
            return BadRequest(new { message = "Ngày tổ chức không hợp lệ." });
        }
        // Ràng buộc 30–365 ngày.
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (targetDate < minDate)
            return BadRequest(new { message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất." });
        if (targetDate > maxDate)
            return BadRequest(new { message = "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn." });
        var nextDay = targetDate.AddDays(1);

        try
        {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var customerId = await ResolveAuthenticatedCustomerIdAsync(existingDataStatusId);
        var requestEntity = new RecommendationRequestEntity
        {
            CustomerId = customerId,
            ExpectedBudget = request.ExpectedBudget,
            GuestCount = request.GuestCount,
            DesiredDate = targetDate,
            DesiredShift = desiredShift,
            DesiredStyle = desiredStyle,
            ServiceNeeds = NormalizeOptional(request.ServiceNeeds),
            CreatedAt = DateTime.Now
        };
        _context.RecommendationRequests.Add(requestEntity);
        await _context.SaveChangesAsync();

        var candidateSchedules = await ResolveAvailableCandidatesAsync(
            targetDate, nextDay, desiredShift, request.GuestCount, existingDataStatusId);

        // Ảnh đại diện sảnh (primary ImageAsset) để card hiển thị đúng ảnh từng sảnh
        // thay vì placeholder cho tất cả.
        var hallImageById = await _context.ImageAssets.AsNoTracking()
            .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId && image.HallId.HasValue)
            .GroupBy(image => image.HallId!.Value)
            .Select(g => new { HallId = g.Key, ImagePath = g.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).FirstOrDefault() })
            .ToDictionaryAsync(x => x.HallId, x => x.ImagePath);

        var estimatedTableCountEarly = request.TableCount > 0
            ? request.TableCount
            : (request.OfficialTableCount + request.SpareTableCount) > 0
                ? request.OfficialTableCount + request.SpareTableCount
                : (int)Math.Ceiling(request.GuestCount / 10m);
        if (candidateSchedules.Count == 0)
        {
            var suggestedEmpty = await BuildSuggestedNextAsync(targetDate, desiredShift, estimatedTableCountEarly, request.ExpectedBudget, existingDataStatusId);
            return Ok(new RecommendationResponseDto
            {
                RecommendationRequestId = requestEntity.RecommendationRequestId,
                TotalCandidatesEvaluated = 0,
                AlgorithmNotice = "Không có lịch sảnh AVAILABLE phù hợp ngày, ca và số lượng khách đã chọn.",
                AnalyzedAt = DateTime.Now,
                SuggestedNext = suggestedEmpty
            });
        }

        var activeMenus = await _context.Menus.AsNoTracking()
            .Where(menu =>
                menu.MenuType == MenuTypes.Standard &&
                menu.DataStatusId == existingDataStatusId &&
                menu.Status != null &&
                menu.Status.StatusGroup == StatusGroups.Menu &&
                menu.Status.StatusCode == MenuStatusCodes.Active)
            .ToListAsync();

        var activeDecors = await _context.DecorPackages.AsNoTracking()
            .Where(decor =>
                decor.DataStatusId == existingDataStatusId &&
                decor.Status != null &&
                decor.Status.StatusGroup == StatusGroups.DecorationPackage &&
                decor.Status.StatusCode == BusinessStatusCodes.Active)
            .Select(decor => new
            {
                Decor = decor,
                ImageUrl = decor.Images
                    .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var estimatedTableCount = request.TableCount > 0
            ? request.TableCount
            : (request.OfficialTableCount + request.SpareTableCount) > 0
                ? request.OfficialTableCount + request.SpareTableCount
                : (int)Math.Ceiling(request.GuestCount / 10m);
        // Dịch vụ ưu tiên (ServiceNeeds "DV001,DV002..."): query trực tiếp bảng DichVu
        // (chỉ lấy ACTIVE) để cộng vào dự toán trọn gói, khớp từng đồng với thẻ hiển thị.
        var priorityServices = await ResolvePriorityServicesAsync(request.ServiceNeeds, existingDataStatusId);
        var servicesTotal = priorityServices.Sum(s => s.Price);
        var scoredCombos = new List<ComboSuggestionDto>();

        foreach (var schedule in candidateSchedules)
        foreach (var menu in activeMenus)
        foreach (var decorCandidate in activeDecors)
        {
            var hall = schedule.Hall;
            var decor = decorCandidate.Decor;
            var menuTotal = menu.PricePerTable * estimatedTableCount;
            // Dự toán trọn gói = Giá thuê sảnh + (Đơn giá Menu × Tổng số bàn) + Giá Decor + Tổng giá Dịch vụ ưu tiên.
            var totalCost = hall.RentalPrice + menuTotal + decor.Gia + servicesTotal;
            // Hard filter WSM: est_total <= budget + 10tr (đồng bộ py + C#).
            if (totalCost > request.ExpectedBudget + OverBudgetToleranceVnd)
                continue;
            var budgetDifference = request.ExpectedBudget - totalCost;
            var r1 = CalculateBudgetScore(totalCost, request.ExpectedBudget);
            var optimalTables = GetOptimalTables(hall);
            var r2 = CalculateSpaceScore(estimatedTableCount, optimalTables, hall.CapacityMaxTables);
            var r3 = CalculateStyleScore(desiredStyle, decor.PhongCach);
            var priorityCodes = (request.ServiceNeeds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => c.Length > 0)
                .Distinct()
                .ToList();
            var includedCodes = priorityServices
                .Select(s => (s.ServiceCode ?? string.Empty).Trim().ToUpperInvariant())
                .Where(c => c.Length > 0)
                .ToList();
            var r4 = CalculateServiceScore(priorityCodes, includedCodes);
            var hitCount = priorityCodes.Count == 0
                ? 0
                : priorityCodes.Count(c => includedCodes.Contains(c, StringComparer.OrdinalIgnoreCase));
            var overallScore = Math.Round((BudgetWeight * r1 + SpaceWeight * r2 + StyleWeight * r3 + ServiceWeight * r4) * 100, 1);

            var explanations = BuildExplanations(hall.HallName, hall.CapacityMinTables, hall.CapacityMaxTables, optimalTables,
                decor.TenGoi, decor.PhongCach, desiredStyle, request.ExpectedBudget, totalCost,
                estimatedTableCount, r3, priorityCodes, hitCount);

            scoredCombos.Add(new ComboSuggestionDto
            {
                OverallMatchScore = overallScore,
                BudgetMatchScore = Math.Round(r1 * 100, 1),
                SpaceFitScore = Math.Round(r2 * 100, 1),
                StyleThemeScore = Math.Round(r3 * 100, 1),
                RatingCredibilityScore = Math.Round((r2 + r3) / 2 * 100, 1),
                HallScheduleId = schedule.HallScheduleId,
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                HallImageUrl = hallImageById.TryGetValue(hall.HallId, out var hallImg) ? hallImg : null,
                HallCapacity = hall.MaximumCapacity,
                CapacityMinGuests = hall.MinimumCapacity ?? 150,
                CapacityMaxGuests = hall.MaximumCapacity,
                CapacityMinTables = (hall.MinimumCapacity ?? 150) / 10,
                CapacityMaxTables = hall.MaximumCapacity / 10,
                HallRentalPrice = hall.RentalPrice,
                HallDescription = hall.Description ?? string.Empty,
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                MenuPricePerTable = menu.PricePerTable,
                EstimatedTableCount = estimatedTableCount,
                MenuTotalPrice = menuTotal,
                MenuDescription = menu.Description ?? string.Empty,
                DecorationPackageId = decor.GoiTrangTriID,
                DecorationPackageName = decor.TenGoi,
                DecorationStyle = decor.PhongCach,
                DecorationPrice = decor.Gia,
                DecorationDescription = decor.MoTa ?? string.Empty,
                DecorationImageUrl = decorCandidate.ImageUrl,
                IncludedServices = priorityServices,
                ServicesTotalPrice = servicesTotal,
                TotalEstimatedCost = totalCost,
                BudgetDifference = budgetDifference,
                BudgetAnalysisText = budgetDifference >= 0
                    ? $"Trong ngân sách (Tiết kiệm {FormatVnd(budgetDifference)})"
                    : $"Vượt ngân sách ({FormatVnd(Math.Abs(budgetDifference))})",
                RecommendationReasons = explanations,
                Explanations = explanations
            });
        }

        var topCombos = SelectDiverseTop3(scoredCombos, request.ExpectedBudget);
        for (var index = 0; index < topCombos.Count; index++)
        {
            topCombos[index].Rank = index + 1;
            ApplyComboMeta(topCombos[index], index + 1);
        }

        SuggestedNextDto? suggestedNext = null;
        var notice = $"Đã đánh giá {scoredCombos.Count} phương án bằng scoring C# (W* 0.3004/0.3198/0.1799/0.1999) và trả về tối đa 3 kết quả.";
        if (topCombos.Count == 0)
        {
            suggestedNext = await BuildSuggestedNextAsync(targetDate, desiredShift, estimatedTableCount, request.ExpectedBudget, existingDataStatusId);
            notice = $"Đã đánh giá {scoredCombos.Count} phương án, không có combo trong ngân sách +10tr.";
        }

        return Ok(new RecommendationResponseDto
        {
            RecommendationRequestId = requestEntity.RecommendationRequestId,
            TotalCandidatesEvaluated = scoredCombos.Count,
            TopCombos = topCombos,
            AlgorithmNotice = notice,
            AnalyzedAt = DateTime.Now,
            SuggestedNext = suggestedNext
        });
        }
        catch (Exception ex)
        {
            // Bọc toàn bộ flow DB + scoring: log chi tiết, trả 500 có traceId
            // thay vì để UseExceptionHandler che lỗi generic.
            _logger.LogError(ex, "SuggestTop3Combos thất bại (date={Date}, shift={Shift}, guests={Guests}, traceId={TraceId}). Chi tiết: {Detail}",
                targetDate.ToString("yyyy-MM-dd"), desiredShift, request.GuestCount, HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể phân tích gói tiệc lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("hall-availability")]
    public async Task<IActionResult> GetHallAvailability([FromQuery] DateTime date)
    {
        DateTime targetDate;
        try
        {
            targetDate = date.Date;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetHallAvailability: date không hợp lệ.");
            return BadRequest(new { message = "Ngày truy vấn không hợp lệ." });
        }
        // Ràng buộc Riverside Palace: chỉ nhận đặt tiệc trước từ 30 ngày đến tối đa 365 ngày (1 năm).
        var minDate = DateTime.Today.AddDays(30);
        var maxDate = DateTime.Today.AddDays(365);
        if (targetDate < minDate)
            return BadRequest(new { message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất." });
        if (targetDate > maxDate)
            return BadRequest(new { message = "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn." });
        var nextDay = targetDate.AddDays(1);

        try
        {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatusId == existingDataStatusId &&
                hall.Status != null &&
                hall.Status.StatusGroup == StatusGroups.Hall && hall.Status.StatusCode == HallStatusCodes.Active)
            .OrderBy(hall => hall.HallId)
            .Select(hall => new
            {
                hall.HallId,
                hall.HallName,
                hall.HallCode,
                hall.MinimumCapacity,
                Capacity = hall.MaximumCapacity,
                hall.RentalPrice
            }).ToListAsync();

        // Default-to-Available (BR03): sảnh/ca CHỈ kín khi (a) có LichSanh với
        // TrangThai != AVAILABLE, HOẶC (b) có DatTiec (trừ Đã hủy) chiếm giữ.
        // Ngày xa chưa có record LichSanh -> coi TRỐNG (xanh "Ca còn trống").
        var daySchedules = await _context.HallSchedules.AsNoTracking()
            .Where(schedule => schedule.Date >= targetDate && schedule.Date < nextDay &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status != null &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule)
            .Select(schedule => new { schedule.HallId, schedule.Shift, schedule.Status.StatusCode })
            .ToListAsync();

        var occupiedHallShifts = await _context.WeddingBookings.AsNoTracking()
            .Where(booking =>
                booking.HallSchedule.Date >= targetDate && booking.HallSchedule.Date < nextDay &&
                booking.HallSchedule.DataStatusId == existingDataStatusId &&
                booking.Status != null &&
                booking.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            .Select(booking => new { booking.HallSchedule.HallId, booking.HallSchedule.Shift })
            .ToListAsync();

        bool IsBlocked(int hallId, string shift) =>
            daySchedules.Any(item => item.HallId == hallId &&
                string.Equals(NormalizeHallShift(item.Shift), NormalizeHallShift(shift), StringComparison.OrdinalIgnoreCase) &&
                item.StatusCode != HallScheduleStatusCodes.Available) ||
            occupiedHallShifts.Any(item => item.HallId == hallId &&
                string.Equals(NormalizeHallShift(item.Shift), NormalizeHallShift(shift), StringComparison.OrdinalIgnoreCase));

        return Ok(new HallAvailabilityResponseDto
        {
            QueryDate = targetDate.ToString("yyyy-MM-dd"),
            Halls = halls.Select(hall => new HallAvailabilityItemDto
            {
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                Capacity = hall.Capacity,
                CapacityMinGuests = hall.MinimumCapacity ?? 150,
                CapacityMaxGuests = hall.Capacity,
                CapacityMinTables = (hall.MinimumCapacity ?? 150) / 10,
                CapacityMaxTables = hall.Capacity / 10,
                RentalPrice = hall.RentalPrice,
                IsNoonAvailable = !IsBlocked(hall.HallId, HallShiftNames.Lunch),
                IsEveningAvailable = !IsBlocked(hall.HallId, HallShiftNames.Dinner)
            }).ToList()
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHallAvailability thất bại (date={Date}).", targetDate.ToString("yyyy-MM-dd"));
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tải lịch sảnh lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("available-dates")]
    public async Task<IActionResult> GetAvailableDates(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? shift)
    {
        try
        {
            // Parse ngày null-safe, mặc định quét 60 ngày, hỗ trợ tới 365 ngày.
            DateTime fromDate;
            if (!DateTime.TryParse(from, out var parsedFrom))
                fromDate = DateTime.Today.AddDays(30);
            else
                fromDate = parsedFrom.Date;

            DateTime toDate;
            if (!DateTime.TryParse(to, out var parsedTo))
                toDate = fromDate.AddDays(60);
            else
                toDate = parsedTo.Date;

            if (toDate < fromDate)
                toDate = fromDate.AddDays(60);
            // Giới hạn quét tối đa 365 ngày (1 năm) để vừa hỗ trợ đặt xa vừa tránh query nặng.
            if ((toDate - fromDate).TotalDays > 365)
                toDate = fromDate.AddDays(365);
            // Kẹp trong [Today, Today+365].
            var hardMin = DateTime.Today;
            var hardMax = DateTime.Today.AddDays(365);
            if (fromDate < hardMin) fromDate = hardMin;
            if (toDate > hardMax) toDate = hardMax;
            if (toDate < fromDate)
                return Ok(new List<AvailableDateItemDto>());

            var shiftFilter = (shift ?? string.Empty).Trim();
            var filterByShift = shiftFilter == HallShiftNames.Lunch || shiftFilter == HallShiftNames.Dinner;
            var shiftsToCheck = filterByShift
                ? new[] { shiftFilter }
                : new[] { HallShiftNames.Lunch, HallShiftNames.Dinner };

            var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
            var toExclusive = toDate.AddDays(1);

            var activeHalls = await _context.Halls.AsNoTracking()
                .Where(hall => hall.DataStatusId == existingDataStatusId &&
                    hall.Status != null &&
                    hall.Status.StatusGroup == StatusGroups.Hall &&
                    hall.Status.StatusCode == HallStatusCodes.Active)
                .OrderBy(hall => hall.HallName)
                .Select(hall => new { hall.HallId, hall.HallName })
                .ToListAsync();

            if (activeHalls.Count == 0)
                return Ok(new List<AvailableDateItemDto>());

            // Default-to-Available: load các ô ĐÃ BỊ CHIẾM (LichSanh != AVAILABLE hoặc DatTiec hoạt động),
            // các ô còn lại (kể cả chưa có record) đều coi TRỐNG.
            var blockedSchedules = await _context.HallSchedules.AsNoTracking()
                .Where(schedule =>
                    schedule.Date >= fromDate && schedule.Date < toExclusive &&
                    schedule.DataStatusId == existingDataStatusId &&
                    schedule.Status != null &&
                    schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                    schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
                .Select(schedule => new { schedule.Date, schedule.Shift, schedule.HallId })
                .ToListAsync();

            var occupiedByBookings = await _context.WeddingBookings.AsNoTracking()
                .Where(booking =>
                    booking.HallSchedule.Date >= fromDate && booking.HallSchedule.Date < toExclusive &&
                    booking.HallSchedule.DataStatusId == existingDataStatusId &&
                    booking.Status != null &&
                    booking.Status.StatusGroup == StatusGroups.Booking &&
                    BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                .Select(booking => new
                {
                    booking.HallSchedule.Date,
                    booking.HallSchedule.Shift,
                    booking.HallSchedule.HallId
                })
                .ToListAsync();

            var blockedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in blockedSchedules)
                blockedKeys.Add($"{r.Date:yyyy-MM-dd}|{NormalizeHallShift(r.Shift).ToLowerInvariant()}|{r.HallId}");
            foreach (var r in occupiedByBookings)
                blockedKeys.Add($"{r.Date:yyyy-MM-dd}|{NormalizeHallShift(r.Shift).ToLowerInvariant()}|{r.HallId}");

            var grouped = new List<AvailableDateItemDto>();
            for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
            {
                var dayStr = day.ToString("yyyy-MM-dd");
                foreach (var shiftName in shiftsToCheck)
                {
                    var shiftKey = NormalizeHallShift(shiftName).ToLowerInvariant();
                    var freeHalls = activeHalls
                        .Where(h => !blockedKeys.Contains($"{dayStr}|{shiftKey}|{h.HallId}"))
                        .Select(h => h.HallName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();
                    if (freeHalls.Count == 0) continue;
                    grouped.Add(new AvailableDateItemDto
                    {
                        Date = dayStr,
                        Shift = shiftName,
                        AvailableHallCount = freeHalls.Count,
                        AvailableHalls = freeHalls
                    });
                }
            }

            // Rỗng thì trả [] 200 chứ không 500.
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAvailableDates thất bại (from={From}, to={To}, shift={Shift}, traceId={TraceId}). Chi tiết: {Detail}",
                from, to, shift, HttpContext.TraceIdentifier, ex.ToString());
            return Ok(new List<AvailableDateItemDto>());
        }
    }

    [HttpPost("register-booking")]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterPublicBooking([FromBody] RegisterPublicBookingDto? request)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Thiếu dữ liệu đăng ký giữ chỗ." });
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { success = false, message = "Vui lòng cung cấp họ tên và số điện thoại liên hệ." });
        // Validate email thủ công để luôn trả {success,message} tiếng Việt
        // (DTO không còn DataAnnotation để tránh auto-400 ProblemDetails không message).
        var cleanEmail = (request.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanEmail))
            return BadRequest(new { success = false, message = "Vui lòng nhập email để nhận bảng dự toán và xác nhận giữ chỗ." });
        try
        {
            var _ = new System.Net.Mail.MailAddress(cleanEmail);
            if (!cleanEmail.Contains('.'))
                return BadRequest(new { success = false, message = "Địa chỉ email không đúng định dạng. Vui lòng kiểm tra lại." });
        }
        catch
        {
            return BadRequest(new { success = false, message = "Địa chỉ email không đúng định dạng. Vui lòng kiểm tra lại." });
        }
        // Tự phục hồi GuestCount từ TableCount*10 trước Model validation để hết lỗi 400
        // "Số lượng khách phải lớn hơn 0" khi JS chỉ gửi TableCount.
        if (request.GuestCount <= 0 && request.TableCount > 0)
            request.GuestCount = request.TableCount * 10;
        if (request.GuestCount <= 0)
            return BadRequest(new { success = false, message = "Số lượng khách phải lớn hơn 0." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
        var existingDataStatus = await _context.DataStatuses.SingleAsync(item => item.DataStatusCode == DataStatusCodes.Existing);
        var cleanPhone = request.PhoneNumber.Trim();
        var customer = await _context.Customers.FirstOrDefaultAsync(customer =>
            customer.DataStatusId == existingDataStatus.DataStatusId &&
            (customer.PhoneNumber == cleanPhone || (request.UserId.HasValue && customer.UserId == request.UserId.Value)));
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = await GenerateCustomerCodeAsync(),
                FullName = request.CustomerName.Trim(),
                PhoneNumber = cleanPhone,
                Email = cleanEmail,
                UserId = request.UserId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }
        else
        {
            var dirty = false;
            if (request.UserId.HasValue && !customer.UserId.HasValue)
            {
                customer.UserId = request.UserId.Value;
                dirty = true;
            }
            // Đồng bộ email mới để mail xác nhận luôn tới đúng địa chỉ hiện tại.
            if (!string.IsNullOrWhiteSpace(cleanEmail) &&
                !string.Equals((customer.Email ?? string.Empty).Trim(), cleanEmail, StringComparison.OrdinalIgnoreCase))
            {
                customer.Email = cleanEmail;
                dirty = true;
            }
            if (dirty)
                await _context.SaveChangesAsync();
        }

        var hasActiveBooking = await _context.WeddingBookings.AnyAsync(booking =>
            booking.CustomerId == customer.CustomerId &&
            booking.Status != null &&
            booking.Status.StatusGroup == StatusGroups.Booking &&
            BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode));
        if (hasActiveBooking)
            // Cụm "đã có đơn tiệc" + "giữ chỗ 01 phương án" là contract với Frontend
            // (Index.cshtml submitActualBooking mở duplicateBookingModal khi message chứa các cụm này).
            return BadRequest(new { success = false, message = "Quý khách đã có đơn tiệc đang xử lý. Để bảo đảm lịch sảnh, mỗi khách hàng chỉ giữ chỗ 01 phương án tiệc cưới tại một thời điểm. Vui lòng vào mục \"Tiệc của tôi\" để xem hoặc hủy đơn cũ." });

        // Default-to-Available + auto-init LichSanh (CREATE-IF-NOT-EXISTS):
        // - HallScheduleId > 0: dùng record hiện có (tự phục hồi nếu stale-lock mà không có DatTiec chiếm giữ).
        // - HallScheduleId <= 0 (ngày xa chưa có dòng) hoặc trỏ nhầm sang HallId (JS gửi hallScheduleId || hallId):
        //   resolve bằng (HallId, EventDate yyyy-MM-dd, Shift); chưa có record + không có DatTiec hoạt động
        //   -> tự INSERT LichSanh BOOKED, tuyệt đối không báo "Lịch sảnh không tồn tại".
        // Chuẩn hóa ca tổ chức: "Ca Tối (17:30 – 21:30)", "ca toi", "Ca Tối"... -> "Ca tối"/"Ca trưa" chuẩn.
        var requestedShift = NormalizeHallShift(request.Shift);

        HallSchedule? schedule = null;
        if (request.HallScheduleId > 0)
        {
            schedule = await _context.HallSchedules
                .Include(item => item.Hall)
                .Include(item => item.Status)
                .SingleOrDefaultAsync(item => item.HallScheduleId == request.HallScheduleId &&
                    item.DataStatusId == existingDataStatus.DataStatusId &&
                    item.Hall.DataStatusId == existingDataStatus.DataStatusId);
            // LỖI A: verify khớp (HallId, EventDate.Date, Shift chuẩn hóa). Nếu JS gửi nhầm HallId
            // vào HallScheduleId hoặc lịch đã đổi ca/ngày -> set null để rơi vào nhánh
            // auto-init theo (HallId, EventDate yyyy-MM-dd, Shift), tránh đặt nhầm lịch.
            // So sánh Shift sau chuẩn hóa + OrdinalIgnoreCase để tránh lệch hoa/thường ("Ca Tối" vs "Ca tối").
            if (schedule != null && request.HallId > 0 && request.EventDate.HasValue
                && !string.IsNullOrWhiteSpace(requestedShift))
            {
                var reqDay = request.EventDate.Value.Date;
                var sameHall = schedule.HallId == request.HallId;
                var sameDate = schedule.Date.Date == reqDay;
                var sameShift = string.Equals(NormalizeHallShift(schedule.Shift), requestedShift, StringComparison.OrdinalIgnoreCase);
                if (!sameHall || !sameDate || !sameShift)
                    schedule = null;
            }
        }

        if (schedule == null && request.HallId > 0 && request.EventDate.HasValue)
        {
            // Chuẩn hóa ngày (chỉ lấy phần Date, bỏ Time).
            var eventDay = request.EventDate.Value.Date;
            var minDate = DateTime.Today.AddDays(30);
            var maxDate = DateTime.Today.AddDays(365);
            if (eventDay < minDate)
                return BadRequest(new { success = false, message = "Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất." });
            if (eventDay > maxDate)
                return BadRequest(new { success = false, message = "Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn." });
            var nextDay = eventDay.AddDays(1);

            var hall = await _context.Halls
                .Include(h => h.Status)
                .SingleOrDefaultAsync(h => h.HallId == request.HallId &&
                    h.DataStatusId == existingDataStatus.DataStatusId);
            if (hall == null || hall.Status == null ||
                hall.Status.StatusGroup != StatusGroups.Hall || hall.Status.StatusCode != HallStatusCodes.Active)
                return BadRequest(new { success = false, message = "Sảnh tiệc không tồn tại hoặc đã ngừng hoạt động." });
            if (request.GuestCount > hall.MaximumCapacity ||
                (hall.MinimumCapacity.HasValue && request.GuestCount < hall.MinimumCapacity.Value))
                return BadRequest(new { success = false, message = "Số khách không phù hợp sức chứa sảnh." });

            // Tìm LichSanh theo (HallId, Date, Shift chuẩn hóa). So sánh Shift
            // case-insensitive (ToLower dịch được sang SQL) để khớp cả record cũ
            // lưu "Ca Tối"/"Ca Trưa" lệch hoa/thường.
            var normalizedShiftLower = requestedShift.ToLower();
            schedule = await _context.HallSchedules
                .Include(item => item.Hall)
                .Include(item => item.Status)
                .SingleOrDefaultAsync(item => item.HallId == request.HallId &&
                    item.Date >= eventDay && item.Date < nextDay &&
                    item.Shift.ToLower() == normalizedShiftLower &&
                    item.DataStatusId == existingDataStatus.DataStatusId);

            if (schedule == null)
            {
                // Chưa có dòng LichSanh: kiểm tra DatTiec đang hoạt động chiếm dụng
                // (PENDING/CONFIRMED/PREPARING, bỏ qua CANCELLED/COMPLETED) rồi tự INSERT.
                // Chỉ báo bận khi thật sự có booking; còn không thì CREATE-IF-NOT-EXISTS.
                var occupiedByBooking = await _context.WeddingBookings.AnyAsync(booking =>
                    booking.HallSchedule.HallId == request.HallId &&
                    booking.HallSchedule.Date >= eventDay && booking.HallSchedule.Date < nextDay &&
                    booking.HallSchedule.Shift.ToLower() == normalizedShiftLower &&
                    booking.HallSchedule.DataStatusId == existingDataStatus.DataStatusId &&
                    booking.Status != null &&
                    booking.Status.StatusGroup == StatusGroups.Booking &&
                    BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode));
                if (occupiedByBooking)
                    return Conflict(new { success = false, message = $"Sảnh này vào ngày {eventDay:dd/MM/yyyy} ({requestedShift}) đã có khách đặt. Vui lòng chọn sảnh hoặc ngày/ca khác." });
                schedule = new HallSchedule
                {
                    HallId = request.HallId,
                    Date = eventDay,
                    Shift = requestedShift,
                    StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked),
                    DataStatusId = existingDataStatus.DataStatusId,
                    Hall = hall
                };
                _context.HallSchedules.Add(schedule);
                await _context.SaveChangesAsync();
                // FIX TRIỆT ĐỂ: sau INSERT, EF chỉ gán StatusId, navigation Status vẫn null
                // (dù đã gán Hall). Guard bên dưới check schedule.Status == null nên báo sai
                // "Thiếu thông tin sảnh/ngày/ca...". Reload navigation ngay tại đây.
                try
                {
                    await _context.Entry(schedule).Reference(s => s.Status).LoadAsync();
                }
                catch (Exception loadEx)
                {
                    _logger.LogWarning(loadEx, "Không reload được Status sau auto-init HallSchedule (HallId={HallId}, Date={Date}, Shift={Shift}).",
                        schedule.HallId, schedule.Date.ToString("yyyy-MM-dd"), schedule.Shift);
                }
                if (schedule.Hall == null)
                {
                    try { await _context.Entry(schedule).Reference(s => s.Hall).LoadAsync(); } catch { }
                }
            }
        }

        // Log chi tiết trước guard để dễ debug: HallId, HallScheduleId, EventDate raw, Shift.
        _logger.LogInformation(
            "RegisterPublicBooking resolve schedule: HallId={HallId}, HallScheduleId={HallScheduleId}, EventDateRaw={EventDateRaw}, EventDate={EventDate}, RequestedShift={RequestedShift}, ScheduleId={ScheduleId}, StatusId={StatusId}, HasHall={HasHall}, HasStatus={HasStatus}",
            request.HallId, request.HallScheduleId, request.EventDate?.ToString("o") ?? "<null>",
            request.EventDate?.ToString("yyyy-MM-dd") ?? "<null>", requestedShift,
            schedule?.HallScheduleId, schedule?.StatusId, schedule?.Hall != null, schedule?.Status != null);

        // Tự phục hồi navigation nếu bị null nhưng FK vẫn hợp lệ (tránh báo thiếu oan):
        // schedule.Status null + StatusId != 0 -> reload; chỉ báo thiếu khi StatusId == 0.
        if (schedule != null && schedule.Status == null && schedule.StatusId != 0)
        {
            try { await _context.Entry(schedule).Reference(s => s.Status).LoadAsync(); } catch { }
        }
        if (schedule != null && schedule.Hall == null && schedule.HallId != 0)
        {
            try { await _context.Entry(schedule).Reference(s => s.Hall).LoadAsync(); } catch { }
        }

        // Sau nhánh auto-init ở trên, schedule chỉ còn null khi thiếu HallId/EventDate
        // (request không hợp lệ) — không còn báo "Lịch sảnh không tồn tại" cho ô xanh
        // (ngày xa chưa có record đã được tự INSERT BOOKED ở trên).
        // Guard cứng: thiếu hẳn schedule/Hall/StatusId mới báo thiếu; Status navigation null
        // nhưng StatusId hợp lệ thì KHÔNG báo thiếu (đã reload ở trên).
        if (schedule == null || schedule.Hall == null || (schedule.Status == null && schedule.StatusId == 0))
            return BadRequest(new { success = false, message = "Thiếu thông tin sảnh/ngày/ca tổ chức. Vui lòng chọn lại ngày/ca khác." });
        // Phòng thủ: nếu Status navigation vẫn null (hiếm) thì query lại Include đầy đủ rồi mới check tiếp.
        if (schedule.Status == null)
        {
            schedule = await _context.HallSchedules
                .Include(item => item.Hall)
                .Include(item => item.Status)
                .SingleOrDefaultAsync(item => item.HallScheduleId == schedule.HallScheduleId);
            if (schedule == null || schedule.Hall == null || schedule.Status == null)
                return BadRequest(new { success = false, message = "Thiếu thông tin sảnh/ngày/ca tổ chức. Vui lòng chọn lại ngày/ca khác." });
        }
        if (schedule.Status.StatusGroup != StatusGroups.HallSchedule ||
            schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
        {
            // Stale-lock tự phục hồi: LichSanh != AVAILABLE nhưng không còn DatTiec hoạt động nào
            // chiếm giữ (các ca hủy trước đây chưa giải phóng) -> cho phép đặt tiếp thay vì chặn.
            var occupied = await _context.WeddingBookings.AnyAsync(booking =>
                booking.HallScheduleId == schedule.HallScheduleId &&
                booking.Status != null &&
                booking.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode));
            if (occupied)
                return Conflict(new { success = false, message = "Lịch sảnh không còn AVAILABLE." });
        }
        if (request.GuestCount > schedule.Hall.MaximumCapacity ||
            (schedule.Hall.MinimumCapacity.HasValue && request.GuestCount < schedule.Hall.MinimumCapacity.Value))
            return BadRequest(new { success = false, message = "Số khách không phù hợp sức chứa sảnh." });

        Menu? menu = null;
        if (request.MenuId.HasValue)
        {
            menu = await _context.Menus.SingleOrDefaultAsync(item =>
                item.MenuId == request.MenuId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (menu == null) return BadRequest(new { success = false, message = "Thực đơn không tồn tại." });
        }

        DecorPackage? decor = null;
        if (request.DecorationPackageId.HasValue)
        {
            decor = await _context.DecorPackages.SingleOrDefaultAsync(item =>
                item.GoiTrangTriID == request.DecorationPackageId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (decor == null) return BadRequest(new { success = false, message = "Gói trang trí không tồn tại." });
        }

        // LỖI B: giữ bàn dự phòng. DatTiec chỉ lưu SoLuongKhach nên tổng bàn
        // = Official (ceil Guest/10) + Spare; ưu tiên TableCount FE gửi lên nếu lớn hơn.
        var officialTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var spareTableCount = Math.Max(0, request.SpareTableCount);
        var estimatedTableCount = officialTableCount + spareTableCount;
        if (request.TableCount > estimatedTableCount)
            estimatedTableCount = request.TableCount;
        // Nhúng marker bàn dự phòng + ngân sách/phong cách vào SpecialRequests để
        // lookup (PublicBookingController.BuildDetailDto) khôi phục đúng 100%.
        var enrichedRequests = (request.SpecialRequests ?? string.Empty).Trim();
        if (spareTableCount > 0 && !enrichedRequests.Contains("dự phòng", StringComparison.OrdinalIgnoreCase))
            enrichedRequests = string.IsNullOrEmpty(enrichedRequests)
                ? $"[Bàn dự phòng: {spareTableCount}]"
                : $"{enrichedRequests}; [Bàn dự phòng: {spareTableCount}]";
        if (request.ExpectedBudget.HasValue && request.ExpectedBudget.Value > 0
            && !enrichedRequests.Contains("Ngân sách", StringComparison.OrdinalIgnoreCase))
            enrichedRequests = string.IsNullOrEmpty(enrichedRequests)
                ? $"Ngân sách dự kiến {request.ExpectedBudget.Value:N0} đ"
                : $"{enrichedRequests}; Ngân sách dự kiến {request.ExpectedBudget.Value:N0} đ";
        if (!string.IsNullOrWhiteSpace(request.DesiredStyle)
            && !enrichedRequests.Contains(request.DesiredStyle.Trim(), StringComparison.OrdinalIgnoreCase))
            enrichedRequests = string.IsNullOrEmpty(enrichedRequests)
                ? $"Phong cách mong muốn: {request.DesiredStyle.Trim()}"
                : $"{enrichedRequests}; Phong cách mong muốn: {request.DesiredStyle.Trim()}";
        // Thực đơn tùy chỉnh: khách tự thêm/bớt món trong Top 3 (FE gửi customMenuName +
        // customMenuPricePerTable + customMenuItems). Lưu giá custom vào FinalMenuPrice để
        // EstimatedTotal khớp với tổng tiền đã cập nhật ngoài card; nhúng chi tiết món
        // vào SpecialRequests để tra cứu "Tiệc của tôi" vẫn thấy được.
        var customMenuName = (request.CustomMenuName ?? string.Empty).Trim();
        var hasCustomMenu = !string.IsNullOrWhiteSpace(customMenuName)
            || (request.CustomMenuPricePerTable.HasValue && request.CustomMenuPricePerTable.Value > 0);
        var effectiveMenuPricePerTable = menu?.PricePerTable ?? 0m;
        if (hasCustomMenu && request.CustomMenuPricePerTable.HasValue && request.CustomMenuPricePerTable.Value > 0)
            effectiveMenuPricePerTable = request.CustomMenuPricePerTable.Value;
        if (hasCustomMenu)
        {
            var itemsText = request.CustomMenuItems is { Count: > 0 }
                ? string.Join("; ", request.CustomMenuItems.Select(x => (x ?? string.Empty).Trim()).Where(x => x.Length > 0))
                : string.Empty;
            var customLabel = string.IsNullOrWhiteSpace(customMenuName) ? "Thực đơn tùy chỉnh" : customMenuName;
            var customMarker = $"[{customLabel}: {effectiveMenuPricePerTable:N0} đ/bàn{(string.IsNullOrEmpty(itemsText) ? string.Empty : " | " + itemsText)}]";
            enrichedRequests = string.IsNullOrEmpty(enrichedRequests)
                ? customMarker
                : $"{enrichedRequests}; {customMarker}";
        }
        // Dịch vụ đi kèm: resolve mã DV... sang DichVu ACTIVE, lưu BookingServices
        // và CỘNG vào EstimatedTotal để khớp từng đồng với tổng tiền ngoài card Top 3
        // (trước đây bỏ sót khiến tra cứu/mail hiển thị thấp hơn 0–6tr).
        var bookingServices = new List<RecommendedServiceDto>();
        var servicesTotalBooking = 0m;
        var serviceCodes = (request.ServiceCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        List<ServiceItem> serviceEntities = new();
        if (serviceCodes.Count > 0)
        {
            serviceEntities = await _context.ServiceItems.AsNoTracking()
                .Where(svc => svc.DataStatusId == existingDataStatus.DataStatusId &&
                    svc.Status != null &&
                    svc.Status.StatusGroup == StatusGroups.Service &&
                    svc.Status.StatusCode == BusinessStatusCodes.Active &&
                    serviceCodes.Contains(svc.MaDichVu.ToUpper()))
                .ToListAsync();
            bookingServices = serviceEntities
                .OrderBy(svc => serviceCodes.IndexOf((svc.MaDichVu ?? string.Empty).ToUpperInvariant()))
                .Select(svc => new RecommendedServiceDto
                {
                    ServiceCode = svc.MaDichVu,
                    ServiceName = svc.TenDichVu,
                    Price = svc.Gia,
                    Note = svc.MoTa ?? string.Empty
                }).ToList();
            servicesTotalBooking = bookingServices.Sum(s => s.Price);
            var svcText = string.Join("; ", bookingServices.Select(s => $"{s.ServiceName} ({s.Price:N0} đ)"));
            if (!string.IsNullOrWhiteSpace(svcText))
            {
                var svcMarker = $"[Dịch vụ đi kèm: {svcText}]";
                enrichedRequests = string.IsNullOrEmpty(enrichedRequests) ? svcMarker : $"{enrichedRequests}; {svcMarker}";
            }
        }
        var booking = new WeddingBooking
        {
            BookingCode = await GenerateBookingCodeAsync(schedule.Date),
            CustomerId = customer.CustomerId,
            HallScheduleId = schedule.HallScheduleId,
            MenuId = menu?.MenuId,
            DecorationPackageId = decor?.GoiTrangTriID,
            GuestCount = request.GuestCount,
            FinalHallPrice = schedule.Hall.RentalPrice,
            FinalMenuPrice = menu == null ? (decimal?)null : effectiveMenuPricePerTable,
            FinalDecorationPrice = decor?.Gia,
            EstimatedTotal = schedule.Hall.RentalPrice + (menu == null ? 0m : effectiveMenuPricePerTable) * estimatedTableCount + (decor?.Gia ?? 0m) + servicesTotalBooking,
            SpecialRequests = NormalizeOptional(enrichedRequests),
            StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Pending),
            BookedAt = DateTime.Now
        };
        schedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();
        // Lưu chi tiết dịch vụ đi kèm để tra cứu "Tiệc của tôi"/Admin thấy đúng dịch vụ đã chọn.
        if (serviceEntities.Count > 0)
        {
            foreach (var svc in serviceEntities)
            {
                _context.WeddingBookingServices.Add(new WeddingBookingService
                {
                    BookingId = booking.BookingId,
                    ServiceId = svc.DichVuID,
                    Quantity = 1,
                    FinalUnitPrice = svc.Gia,
                    DataStatusId = existingDataStatus.DataStatusId
                });
            }
            await _context.SaveChangesAsync();
        }
        await transaction.CommitAsync();

        // FR15 / UC13 (6a) fault-tolerant: gửi email xác nhận SAU commit, lỗi SMTP
        // chỉ LogError, KHÔNG rollback, vẫn trả success + mã cho client.
        var recipientEmail = (request.Email ?? customer.Email)?.Trim();
        var snapshotCustomerName = customer.FullName;
        var snapshotBookingCode = booking.BookingCode;
        var snapshotEventDate = schedule.Date;
        var snapshotShift = schedule.Shift;
        var snapshotHallName = schedule.Hall.HallName;
        var snapshotMenuName = menu?.MenuName;
        var snapshotDecorName = decor?.TenGoi;
        var snapshotTotal = booking.EstimatedTotal ?? 0m;
        var snapshotHoldExpiresAt = booking.BookedAt.AddHours(48);
        bool emailSent = false;
        string? emailError = null;
        if (!string.IsNullOrWhiteSpace(recipientEmail))
        {
            try
            {
                if (_emailService is null)
                {
                    emailError = "IEmailService chưa được đăng ký DI.";
                    _logger.LogWarning("Bỏ qua gửi email giữ chỗ {Code}: IEmailService chưa được đăng ký DI.",
                        snapshotBookingCode);
                }
                else
                {
                    await _emailService.SendBookingConfirmationAsync(
                        recipientEmail,
                        snapshotCustomerName,
                        snapshotBookingCode,
                        snapshotEventDate,
                        snapshotShift ?? requestedShift,
                        snapshotHallName,
                        estimatedTableCount,
                        request.GuestCount,
                        snapshotMenuName,
                        snapshotDecorName,
                        snapshotTotal,
                        snapshotHoldExpiresAt,
                        cleanPhone);
                    emailSent = true;
                    _logger.LogInformation("Đã gửi email xác nhận thành công đến {Email} cho mã đơn {BookingCode}",
                        recipientEmail, snapshotBookingCode);
                }
            }
            catch (Exception ex)
            {
                emailError = ex.Message;
                _logger.LogError(ex, "Gửi email xác nhận thất bại cho mã đơn {BookingCode} đến {Email}",
                    snapshotBookingCode, recipientEmail);
            }
        }
        else
        {
            _logger.LogWarning("Khách hàng không cung cấp email cho mã đơn {BookingCode}, bỏ qua gửi email", snapshotBookingCode);
        }

        return Ok(new { success = true, bookingCode = booking.BookingCode, bookingId = booking.BookingId, emailSent, emailError });
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { }
            _logger.LogError(ex, "RegisterPublicBooking thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể lưu đơn đăng ký lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private async Task<int?> ResolveAuthenticatedCustomerIdAsync(byte existingDataStatusId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawUserId, out var userId)) return null;
        return await _context.Customers.AsNoTracking()
            .Where(customer => customer.UserId == userId && customer.DataStatusId == existingDataStatusId)
            .Select(customer => (int?)customer.CustomerId)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Resolve dịch vụ ưu tiên từ ServiceNeeds ("DV001,DV002...") sang danh mục
    /// DichVu ACTIVE trong CSDL (MaDichVu/TenDichVu/Gia). Dùng cho dự toán trọn gói.
    /// </summary>
    private async Task<List<RecommendedServiceDto>> ResolvePriorityServicesAsync(
        string? serviceNeeds, byte existingDataStatusId)
    {
        var codes = (serviceNeeds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length == 0)
            return new List<RecommendedServiceDto>();
        var normalized = codes
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
        var items = await _context.ServiceItems.AsNoTracking()
            .Where(svc =>
                svc.DataStatusId == existingDataStatusId &&
                svc.Status != null &&
                svc.Status.StatusGroup == StatusGroups.Service &&
                svc.Status.StatusCode == BusinessStatusCodes.Active &&
                normalized.Contains(svc.MaDichVu.ToUpper()))
            .Select(svc => new RecommendedServiceDto
            {
                ServiceCode = svc.MaDichVu,
                ServiceName = svc.TenDichVu,
                Price = svc.Gia,
                Note = svc.MoTa ?? string.Empty
            })
            .ToListAsync();
        // Giữ đúng thứ tự FE đã chọn.
        var order = normalized
            .Select((code, index) => new { code, index })
            .ToDictionary(x => x.code, x => x.index);
        return items
            .OrderBy(s => order.TryGetValue((s.ServiceCode ?? string.Empty).ToUpperInvariant(), out var i) ? i : int.MaxValue)
            .ToList();
    }

    /// <summary>Nhóm phong cách tương đồng — khớp style_groups trong AI_Service/data/raw_catalog.json.</summary>
    private static readonly Dictionary<string, string[]> StyleGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["romantic"] = ["Lãng mạn", "Hiện đại / lãng mạn", "Sân vườn / lãng mạn", "Cổ tích / lãng mạn"],
        ["gentle"] = ["Nhẹ nhàng"],
        ["nature"] = ["Thiên nhiên / ngoài trời"],
    };

    /// <summary>r1 ngân sách 0..1 — port y hệt wsm_engine.py score_candidate.</summary>
    private static double CalculateBudgetScore(decimal totalCost, decimal expectedBudget)
    {
        var budget = (double)(expectedBudget > 0 ? expectedBudget : 1m);
        var total = (double)totalCost;
        var over = total - budget;
        if (over <= 0)
            return Math.Max(0.0, 1.0 - Math.Abs(total - budget) / Math.Max(1.0, budget));
        if (over <= 5_000_000) return 0.70 - (over / 5_000_000) * 0.05;
        if (over <= 10_000_000) return 0.65 - ((over - 5_000_000) / 5_000_000) * 0.10;
        var extraTens = (over - 10_000_000) / 10_000_000;
        return Math.Max(0.0, 0.45 - extraTens * 0.08);
    }

    /// <summary>r2 sức chứa 0..1: 1 - |TableCount-Optimal|/MaxTables, clamp 0..1.</summary>
    private static double CalculateSpaceScore(int tableCount, int optimalTables, int maxTables)
    {
        var denom = Math.Max(1.0, (double)Math.Max(1, maxTables));
        return Math.Max(0.0, 1.0 - Math.Abs((double)tableCount - optimalTables) / denom);
    }

    /// <summary>r3 phong cách: 1.0 trùng hoàn toàn / 0.5 cùng nhóm tương đồng / 0.0 khác.</summary>
    private static double CalculateStyleScore(string? desiredStyle, string? decorStyle)
    {
        var desired = (desiredStyle ?? string.Empty).Trim();
        var decor = (decorStyle ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(desired) || string.IsNullOrEmpty(decor))
            return 0.0;
        var dn = desired.ToLowerInvariant();
        var cn = decor.ToLowerInvariant();
        if (dn == cn || dn.Contains(cn) || cn.Contains(dn))
            return 1.0;
        foreach (var members in StyleGroups.Values)
        {
            var lowered = members.Select(m => m.ToLowerInvariant()).ToList();
            if (lowered.Contains(dn) && lowered.Contains(cn))
                return 0.5;
            foreach (var member in lowered)
            {
                if ((dn.Contains(member) || member.Contains(dn))
                    && (cn.Contains(member) || member.Contains(cn)))
                    return 0.5;
            }
        }
        return 0.0;
    }

    /// <summary>r4 dịch vụ: số DV combo có / tổng DV khách chọn; khách không chọn DV nào => 1.0.</summary>
    private static double CalculateServiceScore(IReadOnlyCollection<string> priority, IReadOnlyCollection<string> included)
    {
        if (priority.Count == 0) return 1.0;
        var set = new HashSet<string>(included, StringComparer.OrdinalIgnoreCase);
        var hit = priority.Count(set.Contains);
        return (double)hit / priority.Count;
    }

    private static string FormatVnd(decimal value) =>
        $"{value:N0}".Replace(",", ".") + " đ";

    /// <summary>XAI định lượng ≥2 câu — mirror Python build_explanation().</summary>
    private static List<string> BuildExplanations(string hallName, int minTables, int maxTables, int optimalTables,
        string decorName, string decorStyle, string desiredStyle, decimal budget, decimal total,
        int tableCount, double r3, IReadOnlyList<string> priority, int hitServices)
    {
        var list = new List<string>(4);
        var diff = budget - total;
        var pct = budget > 0 ? (double)Math.Abs(diff) / (double)budget * 100 : 0.0;
        var tr = (double)Math.Abs(diff) / 1_000_000;
        list.Add(diff >= 0
            ? $"Chi phí: Tiết kiệm {FormatVnd(diff)} ({tr:F1} triệu, {pct:F1}%) so với ngân sách."
            : $"Chi phí: Vượt {FormatVnd(Math.Abs(diff))} ({tr:F1} triệu, {pct:F1}%) so với ngân sách, cân nhắc điều chỉnh.");
        var span = Math.Max(1, maxTables - minTables);
        var ratio = Math.Clamp((double)(tableCount - minTables) / span, 0.0, 1.0);
        list.Add($"Sức chứa: {tableCount} bàn đạt lấp đầy {ratio * 100:F1}% sảnh {hallName} ({minTables}-{maxTables} bàn, tối ưu {optimalTables} bàn).");
        list.Add(r3 >= 1.0
            ? $"Phong cách: '{decorStyle}' khớp 100% với mong muốn '{desiredStyle}'."
            : r3 >= 0.5
                ? $"Phong cách: '{decorName}' ({decorStyle}) cùng nhóm tương đồng với '{desiredStyle}' (khớp 50%)."
                : $"Phong cách: '{decorName}' ({decorStyle}) khác với '{desiredStyle}' (khớp 0%), bù lại bằng chi phí và sức chứa.");
        list.Add(priority.Count == 0
            ? "Dịch vụ: khách không chọn DV ưu tiên, gói đáp ứng linh hoạt 100% dịch vụ đi kèm."
            : $"Dịch vụ: đáp ứng {hitServices}/{priority.Count} DV ưu tiên ({(priority.Count > 0 ? (double)hitServices / priority.Count * 100 : 0):F0}%).");
        return list;
    }

    private static int GetOptimalTables(Hall hall)
    {
        var minT = hall.CapacityMinTables;
        var maxT = Math.Max(1, hall.CapacityMaxTables);
        return (minT + maxT) / 2;
    }

    private async Task<byte> GetDataStatusIdAsync(string code) => await _context.DataStatuses
        .Where(item => item.DataStatusCode == code).Select(item => item.DataStatusId).SingleAsync();

    private async Task<string> GenerateBookingCodeAsync(DateTime eventDate)
    {
        string code;
        do code = $"DT-{eventDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.WeddingBookings.AnyAsync(item => item.BookingCode == code));
        return code;
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        string code;
        do code = $"KH-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.Customers.AnyAsync(item => item.CustomerCode == code));
        return code;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Chuẩn hóa tên ca tổ chức về "Ca trưa" / "Ca tối" chuẩn (khớp <see cref="HallShiftNames"/>).
    /// Chịu được biến thể FE gửi lên: "Ca Tối (17:30 – 21:30)", "Buổi tối...", "ca toi"/"Ca Trua"
    /// (không dấu, sai hoa/thường). Rỗng/null -> mặc định "Ca tối".
    /// </summary>
    private static string NormalizeHallShift(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return HallShiftNames.Dinner;
        var s = raw.Trim();
        if (s.Contains("trưa", StringComparison.OrdinalIgnoreCase) || s.Contains("trua", StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Lunch;
        if (s.Contains("tối", StringComparison.OrdinalIgnoreCase) || s.Contains("toi", StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Dinner;
        if (string.Equals(s, HallShiftNames.Lunch, StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Lunch;
        if (string.Equals(s, HallShiftNames.Dinner, StringComparison.OrdinalIgnoreCase))
            return HallShiftNames.Dinner;
        return s;
    }

    // DTO mapping snake_case của Python FastAPI (AI_Service/app/schemas/recommendation.py).
    // Bắt buộc có [JsonPropertyName] + AiJsonOptions case-insensitive để không deserialize ra null.
    private sealed class AiRankItem
    {
        [JsonPropertyName("hall_id")] public int HallId { get; set; }
        [JsonPropertyName("hall_name")] public string HallName { get; set; } = string.Empty;
        [JsonPropertyName("hall_price")] public double HallPrice { get; set; }
        [JsonPropertyName("menu_id")] public int MenuId { get; set; }
        [JsonPropertyName("menu_name")] public string MenuName { get; set; } = string.Empty;
        [JsonPropertyName("price_per_table")] public double PricePerTable { get; set; }
        [JsonPropertyName("total_menu_price")] public double TotalMenuPrice { get; set; }
        [JsonPropertyName("decor_id")] public int DecorId { get; set; }
        [JsonPropertyName("decor_name")] public string DecorName { get; set; } = string.Empty;
        [JsonPropertyName("decor_price")] public double DecorPrice { get; set; }
        [JsonPropertyName("decor_style")] public string DecorStyle { get; set; } = string.Empty;
        [JsonPropertyName("included_services")] public List<string> IncludedServices { get; set; } = new();
        [JsonPropertyName("total_estimated_cost")] public double TotalEstimatedCost { get; set; }
        [JsonPropertyName("matching_score")] public double MatchingScore { get; set; }
        [JsonPropertyName("explanation_arguments")] public List<string> ExplanationArguments { get; set; } = new();
    }

    private sealed class AiRankResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; } = true;
        [JsonPropertyName("total_candidates_evaluated")] public int TotalCandidatesEvaluated { get; set; }
        [JsonPropertyName("top_3")] public List<AiRankItem>? Top3 { get; set; }
        [JsonPropertyName("top_3_recommendations")] public List<AiRankItem>? Top3Recommendations { get; set; }
        [JsonPropertyName("algorithm_notice")] public string? AlgorithmNotice { get; set; }
    }
}

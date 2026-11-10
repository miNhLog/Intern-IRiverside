using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Coordination;
using HeThongDatTiecCuoi_WEB.Models.Home;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public HomeController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(
        bool publicSite = false,
        CancellationToken cancellationToken = default)
    {
        if (!publicSite && User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (!publicSite && User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _apiClient.GetFeaturedHallsAsync(cancellationToken);
        return View(new HomeViewModel
        {
            FeaturedHalls = result.Succeeded && result.Value is not null
                ? result.Value
                : []
        });
    }


    [Authorize]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        // Admin truy cập trực tiếp /Home/Dashboard
        // được đưa về trang tổng quan hệ thống.
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (User.IsInRole(RoleNames.HallManager))
        {
            if (!TryGetAccessToken(out var hallManagerToken)) return RedirectToAction("Login", "Auth");
            var hallManagerResult = await _apiClient.GetHallManagerDashboardAsync(hallManagerToken, cancellationToken);
            if (hallManagerResult.Succeeded && hallManagerResult.Value is not null)
                return View("HallManagerDashboard", hallManagerResult.Value);

            return View("HallManagerDashboard", new HallManagerDashboardViewModel
            {
                Schedule = new HallManagerScheduleViewModel { Date = VietnamTime.Now.Date },
                ErrorMessage = hallManagerResult.Error ?? "Không thể tải dữ liệu tổng quan quản lý sảnh."
            });
        }

        if (User.IsInRole(RoleNames.Coordinator))
        {
            if (!TryGetAccessToken(out var coordinatorToken)) return RedirectToAction("Login", "Auth");
            var partiesTask = _apiClient.GetCoordinatorAssignedPartiesAsync(coordinatorToken, cancellationToken);
            var incidentsTask = _apiClient.GetIncidentsAsync(coordinatorToken, null, null, null,
                null, null, null, 1, 1, cancellationToken);
            var checklistTask = _apiClient.GetChecklistSummaryAsync(coordinatorToken, cancellationToken);
            await Task.WhenAll(partiesTask, incidentsTask, checklistTask);
            var parties = await partiesTask;
            var incidents = await incidentsTask;
            var checklist = await checklistTask;
            var now = VietnamTime.Now;
            var model = new CoordinatorDashboardViewModel { GeneratedAt = now };

            if (parties.Succeeded && parties.Value is not null)
            {
                model.TodayParties = parties.Value.Where(p => p.EventDate.Date == now.Date)
                    .OrderBy(p => p.Shift).ThenBy(p => p.HallName).ToList();
                model.TodayCount = model.TodayParties.Count;
                model.UpcomingCount = parties.Value.Count(p => p.EventDate.Date > now.Date);
            }
            else model.PartiesError = "Không thể tải tiệc được phân công. Vui lòng thử lại.";

            if (incidents.Succeeded && incidents.Value is not null)
                model.OpenIncidentCount = incidents.Value.Kpi.Pending + incidents.Value.Kpi.Processing;
            else model.IncidentsError = "Không thể tải số liệu sự cố. Vui lòng thử lại.";

            if (checklist.Succeeded && checklist.Value is not null)
                model.IncompleteWorkCount = checklist.Value.IncompleteWorkCount;
            else model.ChecklistError = "Không thể tải công việc checklist. Vui lòng thử lại.";

            return View("CoordinatorDashboard", model);
        }

        if (!User.IsInRole(RoleNames.Manager))
        {
            return View(new ManagerDashboardViewModel());
        }

        if (!Request.Cookies.TryGetValue(ApiTokenCookie, out var accessToken) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var result = await _apiClient.GetManagerDashboardAsync(accessToken, cancellationToken);
        if (result.Succeeded && result.Value is not null)
        {
            return View(result.Value);
        }

        return View(new ManagerDashboardViewModel
        {
            ErrorMessage = result.Error ?? "Không thể tải dữ liệu tổng quan từ hệ thống."
        });
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> HallManagerDashboardSchedule(DateTime date,
        CancellationToken cancellationToken)
    {
        if (date == default) return BadRequest(new { message = "Ngày xem lịch không hợp lệ." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetHallManagerDashboardScheduleAsync(token, date, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Json(result.Value)
            : StatusCode(result.StatusCode ?? 502, new { message = result.Error ?? "Không thể tải lịch sảnh." });
    }

    [Authorize(Roles = RoleNames.Manager)]
    public IActionResult HallManagerAssignments()
    {
        return RedirectToAction("Index", "ManagerHr");
    }

    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationAssignments(string? search, int? hallId, DateTime? eventDate,
        string? coordinationStatus, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!TryGetAccessToken(out var token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetHallManagerCoordinationAsync(token, search, hallId, eventDate,
            coordinationStatus, page, 10, cancellationToken);
        return View(new HallManagerCoordinationPageViewModel
        {
            Data = result.Value ?? new(), Search = search, HallId = hallId, EventDate = eventDate,
            CoordinationStatus = coordinationStatus,
            ErrorMessage = result.Succeeded ? null : result.Error ?? "Không thể tải danh sách phân công."
        });
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationOptions(int bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetCoordinatorAvailabilityAsync(token, bookingId, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationUnassignedOptions(CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetHallManagerCoordinationAsync(token, null, null, null,
            "UNASSIGNED", 1, 50, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? 502, new { message = result.Error });

        var items = result.Value.Items.ToList();
        for (var currentPage = 2; currentPage <= result.Value.TotalPages; currentPage++)
        {
            var nextPage = await _apiClient.GetHallManagerCoordinationAsync(token, null, null, null,
                "UNASSIGNED", currentPage, 50, cancellationToken);
            if (!nextPage.Succeeded || nextPage.Value is null)
                return StatusCode(nextPage.StatusCode ?? 502, new { message = nextPage.Error });
            items.AddRange(nextPage.Value.Items);
        }

        return Json(items.Where(item => item.CanAssign).Select(item => new
        {
            item.BookingId, item.BookingCode, item.CustomerName, item.HallName,
            EventDate = item.EventDate.ToString("dd/MM/yyyy"), item.Shift, item.TableCount
        }));
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CoordinationHistory(int bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.GetHallManagerCoordinationDetailAsync(token, bookingId, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> AssignCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Tiệc hoặc điều phối viên không hợp lệ." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.AssignCoordinatorAsync(token, model.BookingId, model.CoordinatorEmployeeId, cancellationToken);
        return result.Succeeded ? StatusCode(result.StatusCode ?? 200, result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> ChangeCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Reason))
            return BadRequest(new { message = "Vui lòng chọn điều phối viên và nhập lý do thay đổi." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.ChangeCoordinatorAsync(token, model.BookingId, model.CoordinatorEmployeeId,
            model.Reason, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> CancelCoordinator(CoordinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.BookingId < 1 || string.IsNullOrWhiteSpace(model.Reason))
            return BadRequest(new { message = "Vui lòng nhập lý do hủy phân công." });
        if (!TryGetAccessToken(out var token)) return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn." });
        var result = await _apiClient.CancelCoordinatorAssignmentAsync(token, model.BookingId, model.Reason, cancellationToken);
        return result.Succeeded ? Json(result.Value) : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public Task<IActionResult> WorkSchedule(CancellationToken cancellationToken) =>
        CoordinatorWorkspace("schedule", cancellationToken);

    [Authorize(Roles = RoleNames.Coordinator)]
    public Task<IActionResult> WorkProgress(CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectToAction("Index", "Checklist"));

    private async Task<IActionResult> CoordinatorWorkspace(string page, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token)) return RedirectToAction("Login", "Auth");
        var parties = await _apiClient.GetCoordinatorAssignedPartiesAsync(token, cancellationToken);
        return View("CoordinatorWorkspace", new CoordinatorWorkspaceViewModel
        {
            Page = page, Today = VietnamTime.Now.Date,
            Parties = parties.Succeeded ? parties.Value ?? [] : [],
            ErrorMessage = parties.Succeeded ? null : "Không tải được danh sách tiệc. Vui lòng thử lại."
        });
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public IActionResult AssignedParties() =>
        RedirectToAction(nameof(WorkSchedule), new { scope = "all" });

    [Authorize(Roles = RoleNames.Coordinator)]
    public IActionResult AssignedPartyDetail(int bookingId) =>
        RedirectToAction(nameof(WorkSchedule), new { scope = "all", bookingId });

    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private bool TryGetAccessToken(out string token)
    {
        token = Request.Cookies[ApiTokenCookie] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }
}

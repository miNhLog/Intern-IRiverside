using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Incident;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/su-co")]
public sealed class ManagerIncidentController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;

    public ManagerIncidentController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? status, string? severity, int? hallId,
        DateTime? fromDate, DateTime? toDate, int page = 1, CancellationToken cancellationToken = default)
    {
        var token = Token();
        if (token is null) return RedirectToAction("Login", "Auth");
        var listTask = _api.GetIncidentsAsync(token, search, status, severity, hallId, fromDate, toDate,
            page, 10, cancellationToken);
        var optionsTask = _api.GetIncidentBookingOptionsAsync(token, cancellationToken);
        await Task.WhenAll(listTask, optionsTask);
        var list = await listTask;
        var options = await optionsTask;
        return View(new ManagerIncidentPageViewModel
        {
            Data = list.Value ?? new(), BookingOptions = options.Value ?? [], Search = search, Status = status,
            Severity = severity, HallId = hallId, FromDate = fromDate, ToDate = toDate,
            ErrorMessage = !list.Succeeded ? list.Error : !options.Succeeded ? options.Error : null
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return Unauthorized();
        var detailTask = _api.GetIncidentAsync(token, id, cancellationToken);
        var historyTask = _api.GetIncidentHistoryAsync(token, id, cancellationToken);
        await Task.WhenAll(detailTask, historyTask);
        var detail = await detailTask;
        var history = await historyTask;
        if (!detail.Succeeded || detail.Value is null)
            return StatusCode(detail.StatusCode ?? 502, new { message = detail.Error });
        return PartialView("_Detail", new ManagerIncidentDetailViewModel
        {
            Incident = detail.Value, History = history.Value ?? []
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IncidentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Token();
        if (token is null) return Unauthorized();
        return Result(await _api.CreateIncidentAsync(token, model, cancellationToken), "Đã ghi nhận sự cố mới.");
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, IncidentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Token();
        if (token is null) return Unauthorized();
        return Result(await _api.UpdateIncidentAsync(token, id, model, cancellationToken),
            "Đã cập nhật nội dung báo cáo.");
    }

    [HttpPost("{id:int}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return Unauthorized();
        return Result(await _api.UpdateIncidentStatusAsync(token, id, "PROCESSING", null, cancellationToken),
            "Manager đã tiếp nhận xử lý sự cố.");
    }

    [HttpPost("{id:int}/resolution")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateResolution(int id, IncidentResolutionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Token();
        if (token is null) return Unauthorized();
        return Result(await _api.UpdateIncidentResolutionAsync(token, id, model.Resolution, cancellationToken),
            "Đã lưu hướng giải quyết.");
    }

    [HttpPost("{id:int}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, IncidentResolutionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Token();
        if (token is null) return Unauthorized();
        return Result(await _api.UpdateIncidentStatusAsync(token, id, "RESOLVED", model.Resolution, cancellationToken),
            "Sự cố đã được xác nhận hoàn tất.");
    }

    private IActionResult Result(ApiCallResult<IncidentDetailViewModel> result, string message) => result.Succeeded
        ? Json(new { message, incident = result.Value })
        : StatusCode(result.StatusCode ?? 502, new { message = result.Error });

    private string? Token() => Request.Cookies[TokenCookie];
    private string FirstError() => ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
        .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message)) ?? "Dữ liệu không hợp lệ.";
}

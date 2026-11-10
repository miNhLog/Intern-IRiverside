using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Incident;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Coordinator)]
[Route("dieu-phoi/su-co")]
public sealed class CoordinatorIncidentController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public CoordinatorIncidentController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? status, string? severity, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var listTask = _api.GetIncidentsAsync(token, search, status, severity, null, null, null, page, 10, cancellationToken);
        var optionsTask = _api.GetIncidentBookingOptionsAsync(token, cancellationToken);
        await Task.WhenAll(listTask, optionsTask);
        var list = await listTask;
        var options = await optionsTask;
        return View(new CoordinatorIncidentPageViewModel
        {
            Data = list.Value ?? new(), BookingOptions = options.Value ?? [], Search = search,
            Status = status, Severity = severity,
            ErrorMessage = !list.Succeeded ? list.Error : !options.Succeeded ? options.Error : null
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await _api.GetIncidentAsync(token, id, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? PartialView("_Detail", result.Value)
            : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IncidentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await _api.CreateIncidentAsync(token, model, cancellationToken);
        return result.Succeeded ? Json(new { message = "Đã gửi báo cáo sự cố thành công.", incident = result.Value })
            : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, IncidentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = FirstError() });
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var result = await _api.UpdateIncidentAsync(token, id, model, cancellationToken);
        return result.Succeeded ? Json(new { message = "Đã cập nhật báo cáo sự cố.", incident = result.Value })
            : StatusCode(result.StatusCode ?? 502, new { message = result.Error });
    }

    private string FirstError() => ModelState.Values.SelectMany(v => v.Errors)
        .Select(e => e.ErrorMessage).FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        ?? "Dữ liệu báo cáo không hợp lệ.";
}

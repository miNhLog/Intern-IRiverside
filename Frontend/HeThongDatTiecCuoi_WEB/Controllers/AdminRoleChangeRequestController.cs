using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.RoleChangeRequest;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("quan-tri/yeu-cau-thay-doi-vai-tro")]
public sealed class AdminRoleChangeRequestController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;
    public AdminRoleChangeRequestController(IRiversideApiClient apiClient) => _apiClient = apiClient;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? keyword, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetAdminRoleChangeRequestsAsync(status, keyword, token, cancellationToken);
        return View(new AdminRoleChangeRequestPageViewModel { Requests = result.Value ?? [], Status = status, Keyword = keyword, ErrorMessage = result.Succeeded ? null : result.Error });
    }

    [HttpPost("{id:int}/duyet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.ApproveRoleChangeRequestAsync(id, token, cancellationToken);
        TempData[result.Succeeded ? "RoleAdminSuccess" : "RoleAdminError"] = result.Value?.Message ?? result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/tu-choi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, RejectRoleChangeRequestViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { TempData["RoleAdminError"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(); return RedirectToAction(nameof(Index)); }
        var token = Request.Cookies[TokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.RejectRoleChangeRequestAsync(id, model, token, cancellationToken);
        TempData[result.Succeeded ? "RoleAdminSuccess" : "RoleAdminError"] = result.Value?.Message ?? result.Error;
        return RedirectToAction(nameof(Index));
    }
}

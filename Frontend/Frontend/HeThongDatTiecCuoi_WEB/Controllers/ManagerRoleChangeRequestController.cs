using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.RoleChangeRequest;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/yeu-cau-thay-doi-vai-tro")]
public sealed class ManagerRoleChangeRequestController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public ManagerRoleChangeRequestController(IRiversideApiClient apiClient) => _apiClient = apiClient;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync();
        if (accessToken is null)
            return RedirectToAction("Login", "Auth");

        var candidatesResult = await _apiClient.GetRoleChangeCandidatesAsync(accessToken, cancellationToken);
        var requestsResult = await _apiClient.GetManagerRoleChangeRequestsAsync(accessToken, cancellationToken);
        var model = new ManagerRoleChangeRequestPageViewModel
        {
            Candidates = candidatesResult.Value ?? [],
            Requests = requestsResult.Value ?? [],
            ErrorMessage = !candidatesResult.Succeeded ? candidatesResult.Error
                : !requestsResult.Succeeded ? requestsResult.Error : null
        };
        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind(Prefix = "Form")] CreateRoleChangeRequestViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["RoleChangeError"] = ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Dữ liệu yêu cầu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var accessToken = await GetAccessTokenAsync();
        if (accessToken is null)
            return RedirectToAction("Login", "Auth");

        var result = await _apiClient.CreateRoleChangeRequestAsync(model, accessToken, cancellationToken);
        TempData[result.Succeeded ? "RoleChangeSuccess" : "RoleChangeError"] = result.Succeeded
            ? "Đã gửi yêu cầu thay đổi vai trò thành công."
            : result.Error ?? "Không thể gửi yêu cầu thay đổi vai trò.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (!string.IsNullOrWhiteSpace(accessToken))
            return accessToken;

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(ApiTokenCookie);
        return null;
    }
}

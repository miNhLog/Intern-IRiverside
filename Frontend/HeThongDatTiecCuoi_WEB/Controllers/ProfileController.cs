using System.Security.Claims;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Profile;
using HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager + "," + RoleNames.HallManager + "," + RoleNames.Coordinator)]
[Route("profile")]
public sealed class ProfileController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public ProfileController(IRiversideApiClient apiClient) => _apiClient = apiClient;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        var result = await _apiClient.GetProfileAsync(token, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["ProfileError"] = result.Error ?? "Không thể tải hồ sơ cá nhân.";
            return View(new ProfilePageViewModel
            {
                Profile = new ProfileDto(0, User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                    User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, string.Empty, DateTime.MinValue,
                    null, User.Identity?.Name, null)
            });
        }

        return View(new ProfilePageViewModel
        {
            Profile = result.Value,
            Email = result.Value.Email,
            FullName = result.Value.FullName ?? string.Empty,
            PhoneNumber = result.Value.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ProfilePageViewModel model, CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        if (!ModelState.IsValid)
        {
            var current = await _apiClient.GetProfileAsync(token, cancellationToken);
            model.Profile = current.Value ?? model.Profile;
            return View("Index", model);
        }

        var result = await _apiClient.UpdateProfileAsync(model, token, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể cập nhật hồ sơ cá nhân.");
            var current = await _apiClient.GetProfileAsync(token, cancellationToken);
            model.Profile = current.Value ?? model.Profile;
            return View("Index", model);
        }

        await UpdateProfileClaimsAsync(
            result.Value.Profile.FullName ?? result.Value.Profile.Email,
            result.Value.Profile.Email);
        TempData["ProfileSuccess"] = result.Value.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction("Login", "Auth");
        if (!ModelState.IsValid)
        {
            TempData["ProfileError"] = ModelState.Values.SelectMany(value => value.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        var result = await _apiClient.ChangePasswordAsync(model, token, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ProfileError"] = result.Error ?? "Không thể đổi mật khẩu.";
            return RedirectToAction(nameof(Index));
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(ApiTokenCookie);
        TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
        return RedirectToAction("Login", "Auth");
    }

    private async Task UpdateProfileClaimsAsync(string displayName, string email)
    {
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = User.Claims.Where(claim => claim.Type != ClaimTypes.Name && claim.Type != ClaimTypes.Email)
            .Append(new Claim(ClaimTypes.Name, displayName))
            .Append(new Claim(ClaimTypes.Email, email))
            .ToList();
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            ticket.Properties);
    }
}

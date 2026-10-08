using System.Security.Claims;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class AuthController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private const string ExternalCookieScheme = "External";
    public const string MustChangePasswordClaim = "must_change_password";
    private readonly IRiversideApiClient _apiClient;
    private readonly IConfiguration _configuration;

    public AuthController(IRiversideApiClient apiClient, IConfiguration configuration)
    {
        _apiClient = apiClient;
        _configuration = configuration;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var hasApiToken = !string.IsNullOrWhiteSpace(Request.Cookies[ApiTokenCookie]);

        // CHỈ chuyển hướng vào trong khi CẢ HAI cookie rp_auth VÀ rp_api_token đều còn hợp lệ
        if (User.Identity?.IsAuthenticated == true && hasApiToken)
        {
            var accessToken = Request.Cookies[ApiTokenCookie];

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                Response.Cookies.Delete(ApiTokenCookie);

                return View(new LoginViewModel
                {
                    ReturnUrl = returnUrl
                });
            }

            return RedirectForRole(User.FindFirstValue(ClaimTypes.Role));
        }

        // Nếu có rp_auth nhưng mất rp_api_token (phiên lỗi), tự động dọn dẹp sạch sẽ
        if (User.Identity?.IsAuthenticated == true && !hasApiToken)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete(ApiTokenCookie);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.LoginAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng nhập không thành công.");
            return View(model);
        }

        await SignInAsync(result.Value, model.RememberMe);
        if (result.Value.User.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectForRole(result.Value.User.RoleName);
    }

    [HttpGet("doi-mat-khau")]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost("doi-mat-khau")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        var requiresOtp = bool.TryParse(User.FindFirstValue(MustChangePasswordClaim), out var mustChange) && mustChange;
        if (requiresOtp && (string.IsNullOrWhiteSpace(model.OtpCode) || model.OtpCode.Length != 6 || !model.OtpCode.All(char.IsDigit)))
            ModelState.AddModelError(nameof(model.OtpCode), "Vui lòng nhập mã OTP gồm 6 chữ số.");
        if (!ModelState.IsValid) return View(model);

        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(Login));
        var result = await _apiClient.ChangePasswordAsync(model, token, cancellationToken);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error ?? "Không thể đổi mật khẩu."); return View(model); }
        await UpdateMustChangePasswordClaimAsync(false);
        TempData["PasswordChangeSuccess"] = result.Value?.Message ?? "Đổi mật khẩu thành công.";
        return RedirectForRole(User.FindFirstValue(ClaimTypes.Role));
    }

    [HttpPost("doi-mat-khau/gui-otp")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFirstPasswordOtp(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(token))
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

        var result = await _apiClient.SendFirstPasswordOtpAsync(token, cancellationToken);
        return Json(new
        {
            success = result.Succeeded,
            message = result.Succeeded ? result.Value?.Message : result.Error,
            cooldownSeconds = result.Value?.CooldownSeconds ?? 60
        });
    }

    [HttpGet("auth/google")]
    [AllowAnonymous]
    public IActionResult GoogleLogin(string? returnUrl = null, bool rememberMe = false)
    {
        if (string.IsNullOrWhiteSpace(_configuration["Google:ClientId"]) ||
            string.IsNullOrWhiteSpace(_configuration["Google:ClientSecret"]))
        {
            TempData["Error"] = "Đăng nhập Google chưa được cấu hình.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var redirectUrl = Url.Action(nameof(GoogleCallback), new { returnUrl, rememberMe });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Google");
    }

    [HttpGet("auth/google-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(
        string? returnUrl,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var external = await HttpContext.AuthenticateAsync(ExternalCookieScheme);
        var accessToken = external.Properties?.GetTokenValue("access_token");
        await HttpContext.SignOutAsync(ExternalCookieScheme);

        if (!external.Succeeded || string.IsNullOrWhiteSpace(accessToken))
        {
            TempData["Error"] = "Google không trả về thông tin đăng nhập hợp lệ.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var result = await _apiClient.LoginWithGoogleAsync(accessToken, rememberMe, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["Error"] = result.Error ?? "Đăng nhập Google không thành công.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        await SignInAsync(result.Value, rememberMe);
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectForRole(result.Value.User.RoleName);
    }

    [HttpGet("auth/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.ForgotPasswordAsync(model, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? result.Value?.Message ?? "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
            : result.Error ?? "Không thể gửi yêu cầu đặt lại mật khẩu.";
        return View(model);
    }

    [HttpGet("auth/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token)
    {
        return View(new ResetPasswordViewModel { Token = token ?? string.Empty });
    }

    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.ResetPasswordAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể đặt lại mật khẩu.");
            return View(model);
        }

        TempData["Success"] = result.Value?.Message ?? "Đặt lại mật khẩu thành công.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost("dang-xuat")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        TempData.Remove("PasswordChangeSuccess");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(ApiTokenCookie);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectForRole(string? roleName)
    {
        if (roleName == RoleNames.Admin)
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (roleName is RoleNames.Manager or RoleNames.HallManager or RoleNames.Coordinator)
        {
            return RedirectToAction("Dashboard", "Home");
        }

        return RedirectToAction("Index", "Home");
    }

    private async Task SignInAsync(AuthResponseDto auth, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.UserId.ToString()),
            new(ClaimTypes.Name, auth.User.FullName),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.RoleName),
            new(MustChangePasswordClaim, auth.User.MustChangePassword.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = new DateTimeOffset(auth.ExpiresAtUtc)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        var expireTime = auth.ExpiresAtUtc > DateTime.UtcNow
    ? new DateTimeOffset(auth.ExpiresAtUtc)
    : DateTimeOffset.UtcNow.AddHours(1);

        Response.Cookies.Append(ApiTokenCookie, auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expireTime
        });
    }

    private async Task UpdateMustChangePasswordClaimAsync(bool mustChangePassword)
    {
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = User.Claims
            .Where(claim => claim.Type != MustChangePasswordClaim)
            .Append(new Claim(MustChangePasswordClaim, mustChangePassword.ToString()))
            .ToList();
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = ticket.Properties?.IsPersistent ?? false,
                ExpiresUtc = ticket.Properties?.ExpiresUtc
            });
    }
}

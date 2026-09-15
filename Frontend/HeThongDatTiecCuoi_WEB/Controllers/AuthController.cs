using System.Security.Claims;
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
    private readonly IRiversideApiClient _apiClient;

    public AuthController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("dang-nhap")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Quản trị viên"))
            {
                return RedirectToAction("Index", "AdminSanh");
            }

            return RedirectToAction("Dashboard", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("dang-nhap")]
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

        await SignInAsync(result.Value, model.GhiNhoDangNhap);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        if (result.Value.User.VaiTro == "Quản trị viên")
        {
            return RedirectToAction("Index", "AdminSanh");
        }

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet("dang-ky")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Quản trị viên"))
            {
                return RedirectToAction("Index", "AdminSanh");
            }

            return RedirectToAction("Dashboard", "Home");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost("dang-ky")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.RegisterAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng ký không thành công.");
            return View(model);
        }

        await SignInAsync(result.Value, isPersistent: false);
        TempData["Success"] = "Tạo tài khoản thành công. Chào mừng anh/chị đến Riverside Palace!";
        return RedirectToAction("Dashboard", "Home");
    }

    [HttpPost("dang-xuat")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(ApiTokenCookie);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAsync(AuthResponseDto auth, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.NguoiDungId.ToString()),
            new(ClaimTypes.Name, auth.User.HoTen),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.VaiTro)
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

        Response.Cookies.Append(ApiTokenCookie, auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = new DateTimeOffset(auth.ExpiresAtUtc)
        });
    }
}

using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IFirstPasswordOtpService _firstPasswordOtpService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IPasswordResetService passwordResetService,
        IFirstPasswordOtpService firstPasswordOtpService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _firstPasswordOtpService = firstPasswordOtpService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        return ToActionResult(await _authService.ChangePasswordAsync(userId, request, cancellationToken));
    }

    [HttpPost("first-password-otp")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SendFirstPasswordOtp(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try
        {
            return ToActionResult(await _firstPasswordOtpService.SendAsync(userId, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Không thể gửi OTP đổi mật khẩu lần đầu cho UserId {UserId}.", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse("Không thể gửi mã OTP. Vui lòng kiểm tra cấu hình email và thử lại."));
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _passwordResetService.SendResetLinkForEmailAsync(
                request.Email, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Không thể gửi email đặt lại mật khẩu.");
        }

        return Ok(new
        {
            message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _passwordResetService.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginWithGoogleAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId))
        {
            return Unauthorized(new ApiErrorResponse("Token không hợp lệ."));
        }

        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("staff-only")]
    [Authorize(
    Roles = RoleNames.Admin + "," +
            RoleNames.Manager + "," +
            RoleNames.HallManager + "," +
            RoleNames.Coordinator)]
    public IActionResult StaffOnly()
    {
        return Ok(new
        {
            message = "Đã xác thực quyền nhân viên hoặc quản trị viên."
        });
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        return result.Succeeded
            ? StatusCode(result.StatusCode, result.Value)
            : StatusCode(result.StatusCode, new ApiErrorResponse(result.Error ?? "Đã xảy ra lỗi."));
    }
}

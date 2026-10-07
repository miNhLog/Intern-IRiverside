using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class PasswordResetService : IPasswordResetService
{
    private const string InvalidTokenMessage = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly PasswordResetOptions _options;
    private readonly AccountProvisioningOptions _accountProvisioningOptions;

    public PasswordResetService(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IEmailService emailService,
        IOptions<PasswordResetOptions> options,
        IOptions<AccountProvisioningOptions> accountProvisioningOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _options = options.Value;
        _accountProvisioningOptions = accountProvisioningOptions.Value;
    }

    public async Task<bool> SendResetLinkForEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Email == normalizedEmail &&
                        item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (user is null)
        {
            return false;
        }

        await CreateAndSendAsync(user, false, cancellationToken);
        return true;
    }

    public async Task<ServiceResult<object>> SendResetLinkForUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(item => item.Role)
            .Include(item => item.Employee)
            .SingleOrDefaultAsync(
                item => item.UserId == userId &&
                        item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (user is null)
        {
            return ServiceResult<object>.Failure("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        if (user.Employee is null || (user.Role.RoleName != RoleNames.Manager &&
            user.Role.RoleName != RoleNames.HallManager &&
            user.Role.RoleName != RoleNames.Coordinator))
        {
            return ServiceResult<object>.Failure(
                "Chỉ có thể đặt lại mật khẩu cho tài khoản nhân viên nội bộ.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(user.Email) || !MailAddress.TryCreate(user.Email, out _))
        {
            return ServiceResult<object>.Failure(
                "Không thể gửi yêu cầu đặt lại mật khẩu vì tài khoản chưa có địa chỉ email hợp lệ.",
                StatusCodes.Status400BadRequest);
        }

        var cooldownStart = DateTime.UtcNow.AddSeconds(-Math.Max(1, _options.CooldownSeconds));
        if (await _context.PasswordResetTokens.AsNoTracking().AnyAsync(
            item => item.UserId == userId && item.CreatedAt > cooldownStart,
            cancellationToken))
        {
            return ServiceResult<object>.Failure(
                "Yêu cầu đặt lại mật khẩu vừa được gửi. Vui lòng thử lại sau.",
                StatusCodes.Status429TooManyRequests);
        }

        await CreateAndSendAsync(user, true, cancellationToken);
        return ServiceResult<object>.Success(new { message = "Đã gửi liên kết đặt lại mật khẩu đến email của nhân viên." });
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ServiceResult<object>.Failure(InvalidTokenMessage, StatusCodes.Status400BadRequest);
        }

        if (newPassword != confirmPassword)
        {
            return ServiceResult<object>.Failure(
                "Mật khẩu xác nhận không khớp.", StatusCodes.Status400BadRequest);
        }

        if (!PasswordPolicy.IsValid(newPassword))
        {
            return ServiceResult<object>.Failure(
                "Mật khẩu phải có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.",
                StatusCodes.Status400BadRequest);
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var resetToken = await _context.PasswordResetTokens
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash &&
                        item.UsedAt == null &&
                        item.ExpiresAt > now,
                cancellationToken);

        if (resetToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<object>.Failure(InvalidTokenMessage, StatusCodes.Status400BadRequest);
        }

        if (newPassword == _accountProvisioningOptions.DefaultStaffPassword)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<object>.Failure(
                "Mật khẩu mới không được trùng với mật khẩu mặc định.",
                StatusCodes.Status400BadRequest);
        }

        if (_passwordHasher.VerifyHashedPassword(
                resetToken.User, resetToken.User.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<object>.Failure(
                "Mật khẩu mới phải khác mật khẩu hiện tại.",
                StatusCodes.Status400BadRequest);
        }

        resetToken.User.PasswordHash = _passwordHasher.HashPassword(resetToken.User, newPassword);
        resetToken.User.MustChangePassword = false;
        resetToken.User.UpdatedAt = now;
        resetToken.UsedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<object>.Success(new
        {
            message = "Đặt lại mật khẩu thành công."
        });
    }

    private async Task CreateAndSendAsync(
        User user,
        bool administratorRequested,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FrontendBaseUrl))
        {
            throw new InvalidOperationException("Thiếu cấu hình PasswordReset:FrontendBaseUrl.");
        }

        var lifetimeMinutes = Math.Clamp(_options.TokenLifetimeMinutes, 15, 30);
        var now = DateTime.UtcNow;

        await _context.PasswordResetTokens
            .Where(item => item.UserId == user.UserId && item.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.UsedAt, now),
                cancellationToken);

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var resetToken = new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(lifetimeMinutes)
        };
        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        var resetLink = $"{_options.FrontendBaseUrl.TrimEnd('/')}/auth/reset-password" +
                        $"?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await _emailService.SendPasswordResetEmailAsync(
                user.Email, resetLink, lifetimeMinutes, administratorRequested, cancellationToken);
        }
        catch
        {
            _context.PasswordResetTokens.Remove(resetToken);
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

}

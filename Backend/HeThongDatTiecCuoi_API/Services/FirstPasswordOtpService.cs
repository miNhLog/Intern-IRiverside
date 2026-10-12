using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class FirstPasswordOtpService : IFirstPasswordOtpService
{
    private const int LifetimeMinutes = 5;
    private const int CooldownSeconds = 60;
    private const int MaximumAttempts = 5;
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher<FirstPasswordOtp> _hasher;

    public FirstPasswordOtpService(
        ApplicationDbContext db,
        IEmailService emailService,
        IPasswordHasher<FirstPasswordOtp> hasher)
    {
        _db = db;
        _emailService = emailService;
        _hasher = hasher;
    }

    public async Task<ServiceResult<object>> SendAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null)
            return ServiceResult<object>.Failure("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        if (!user.MustChangePassword)
            return ServiceResult<object>.Failure("Tài khoản không còn yêu cầu đổi mật khẩu lần đầu.", StatusCodes.Status400BadRequest);

        var now = DateTime.Now;
        var latest = await _db.FirstPasswordOtps
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && latest.CreatedAt.AddSeconds(CooldownSeconds) > now)
        {
            var remaining = (int)Math.Ceiling((latest.CreatedAt.AddSeconds(CooldownSeconds) - now).TotalSeconds);
            return ServiceResult<object>.Failure($"Vui lòng chờ {remaining} giây trước khi gửi lại mã OTP.", StatusCodes.Status429TooManyRequests);
        }

        var activeCodes = await _db.FirstPasswordOtps
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var activeCode in activeCodes) activeCode.UsedAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new FirstPasswordOtp
        {
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(LifetimeMinutes)
        };
        otp.CodeHash = _hasher.HashPassword(otp, code);
        _db.FirstPasswordOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendFirstPasswordOtpEmailAsync(user.Email, code, LifetimeMinutes, cancellationToken);
        }
        catch
        {
            otp.UsedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return ServiceResult<object>.Success(new { message = $"Mã OTP đã được gửi đến {MaskEmail(user.Email)}.", cooldownSeconds = CooldownSeconds });
    }

    public async Task<ServiceResult<object>> ValidateAsync(int userId, string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
            return ServiceResult<object>.Failure("Vui lòng nhập mã OTP gồm 6 chữ số.", StatusCodes.Status400BadRequest);

        var now = DateTime.Now;
        var otp = await _db.FirstPasswordOtps
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (otp is null)
            return ServiceResult<object>.Failure("Bạn chưa gửi mã OTP hoặc mã đã được sử dụng.", StatusCodes.Status400BadRequest);
        if (otp.ExpiresAt <= now)
        {
            otp.UsedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult<object>.Failure("Mã OTP đã hết hạn. Vui lòng gửi mã mới.", StatusCodes.Status400BadRequest);
        }
        if (otp.FailedAttempts >= MaximumAttempts)
            return ServiceResult<object>.Failure("Mã OTP đã bị khóa do nhập sai quá 5 lần. Vui lòng gửi mã mới.", StatusCodes.Status400BadRequest);

        if (_hasher.VerifyHashedPassword(otp, otp.CodeHash, code) == PasswordVerificationResult.Failed)
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= MaximumAttempts) otp.UsedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            var remaining = MaximumAttempts - otp.FailedAttempts;
            return ServiceResult<object>.Failure(
                remaining > 0 ? $"Mã OTP không chính xác. Bạn còn {remaining} lần thử." : "Mã OTP đã bị khóa. Vui lòng gửi mã mới.",
                StatusCodes.Status400BadRequest);
        }

        otp.UsedAt = now;
        return ServiceResult<object>.Success(new { message = "Mã OTP hợp lệ." });
    }

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 1) return email;
        return $"{email[0]}{new string('*', Math.Min(5, separator - 1))}{email[separator..]}";
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, bool rememberMe)
    {
        var now = DateTime.UtcNow;
        var expiresAt = rememberMe
            ? now.AddDays(_options.RememberMeDays)
            : now.AddMinutes(_options.AccessTokenMinutes);

        // Tài khoản nội bộ có thể từng phát sinh hồ sơ khách hàng khi thử đặt tiệc.
        // Luôn ưu tiên hồ sơ nhân viên để claim tên không bị đổi thành tên người đặt tiệc.
        var displayName = user.Employee?.FullName ?? user.Customer?.FullName ?? user.Email;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.RoleName),
            new("session_version", (user.UpdatedAt?.ToUniversalTime().Ticks ?? 0).ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

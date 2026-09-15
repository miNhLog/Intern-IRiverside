using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(NguoiDung user, bool rememberMe);
}

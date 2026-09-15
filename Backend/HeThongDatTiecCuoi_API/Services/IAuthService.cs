using HeThongDatTiecCuoi_API.DTOs.Auth;

namespace HeThongDatTiecCuoi_API.Services;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken);
}

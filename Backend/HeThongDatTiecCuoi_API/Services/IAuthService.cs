using HeThongDatTiecCuoi_API.DTOs.Auth;

namespace HeThongDatTiecCuoi_API.Services;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<object>> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponse>> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken);
}

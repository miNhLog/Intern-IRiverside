namespace HeThongDatTiecCuoi_API.Services;

public interface IFirstPasswordOtpService
{
    Task<ServiceResult<object>> SendAsync(int userId, CancellationToken cancellationToken);
    Task<ServiceResult<object>> ValidateAsync(int userId, string? code, CancellationToken cancellationToken);
}

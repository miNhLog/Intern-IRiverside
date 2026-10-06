using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public interface IStatusService
{
    Task<Status?> GetStatusAsync(
        string group,
        string code,
        CancellationToken cancellationToken = default);

    Task<int> GetStatusIdAsync(
        string group,
        string code,
        CancellationToken cancellationToken = default);
}

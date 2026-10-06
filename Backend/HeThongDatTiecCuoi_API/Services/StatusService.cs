using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class StatusService : IStatusService
{
    private readonly ApplicationDbContext _context;

    public StatusService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Status?> GetStatusAsync(
        string group,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroup = group.Trim().ToUpperInvariant();
        var normalizedCode = code.Trim().ToUpperInvariant();

        return _context.Statuses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                status => status.StatusGroup == normalizedGroup &&
                          status.StatusCode == normalizedCode,
                cancellationToken);
    }

    public async Task<int> GetStatusIdAsync(
        string group,
        string code,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(group, code, cancellationToken);
        return status?.StatusId
            ?? throw new InvalidOperationException(
                $"Missing status configuration: {group}/{code}.");
    }
}

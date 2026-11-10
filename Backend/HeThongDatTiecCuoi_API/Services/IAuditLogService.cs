using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public interface IAuditLogService
{
    AuditLog Create(string action, int? actorUserId, long targetUserId, string? authMethod,
        string outcome, string notes, string? correlationId = null);
    Task TrySaveAsync(AuditLog log, CancellationToken cancellationToken);
}

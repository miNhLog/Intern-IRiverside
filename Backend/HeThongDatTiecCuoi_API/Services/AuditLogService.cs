using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public AuditLog Create(string action, int? actorUserId, long targetUserId, string? authMethod,
        string outcome, string notes, string? correlationId = null)
    {
        var context = _httpContextAccessor.HttpContext;
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        return new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityName = AuditEntityNames.User,
            EntityId = targetUserId,
            Timestamp = DateTime.Now,
            Notes = notes,
            // The API is called through the MVC server. Without a configured trusted-proxy
            // chain RemoteIpAddress is not the end user's address, so it is deliberately null.
            IpAddress = null,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(500, userAgent.Length)],
            AuthMethod = authMethod,
            Outcome = outcome,
            CorrelationId = correlationId ?? context?.TraceIdentifier
        };
    }

    public async Task TrySaveAsync(AuditLog log, CancellationToken cancellationToken)
    {
        try
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _context.Entry(log).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            _logger.LogError(exception, "Không thể ghi audit log {Action} cho đối tượng {EntityId}.", log.Action, log.EntityId);
        }
    }
}

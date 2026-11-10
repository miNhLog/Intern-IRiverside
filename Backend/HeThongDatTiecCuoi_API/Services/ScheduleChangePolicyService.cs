using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public interface IScheduleChangePolicyService
{
    Task<ScheduleChangePolicy> GetActiveAsync(DateTime at, CancellationToken cancellationToken = default);
    Task<List<ScheduleChangePolicyDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<ScheduleChangePolicyDto> CreateVersionAsync(int userId, ScheduleChangePolicyVersionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ScheduleChangePolicyService : IScheduleChangePolicyService
{
    private readonly ApplicationDbContext _context;
    public ScheduleChangePolicyService(ApplicationDbContext context) => _context = context;

    public async Task<ScheduleChangePolicy> GetActiveAsync(DateTime at, CancellationToken cancellationToken = default) =>
        await _context.ScheduleChangePolicies.Include(x => x.Tiers)
            .Where(x => x.PolicyCode == "POL-CHG-05" && x.Status == ScheduleChangePolicyStatusCodes.Active &&
                x.EffectiveFrom <= at && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value > at))
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException("Chưa có phiên bản POL-CHG-05 đang hiệu lực.");

    public async Task<List<ScheduleChangePolicyDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _context.ScheduleChangePolicies.AsNoTracking().Include(x => x.Tiers)
            .Where(x => x.PolicyCode == "POL-CHG-05").OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        return policies.Select(ToDto).ToList();
    }

    public async Task<ScheduleChangePolicyDto> CreateVersionAsync(int userId, ScheduleChangePolicyVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var latestEffectiveFrom = await _context.ScheduleChangePolicies
            .Where(x => x.PolicyCode == "POL-CHG-05")
            .MaxAsync(x => (DateTime?)x.EffectiveFrom, cancellationToken);
        if (latestEffectiveFrom.HasValue && request.EffectiveFrom <= latestEffectiveFrom.Value)
            throw new ArgumentException("Ngày hiệu lực của phiên bản mới phải sau phiên bản gần nhất.");
        var policy = new ScheduleChangePolicy
        {
            PolicyCode = "POL-CHG-05",
            Version = (await _context.ScheduleChangePolicies.Where(x => x.PolicyCode == "POL-CHG-05")
                .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1,
            OnlineMinimumDays = request.OnlineMinimumDays,
            NewDateMinimumDays = request.NewDateMinimumDays,
            PaymentWindowMinutes = request.PaymentWindowMinutes,
            HoldWindowMinutes = request.HoldWindowMinutes,
            Status = ScheduleChangePolicyStatusCodes.Active,
            EffectiveFrom = request.EffectiveFrom,
            CreatedByUserId = userId,
            CreatedAt = RiversideBusinessClock.Now,
            ChangeNote = request.ChangeNote?.Trim(),
            Tiers = request.Tiers.Select(x => new ScheduleChangePolicyTier
            {
                MinimumDays = x.MinimumDays, MaximumDays = x.MaximumDays,
                FeeAmount = x.FeeAmount, CustomerOnlineAllowed = x.CustomerOnlineAllowed
            }).ToList()
        };
        ScheduleChangeBusinessRules.ValidatePolicy(policy);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var overlapping = await _context.ScheduleChangePolicies
            .Where(x => x.PolicyCode == "POL-CHG-05" && x.Status == ScheduleChangePolicyStatusCodes.Active &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value > policy.EffectiveFrom))
            .ToListAsync(cancellationToken);
        foreach (var old in overlapping)
        {
            old.EffectiveTo = policy.EffectiveFrom;
            if (policy.EffectiveFrom <= RiversideBusinessClock.Now) old.Status = ScheduleChangePolicyStatusCodes.Retired;
        }
        _context.ScheduleChangePolicies.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(policy);
    }

    private static ScheduleChangePolicyDto ToDto(ScheduleChangePolicy x) => new()
    {
        PolicyId = x.PolicyId, PolicyCode = x.PolicyCode, Version = x.Version,
        OnlineMinimumDays = x.OnlineMinimumDays, NewDateMinimumDays = x.NewDateMinimumDays,
        PaymentWindowMinutes = x.PaymentWindowMinutes, HoldWindowMinutes = x.HoldWindowMinutes,
        Status = x.Status, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo,
        ChangeNote = x.ChangeNote,
        Tiers = x.Tiers.OrderBy(t => t.MinimumDays).Select(t => new ScheduleChangePolicyTierRequest
        { MinimumDays = t.MinimumDays, MaximumDays = t.MaximumDays, FeeAmount = t.FeeAmount,
            CustomerOnlineAllowed = t.CustomerOnlineAllowed }).ToList()
    };
}

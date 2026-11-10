using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.Options;

/// <summary>
/// TODO-4: Gom hang dung sai ngan sach ve mot cho, bind tu appsettings.
/// prompt.txt: TongChiPhi &lt;= NganSachTran + Δ, voi Δ = 10.000.000 VND
/// (hoac &lt;= 5% ngan sach tran). Quyet: Δ = min(10tr, 5% budget) de tranh
/// vuot 20-30tr voi budget lon; dat UseMinTolerance=false de doi sang max.
/// </summary>
public sealed class PricingPolicyOptions
{
    public const string SectionName = "PricingPolicy";

    [Range(0, 100_000_000)]
    public decimal OverBudgetToleranceVnd { get; init; } = 10_000_000m;

    [Range(0, 1)]
    public double OverBudgetTolerancePercent { get; init; } = 0.05;

    public bool UseMinTolerance { get; init; } = true;

    public decimal GetTolerance(decimal budget)
    {
        var relative = budget > 0 ? budget * (decimal)OverBudgetTolerancePercent : 0m;
        return UseMinTolerance
            ? Math.Min(OverBudgetToleranceVnd, relative)
            : Math.Max(OverBudgetToleranceVnd, relative);
    }
}

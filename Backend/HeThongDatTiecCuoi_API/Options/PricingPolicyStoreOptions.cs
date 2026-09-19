using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.Options;

public sealed class PricingPolicyStoreOptions
{
    public const string SectionName = "PricingPolicyStore";

    [Required]
    public string FilePath { get; init; } = string.Empty;

    [Range(1, 60)]
    public int LockTimeoutSeconds { get; init; } = 5;
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_WEB.Models.AdminPricingPolicy;

public sealed class PricingResponseDto
{
    public string Currency { get; init; } = "VND";

    public List<PricingCategoryResponseDto> Categories { get; init; } = [];
}

public sealed class PricingCategoryResponseDto
{
    public string Category { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public List<PricingItemDto> Items { get; init; } = [];
}

public sealed class PricingItemDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class UpdatePriceRequestDto
{
    [Required]
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? Price { get; init; }

    [Required]
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    [JsonPropertyName("expectedPrice")]
    public decimal? ExpectedPrice { get; init; }
}

public sealed class PolicyResponseDto
{
    public int SchemaVersion { get; init; }

    public long Version { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public string Currency { get; init; } = "VND";

    public DepositPolicyDto Deposit { get; init; } = new();

    public CancellationPolicyDto Cancellation { get; init; } = new();
}

public sealed class DepositPolicyDto
{
    public decimal TotalPercentageOfContractValue { get; init; }

    public List<DepositInstallmentDto> Installments { get; init; } = [];
}

public sealed class DepositInstallmentDto
{
    public int Sequence { get; init; }

    [JsonPropertyName("dueBeforeEventDays")]
    public int DueBeforeEventDays { get; init; }

    [JsonPropertyName("percentageOfContractValue")]
    public decimal PercentageOfContractValue { get; init; }

    public string? Note { get; init; }
}

public sealed class CancellationPolicyDto
{
    public List<CancellationTierDto> Tiers { get; init; } = [];
}

public sealed class CancellationTierDto
{
    public int Sequence { get; init; }

    [JsonPropertyName("noticeDaysFrom")]
    public int NoticeDaysFrom { get; init; }

    [JsonPropertyName("noticeDaysTo")]
    public int? NoticeDaysTo { get; init; }

    [JsonPropertyName("depositPenaltyPercentage")]
    public decimal DepositPenaltyPercentage { get; init; }

    [JsonPropertyName("depositRefundPercentage")]
    public decimal DepositRefundPercentage { get; init; }

    public string? Note { get; init; }
}

public sealed class UpdatePolicyRequestDto
{
    [Required]
    [Range(1, long.MaxValue)]
    [JsonPropertyName("expectedVersion")]
    public long? ExpectedVersion { get; init; }

    [Required]
    public DepositPolicyRequestDto? Deposit { get; init; }

    [Required]
    public CancellationPolicyRequestDto? Cancellation { get; init; }
}

public sealed class DepositPolicyRequestDto
{
    [Required]
    [Range(typeof(decimal), "0", "100")]
    [JsonPropertyName("totalPercentageOfContractValue")]
    public decimal? TotalPercentageOfContractValue { get; init; }

    [Required]
    public List<DepositInstallmentRequestDto>? Installments { get; init; }
}

public sealed class DepositInstallmentRequestDto
{
    [Range(1, int.MaxValue)]
    public int Sequence { get; init; }

    [Range(0, int.MaxValue)]
    [JsonPropertyName("dueBeforeEventDays")]
    public int DueBeforeEventDays { get; init; }

    [Range(typeof(decimal), "0", "100")]
    [JsonPropertyName("percentageOfContractValue")]
    public decimal PercentageOfContractValue { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

public sealed class CancellationPolicyRequestDto
{
    [Required]
    public List<CancellationTierRequestDto>? Tiers { get; init; }
}

public sealed class CancellationTierRequestDto
{
    [Range(1, int.MaxValue)]
    public int Sequence { get; init; }

    [Range(0, int.MaxValue)]
    [JsonPropertyName("noticeDaysFrom")]
    public int NoticeDaysFrom { get; init; }

    [Range(0, int.MaxValue)]
    [JsonPropertyName("noticeDaysTo")]
    public int? NoticeDaysTo { get; init; }

    [Range(typeof(decimal), "0", "100")]
    [JsonPropertyName("depositPenaltyPercentage")]
    public decimal DepositPenaltyPercentage { get; init; }

    [Range(typeof(decimal), "0", "100")]
    [JsonPropertyName("depositRefundPercentage")]
    public decimal DepositRefundPercentage { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

public sealed class AdminPricingPolicyViewModel
{
    public string ActiveTab { get; init; } = "pricing";

    public PricingResponseDto? Pricing { get; init; }

    public PolicyResponseDto? Policies { get; init; }

    public string? PricingError { get; init; }

    public string? PoliciesError { get; init; }
}

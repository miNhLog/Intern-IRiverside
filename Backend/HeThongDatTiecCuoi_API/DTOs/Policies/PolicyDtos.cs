using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Policies;

public sealed class PolicyResponseDto
{
    public int SchemaVersion { get; init; } = 1;

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

    public int DueBeforeEventDays { get; init; }

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

    public int NoticeDaysFrom { get; init; }

    public int? NoticeDaysTo { get; init; }

    public decimal DepositPenaltyPercentage { get; init; }

    public decimal DepositRefundPercentage { get; init; }

    public string? Note { get; init; }
}

public sealed class UpdatePolicyRequestDto
{
    [Required(ErrorMessage = "Expected policy version is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Expected policy version must be greater than 0.")]
    public long? ExpectedVersion { get; init; }

    [Required(ErrorMessage = "Deposit policy is required.")]
    public DepositPolicyRequestDto? Deposit { get; init; }

    [Required(ErrorMessage = "Cancellation policy is required.")]
    public CancellationPolicyRequestDto? Cancellation { get; init; }
}

public sealed class DepositPolicyRequestDto
{
    [Required(ErrorMessage = "Total deposit percentage is required.")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "Total deposit percentage must be between 0 and 100%.")]
    public decimal? TotalPercentageOfContractValue { get; init; }

    [Required(ErrorMessage = "Deposit installments are required.")]
    public List<DepositInstallmentRequestDto>? Installments { get; init; }
}

public sealed class DepositInstallmentRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Installment sequence must start at 1.")]
    public int Sequence { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "Due-before-event days must be greater than or equal to 0.")]
    public int DueBeforeEventDays { get; init; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Deposit percentage must be between 0 and 100%.")]
    public decimal PercentageOfContractValue { get; init; }

    public string? Note { get; init; }
}

public sealed class CancellationPolicyRequestDto
{
    [Required(ErrorMessage = "Cancellation tiers are required.")]
    public List<CancellationTierRequestDto>? Tiers { get; init; }
}

public sealed class CancellationTierRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Cancellation tier sequence must start at 1.")]
    public int Sequence { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "Notice-day lower bound must be greater than or equal to 0.")]
    public int NoticeDaysFrom { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "Notice-day upper bound must be greater than or equal to 0.")]
    public int? NoticeDaysTo { get; init; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Deposit penalty percentage must be between 0 and 100%.")]
    public decimal DepositPenaltyPercentage { get; init; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Deposit refund percentage must be between 0 and 100%.")]
    public decimal DepositRefundPercentage { get; init; }

    public string? Note { get; init; }
}

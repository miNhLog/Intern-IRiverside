using HeThongDatTiecCuoi_API.DTOs.Policies;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPolicyStore
{
    Task<PolicyResponseDto> GetAsync(CancellationToken cancellationToken);

    Task<PolicyResponseDto> UpdateAsync(
        long expectedVersion,
        PolicyContent content,
        CancellationToken cancellationToken);
}

public sealed class PolicyContent
{
    public DepositPolicyDto Deposit { get; init; } = new();

    public CancellationPolicyDto Cancellation { get; init; } = new();
}

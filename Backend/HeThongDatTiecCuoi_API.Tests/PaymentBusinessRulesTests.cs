using HeThongDatTiecCuoi_API.Services;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PaymentBusinessRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.001, false)]
    [InlineData(1.01, true)]
    public void Money_RequiresPositiveExactPrecision(decimal value, bool valid) =>
        Assert.Equal(valid, PaymentBusinessRules.IsMoney(value));

    [Theory]
    [InlineData("DRAFT", "Đặt cọc", 20, null)]
    [InlineData("DRAFT", "Thanh toán đợt", 20, "blocked")]
    [InlineData("ACTIVE", "Thanh toán đợt", 60, null)]
    [InlineData("ACTIVE", "Quyết toán", 61, "blocked")]
    [InlineData("CANCELLED", "Đặt cọc", 1, "blocked")]
    [InlineData("COMPLETED", "Quyết toán", 1, "blocked")]
    public void Recording_EnforcesStateAndRemainingBalance(string status, string type, decimal amount, string? error) =>
        Assert.Equal(error is null, PaymentBusinessRules.Validate(status, "CONFIRMED", 100m, 30m, 40m, type, amount) is null);

    [Fact]
    public void MissingCommitment_IsBlocked() =>
        Assert.NotNull(PaymentBusinessRules.Validate("ACTIVE", "CONFIRMED", 100m, null, 0m, "Đặt cọc", 10m));

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(40, true)]
    public void Activation_RequiresAccumulatedCommitment(decimal deposits, bool accepted) =>
        Assert.Equal(accepted, ContractBusinessRules.ValidateActivation("DRAFT", 100m, "text", "terms", "CONFIRMED", 30m, deposits) is null);
}

using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc18FinancialLedgerTests
{
    [Fact]
    public void RefundMoneyRemainsWholeVndWhileReceiptStorageMayUseTwoDecimals()
    {
        Assert.True(Uc18FinancialLedger.IsValidRefundMoney(100m));
        Assert.False(Uc18FinancialLedger.IsValidRefundMoney(100.01m));
    }

    private static Uc18BookingOperation Operation(
        decimal principalDue = 100m,
        decimal depositPortion = 60m,
        decimal compensationDue = 20m) => new()
        {
            PrincipalRefundDue = principalDue,
            DepositPrincipalPortion = depositPortion,
            CompensationDue = compensationDue
        };

    private static Uc18RefundConfirmation Confirmation(
        decimal principal = 0m,
        decimal depositPrincipal = 0m,
        decimal compensation = 0m) => new()
        {
            PrincipalPaid = principal,
            DepositPrincipalPaid = depositPrincipal,
            CompensationPaid = compensation
        };

    [Fact]
    public void RefundConfirmation_AcceptsPartialPrincipalAndCompensation()
    {
        Assert.Null(Uc18FinancialLedger.ValidateRefundConfirmation(
            Operation(), Confirmation(30m, 20m, 5m), 10m, 10m, 5m));
    }

    [Theory]
    [InlineData(80, 10, 0, 30, 0, 0)]
    [InlineData(30, 70, 0, 0, 0, 0)]
    [InlineData(0, 0, 16, 0, 0, 5)]
    [InlineData(1.5, 0, 0, 0, 0, 0)]
    [InlineData(-1, 0, 0, 0, 0, 0)]
    public void RefundConfirmation_RejectsAmountsOverCapsOrInvalidAmounts(
        decimal principal,
        decimal depositPrincipal,
        decimal compensation,
        decimal priorPrincipal,
        decimal priorDepositPrincipal,
        decimal priorCompensation)
    {
        Assert.NotNull(Uc18FinancialLedger.ValidateRefundConfirmation(
            Operation(), Confirmation(principal, depositPrincipal, compensation),
            priorPrincipal, priorDepositPrincipal, priorCompensation));
    }

    [Fact]
    public void RefundConfirmation_RejectsNonDepositAllocationOverItsCap()
    {
        Assert.NotNull(Uc18FinancialLedger.ValidateRefundConfirmation(
            Operation(100m, 60m), Confirmation(50m, 0m), 0m, 0m, 0m));
    }

    [Fact]
    public void RefundConfirmation_RequiresPositiveWholeVndTotal()
    {
        Assert.NotNull(Uc18FinancialLedger.ValidateRefundConfirmation(
            Operation(), Confirmation(), 0m, 0m, 0m));
        Assert.NotNull(Uc18FinancialLedger.ValidateRefundConfirmation(
            Operation(), Confirmation(1m, 0.5m), 0m, 0m, 0m));
    }
}

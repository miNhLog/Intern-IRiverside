using HeThongDatTiecCuoi_API.Services;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc18FinancialRulesTests
{
    [Theory]
    [InlineData(29, 100)]
    [InlineData(30, 100)]
    [InlineData(59, 100)]
    [InlineData(60, 50)]
    [InlineData(44, 100)]
    [InlineData(45, 100)]
    public void CustomerCancellation_UsesNoticeBands(int noticeDays, decimal expectedRetained)
    {
        var settlement = Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(100m, 0m, 150m, 0m), noticeDays);

        Assert.Equal(expectedRetained, settlement.RetainedDeposit);
        Assert.Equal(150m - expectedRetained, settlement.PrincipalRefund);
    }

    [Fact]
    public void CustomerCancellation_Under30AllowsOnlyLowerAgreedRetention()
    {
        var settlement = Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(100m, 0m, 150m, 0m), 29, 25m);

        Assert.Equal(25m, settlement.RetainedDeposit);
        Assert.Equal(125m, settlement.PrincipalRefund);
        Assert.Throws<ArgumentOutOfRangeException>(() => Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(100m, 0m, 150m, 0m), 29, 100m));
        Assert.Throws<ArgumentException>(() => Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(100m, 0m, 150m, 0m), 30, 25m));
    }

    [Fact]
    public void CustomerCancellation_WithoutDepositRefundsInstallmentsAndNoPenalty()
    {
        var settlement = Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(0m, 0m, 80m, 20m), 10);

        Assert.Equal(0m, settlement.RetainedDeposit);
        Assert.Equal(60m, settlement.PrincipalRefund);
    }

    [Fact]
    public void PriorDepositRefundAndSeparateInstallmentAreAccountedForSeparately()
    {
        var settlement = Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(100m, 40m, 200m, 50m), 60);

        Assert.Equal(30m, settlement.RetainedDeposit);
        Assert.Equal(120m, settlement.PrincipalRefund);
    }

    [Fact]
    public void CustomerCancellation_MidpointRoundsAwayFromZero()
    {
        var settlement = Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(5m, 0m, 5m, 0m), 60);

        Assert.Equal(3m, settlement.RetainedDeposit);
        Assert.Equal(2m, settlement.PrincipalRefund);
    }

    [Fact]
    public void VenueCancellation_RefundsAllPrincipalAndOptionallyCompensatesNetDeposit()
    {
        var receipts = new Uc18BookingReceipts(100m, 40m, 200m, 50m);

        Assert.Equal(new Uc18VenueCancellation(150m, 0m), Uc18FinancialRules.VenueCancellation(receipts, false));
        Assert.Equal(new Uc18VenueCancellation(150m, 60m), Uc18FinancialRules.VenueCancellation(receipts, true));
    }

    [Fact]
    public void RescheduleFee_DistinguishesFirstRepeatAndMenuOnly()
    {
        Assert.Equal(5_000_000m, Uc18FinancialRules.RescheduleFee(100m, 29, true, false));
        Assert.Equal(0m, Uc18FinancialRules.RescheduleFee(100m, 30, true, false));
        Assert.Equal(0m, Uc18FinancialRules.RescheduleFee(100m, 44, true, false));
        Assert.Equal(0m, Uc18FinancialRules.RescheduleFee(100m, 45, true, false));
        Assert.Equal(5m, Uc18FinancialRules.RescheduleFee(100m, 60, true, true));
        Assert.Equal(1m, Uc18FinancialRules.RescheduleFee(10m, 10, true, true));
        Assert.Equal(0m, Uc18FinancialRules.RescheduleFee(100m, 10, false, true));
        Assert.Equal(0m, Uc18FinancialRules.RescheduleFee(0m, 10, true, true));
        Assert.Equal(5_000_000m, Uc18FinancialRules.RescheduleFee(100m, 0, true, false));
    }

    [Fact]
    public void RulesRejectInvalidConservationFractionalVndAndPastNotice()
    {
        Assert.Throws<ArgumentException>(() => Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(10m, 2m, 10m, 3m), 30));
        Assert.Throws<ArgumentException>(() => Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(10.5m, 0m, 20m, 0m), 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => Uc18FinancialRules.CustomerCancellation(
            new Uc18BookingReceipts(10m, 0m, 20m, 0m), -1));
        Assert.Throws<ArgumentException>(() => Uc18FinancialRules.RescheduleFee(10.5m, 30, true, true));
    }
}

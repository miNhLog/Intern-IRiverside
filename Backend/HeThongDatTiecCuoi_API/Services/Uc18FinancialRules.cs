namespace HeThongDatTiecCuoi_API.Services;

public sealed record Uc18BookingReceipts(
    decimal GrossDeposit,
    decimal DepositPrincipalReturned,
    decimal GrossCollected,
    decimal TotalPrincipalReturned);

public sealed record Uc18CustomerCancellation(decimal PrincipalRefund, decimal RetainedDeposit);

public sealed record Uc18VenueCancellation(decimal PrincipalRefund, decimal DepositCompensation);

public static class Uc18FinancialRules
{
    public const decimal FirstRescheduleFeeUnder30Days = 5_000_000m;

    public static Uc18CustomerCancellation CustomerCancellation(
        Uc18BookingReceipts receipts,
        int noticeDays,
        decimal? agreedLowerRetention = null)
    {
        ValidateReceipts(receipts);
        ValidateNoticeDays(noticeDays);

        var netDeposit = receipts.GrossDeposit - receipts.DepositPrincipalReturned;
        decimal retainedDeposit;

        if (noticeDays >= 60)
        {
            if (agreedLowerRetention.HasValue)
                throw new ArgumentException("A lower retention agreement applies only under 30 days.", nameof(agreedLowerRetention));
            retainedDeposit = RoundVnd(netDeposit * 0.5m);
        }
        else if (noticeDays >= 30)
        {
            if (agreedLowerRetention.HasValue)
                throw new ArgumentException("A lower retention agreement applies only under 30 days.", nameof(agreedLowerRetention));
            retainedDeposit = netDeposit;
        }
        else
        {
            retainedDeposit = netDeposit;
            if (agreedLowerRetention.HasValue)
            {
                ValidateWholeVnd(agreedLowerRetention.Value, nameof(agreedLowerRetention));
                if (agreedLowerRetention.Value < 0m || agreedLowerRetention.Value >= netDeposit)
                    throw new ArgumentOutOfRangeException(nameof(agreedLowerRetention), "Agreed retention must be lower than the net deposit.");
                retainedDeposit = agreedLowerRetention.Value;
            }
        }

        var outstandingPrincipal = receipts.GrossCollected - receipts.TotalPrincipalReturned;
        return new Uc18CustomerCancellation(
            outstandingPrincipal - retainedDeposit,
            retainedDeposit);
    }

    public static Uc18VenueCancellation VenueCancellation(
        Uc18BookingReceipts receipts,
        bool depositSanctionsApply)
    {
        ValidateReceipts(receipts);
        var netDeposit = receipts.GrossDeposit - receipts.DepositPrincipalReturned;
        var compensation = depositSanctionsApply ? netDeposit : 0m;
        return new Uc18VenueCancellation(
            receipts.GrossCollected - receipts.TotalPrincipalReturned,
            compensation);
    }

    public static decimal RescheduleFee(
        decimal netDeposit,
        int noticeDays,
        bool isRealSlotMove,
        bool isRepeat)
    {
        ValidateWholeVnd(netDeposit, nameof(netDeposit));
        if (netDeposit < 0m)
            throw new ArgumentOutOfRangeException(nameof(netDeposit), "Net deposit cannot be negative.");
        ValidateNoticeDays(noticeDays);

        if (!isRealSlotMove) return 0m;
        if (isRepeat) return RoundVnd(netDeposit * 0.05m);
        return noticeDays >= 30 ? 0m : FirstRescheduleFeeUnder30Days;
    }

    private static void ValidateReceipts(Uc18BookingReceipts receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        ValidateWholeVnd(receipts.GrossDeposit, nameof(receipts.GrossDeposit));
        ValidateWholeVnd(receipts.DepositPrincipalReturned, nameof(receipts.DepositPrincipalReturned));
        ValidateWholeVnd(receipts.GrossCollected, nameof(receipts.GrossCollected));
        ValidateWholeVnd(receipts.TotalPrincipalReturned, nameof(receipts.TotalPrincipalReturned));

        if (receipts.GrossDeposit < 0m || receipts.DepositPrincipalReturned < 0m ||
            receipts.GrossCollected < 0m || receipts.TotalPrincipalReturned < 0m)
            throw new ArgumentOutOfRangeException(nameof(receipts), "Receipt amounts cannot be negative.");

        var netDeposit = receipts.GrossDeposit - receipts.DepositPrincipalReturned;
        var netCollected = receipts.GrossCollected - receipts.TotalPrincipalReturned;
        var grossNonDeposit = receipts.GrossCollected - receipts.GrossDeposit;
        var refundedNonDeposit = receipts.TotalPrincipalReturned - receipts.DepositPrincipalReturned;
        if (receipts.GrossDeposit > receipts.GrossCollected ||
            receipts.DepositPrincipalReturned > receipts.GrossDeposit ||
            receipts.TotalPrincipalReturned < receipts.DepositPrincipalReturned ||
            receipts.TotalPrincipalReturned > receipts.GrossCollected ||
            netDeposit > netCollected ||
            refundedNonDeposit > grossNonDeposit)
            throw new ArgumentException("Receipt amounts violate principal conservation.", nameof(receipts));
    }

    private static void ValidateNoticeDays(int noticeDays)
    {
        if (noticeDays < 0)
            throw new ArgumentOutOfRangeException(nameof(noticeDays), "Notice days cannot be negative.");
    }

    private static void ValidateWholeVnd(decimal amount, string parameterName)
    {
        if (decimal.Round(amount, 0) != amount)
            throw new ArgumentException("Money inputs must be whole VND; fractional-VND balances are not reconcilable safely.", parameterName);
    }

    private static decimal RoundVnd(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}

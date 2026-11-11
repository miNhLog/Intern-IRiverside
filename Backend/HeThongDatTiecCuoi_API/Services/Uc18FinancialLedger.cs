using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed record Uc18FinancialLedgerSnapshot(
    Uc18BookingReceipts Receipts,
    decimal EffectiveTotal,
    int PriorRealRescheduleCount,
    bool HasPendingReceipts);

public sealed class Uc18FinancialLedger
{
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;
    private readonly ApplicationDbContext _db;

    public Uc18FinancialLedger(ApplicationDbContext db) => _db = db;

    public async Task<Uc18FinancialLedgerSnapshot> ReadAsync(
        Contract contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.ContractId <= 0)
            throw new ArgumentOutOfRangeException(nameof(contract), "Contract must have a persisted ID.");
        ValidateMoney(contract.TotalValue, nameof(contract.TotalValue));

        var paymentGroup = _db.Set<Payment>().Where(payment =>
            payment.ContractId == contract.ContractId &&
            payment.Status.StatusGroup == StatusGroups.Payment);
        var successfulPayments = paymentGroup.Where(payment =>
            payment.Status.StatusCode == PaymentStatusCodes.Success);

        var receiptSums = await successfulPayments
            .GroupBy(payment => payment.ContractId)
            .Select(group => new
            {
                GrossCollected = group.Sum(payment => (decimal?)payment.Amount) ?? 0m,
                GrossDeposit = group.Where(payment => payment.PaymentType == PaymentTypes.Deposit)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .SingleOrDefaultAsync(cancellationToken);

        var refundSums = await _db.Set<Uc18RefundConfirmation>()
            .Where(confirmation => confirmation.Operation.ContractId == contract.ContractId)
            .GroupBy(confirmation => confirmation.Operation.ContractId)
            .Select(group => new
            {
                PrincipalReturned = group.Sum(confirmation => (decimal?)confirmation.PrincipalPaid) ?? 0m,
                DepositPrincipalReturned = group.Sum(confirmation => (decimal?)confirmation.DepositPrincipalPaid) ?? 0m
            })
            .SingleOrDefaultAsync(cancellationToken);

        var operationSums = await _db.Set<Uc18BookingOperation>()
            .Where(operation => operation.ContractId == contract.ContractId &&
                (operation.Kind == Uc18BookingOperation.Reschedule ||
                 operation.Kind == Uc18BookingOperation.MenuChange))
            .GroupBy(operation => operation.ContractId)
            .Select(group => new
            {
                SignedPriceDelta = group.Sum(operation => (decimal?)operation.SignedPriceDelta) ?? 0m,
                FeeAmount = group.Sum(operation => (decimal?)operation.FeeAmount) ?? 0m,
                RealReschedules = group.Count(operation => operation.Kind == Uc18BookingOperation.Reschedule)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var hasPendingReceipts = await paymentGroup.AnyAsync(payment =>
            payment.Status.StatusCode == PaymentStatusCodes.Pending, cancellationToken);

        var receipts = new Uc18BookingReceipts(
            receiptSums?.GrossDeposit ?? 0m,
            refundSums?.DepositPrincipalReturned ?? 0m,
            receiptSums?.GrossCollected ?? 0m,
            refundSums?.PrincipalReturned ?? 0m);
        ValidateReceipts(receipts);

        var effectiveTotal = contract.TotalValue +
            (operationSums?.SignedPriceDelta ?? 0m) +
            (operationSums?.FeeAmount ?? 0m);
        ValidateMoney(effectiveTotal, nameof(effectiveTotal));

        return new Uc18FinancialLedgerSnapshot(
            receipts,
            effectiveTotal,
            operationSums?.RealReschedules ?? 0,
            hasPendingReceipts);
    }

    public static string? ValidateRefundConfirmation(
        Uc18BookingOperation operation,
        Uc18RefundConfirmation confirmation,
        decimal alreadyConfirmedPrincipal,
        decimal alreadyConfirmedDepositPrincipal,
        decimal alreadyConfirmedCompensation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(confirmation);

        if (!IsValidMoney(operation.PrincipalRefundDue) ||
            !IsValidMoney(operation.DepositPrincipalPortion) ||
            !IsValidMoney(operation.CompensationDue) ||
            operation.DepositPrincipalPortion > operation.PrincipalRefundDue)
            return "Stored refund caps are invalid.";

        if (!IsValidMoney(alreadyConfirmedPrincipal) ||
            !IsValidMoney(alreadyConfirmedDepositPrincipal) ||
            !IsValidMoney(alreadyConfirmedCompensation) ||
            alreadyConfirmedPrincipal > operation.PrincipalRefundDue ||
            alreadyConfirmedDepositPrincipal > operation.DepositPrincipalPortion ||
            alreadyConfirmedCompensation > operation.CompensationDue ||
            alreadyConfirmedDepositPrincipal > alreadyConfirmedPrincipal ||
            alreadyConfirmedPrincipal - alreadyConfirmedDepositPrincipal >
            operation.PrincipalRefundDue - operation.DepositPrincipalPortion)
            return "Previously confirmed amounts exceed or violate the stored refund caps.";

        if (!IsValidMoney(confirmation.PrincipalPaid) ||
            !IsValidMoney(confirmation.DepositPrincipalPaid) ||
            !IsValidMoney(confirmation.CompensationPaid))
            return "Confirmation amounts must be nonnegative whole VND with at most two decimal places.";
        if (confirmation.DepositPrincipalPaid > confirmation.PrincipalPaid)
            return "Deposit principal paid cannot exceed principal paid.";
        if (confirmation.PrincipalPaid == 0m && confirmation.CompensationPaid == 0m)
            return "A refund confirmation must record a positive amount.";

        if (confirmation.PrincipalPaid > operation.PrincipalRefundDue - alreadyConfirmedPrincipal ||
            confirmation.DepositPrincipalPaid > operation.DepositPrincipalPortion - alreadyConfirmedDepositPrincipal ||
            confirmation.PrincipalPaid - confirmation.DepositPrincipalPaid >
            operation.PrincipalRefundDue - operation.DepositPrincipalPortion -
            (alreadyConfirmedPrincipal - alreadyConfirmedDepositPrincipal) ||
            confirmation.CompensationPaid > operation.CompensationDue - alreadyConfirmedCompensation)
            return "Confirmation exceeds the remaining principal or compensation cap.";

        return null;
    }

    public static bool IsValidRefundMoney(decimal amount) => IsValidMoney(amount);

    private static void ValidateReceipts(Uc18BookingReceipts receipts)
    {
        if (!IsSqlMoney(receipts.GrossDeposit) ||
            !IsSqlMoney(receipts.DepositPrincipalReturned) ||
            !IsSqlMoney(receipts.GrossCollected) ||
            !IsSqlMoney(receipts.TotalPrincipalReturned))
            throw new InvalidOperationException("Ledger receipt totals must be nonnegative DECIMAL(18,2) amounts.");

        var netDeposit = receipts.GrossDeposit - receipts.DepositPrincipalReturned;
        var netCollected = receipts.GrossCollected - receipts.TotalPrincipalReturned;
        if (receipts.GrossDeposit > receipts.GrossCollected ||
            receipts.DepositPrincipalReturned > receipts.GrossDeposit ||
            receipts.TotalPrincipalReturned < receipts.DepositPrincipalReturned ||
            receipts.TotalPrincipalReturned > receipts.GrossCollected ||
            netDeposit > netCollected)
            throw new InvalidOperationException("Ledger receipts violate principal conservation.");
    }

    private static bool IsValidMoney(decimal value) =>
        value >= 0m && value <= MaxMoney && decimal.Round(value, 0) == value && decimal.Round(value, 2) == value;

    private static bool IsSqlMoney(decimal value) =>
        value >= 0m && value <= MaxMoney && decimal.Round(value, 2) == value;

    private static void ValidateMoney(decimal value, string parameterName)
    {
        if (value < 0m || value > MaxMoney || decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(parameterName, "Amount must fit DECIMAL(18,2) and be nonnegative.");
    }
}

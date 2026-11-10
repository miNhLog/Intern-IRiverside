using System.Collections.Concurrent;
using System.Data;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>
/// Serializes cancellation and payment finalization for the same booking in-process.
/// SQL Server deployments additionally take an update lock on DatTiec inside a
/// serializable transaction, so multiple application instances follow the same rule.
/// </summary>
internal static class BookingPaymentConcurrency
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> BookingLocks = new();

    public static async Task<IDisposable> AcquireAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var gate = BookingLocks.GetOrAdd(bookingId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    public static Task<IDbContextTransaction> BeginSerializableTransactionAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    public static async Task AcquireDatabaseLockAsync(
        ApplicationDbContext context,
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlServer())
            return;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [DatTiecID] FROM [dbo].[DatTiec] WITH (UPDLOCK, HOLDLOCK) WHERE [DatTiecID] = {bookingId}",
            cancellationToken);
    }

    public static async Task<bool> HasRecordedDepositAsync(
        ApplicationDbContext context,
        int bookingId,
        decimal bookingPaidAmount,
        CancellationToken cancellationToken = default)
    {
        if (bookingPaidAmount > 0)
            return true;

        if (await context.PaymentTransactions.AnyAsync(item =>
                item.DatTiecId == bookingId &&
                item.TransactionType == PaymentTransactionTypes.Deposit &&
                PaymentTransactionStatusCodes.PaymentReceived.Contains(item.TrangThai),
                cancellationToken))
            return true;

        return await context.Payments.AnyAsync(item =>
            item.Contract.BookingId == bookingId &&
            item.PaymentType == PaymentTypes.Deposit &&
            item.Status.StatusGroup == StatusGroups.Payment &&
            item.Status.StatusCode == PaymentStatusCodes.Success,
            cancellationToken);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}

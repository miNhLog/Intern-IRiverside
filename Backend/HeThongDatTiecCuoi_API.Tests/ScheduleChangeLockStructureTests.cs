using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ScheduleChangeLockStructureTests
{
    [Fact]
    public void AcceptQuote_UsesBookingThenScheduleLockAndRechecksBeforeAddingHold()
    {
        var source = File.ReadAllText(FindApiSource("Services", "ScheduleChangeService.cs"));
        var methodStart = source.IndexOf("public async Task<QuoteAcceptanceResultDto> AcceptQuoteAsync", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        var methodEnd = source.IndexOf("public async Task<ScheduleChangeDto> WithdrawAsync", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        var method = source[methodStart..methodEnd];

        var transaction = method.IndexOf("BeginSerializableAsync", StringComparison.Ordinal);
        var bookingLock = method.IndexOf("LockBookingAsync(booking.BookingId", StringComparison.Ordinal);
        var scheduleLock = method.IndexOf("LockHallScheduleAsync(change.NewHallScheduleId", StringComparison.Ordinal);
        var conflictCheck = method.IndexOf("EnsureTargetAvailableAsync(change, false", StringComparison.Ordinal);
        var holdInsert = method.IndexOf("var hold = new ScheduleChangeHold", StringComparison.Ordinal);
        Assert.True(transaction >= 0 && transaction < bookingLock);
        Assert.True(bookingLock < scheduleLock);
        Assert.True(scheduleLock < conflictCheck && conflictCheck < holdInsert);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK, ROWLOCK)", source);
        Assert.Contains("[dbo].[DatTiec]", source);
        Assert.Contains("[dbo].[LichSanh]", source);
    }

    [Fact]
    public void Uc15WriteBoundaries_CheckUc18FinancialHistoryUnderBookingLock()
    {
        var source = File.ReadAllText(FindApiSource("Services", "ScheduleChangeService.cs"));
        foreach (var name in new[]
                 {
                     "CreateCustomerRequestAsync", "CreateHallManagerRequestAsync",
                     "QuoteAndConditionallyApproveAsync", "AcceptQuoteAsync", "ReconcileAsync"
                 })
        {
            var start = source.LastIndexOf($"> {name}(", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing {name}");
            var end = source.IndexOf("\n    public async Task", start + name.Length, StringComparison.Ordinal);
            if (end < 0) end = source.Length;
            var method = source[start..end];
            Assert.Contains("LockBookingAsync", method);
            Assert.Contains("EnsureNoUc18FinancialHistoryAsync", method);
            Assert.Contains("EnsureNoOutstandingPayOsContractIntentAsync", method);
        }
        Assert.Contains("operation.Kind == Uc18BookingOperation.Reschedule", source);
        Assert.Contains("operation.Kind == Uc18BookingOperation.Cancel", source);
        Assert.Contains("PrincipalPaid > 0m || refund.CompensationPaid > 0m", source);
        Assert.Contains("transaction.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired", source);
    }

    [Fact]
    public void VerifiedPaymentAndFinalize_KeepPaymentOnReconciliationAndGuardBeforeApplyingBookingChange()
    {
        var source = File.ReadAllText(FindApiSource("Services", "ScheduleChangeService.cs"));
        var paymentStart = source.LastIndexOf("> ProcessVerifiedPaymentAsync(", StringComparison.Ordinal);
        var paymentEnd = source.IndexOf("\n    public async Task<ScheduleChangeDto> ReconcileAsync", paymentStart, StringComparison.Ordinal);
        var paymentMethod = source[paymentStart..paymentEnd];
        Assert.True(paymentMethod.IndexOf("LockBookingAsync", StringComparison.Ordinal) <
                    paymentMethod.IndexOf("tx.TrangThai = \"PAID\"", StringComparison.Ordinal));
        Assert.Contains("catch (ScheduleChangeException ex)", paymentMethod);
        Assert.Contains("MoveToReconciliationAsync(change, ex.Message", paymentMethod);

        var finalizeStart = source.IndexOf("private async Task FinalizeScheduleChangeAsync", StringComparison.Ordinal);
        var finalizeEnd = source.IndexOf("private async Task MoveToReconciliationAsync", finalizeStart, StringComparison.Ordinal);
        var finalizeMethod = source[finalizeStart..finalizeEnd];
        Assert.True(finalizeMethod.IndexOf("EnsureNoUc18FinancialHistoryAsync", StringComparison.Ordinal) <
                    finalizeMethod.IndexOf("booking.HallScheduleId = newSchedule.HallScheduleId", StringComparison.Ordinal));
        Assert.True(finalizeMethod.IndexOf("LockHallScheduleAsync", StringComparison.Ordinal) <
                    finalizeMethod.IndexOf("EnsureTargetAvailableAsync", StringComparison.Ordinal));
    }

    private static string FindApiSource(params string[] relativePath)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "HeThongDatTiecCuoi_API", Path.Combine(relativePath));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }
        throw new FileNotFoundException("Could not locate API source for structural lock assertion.");
    }
}

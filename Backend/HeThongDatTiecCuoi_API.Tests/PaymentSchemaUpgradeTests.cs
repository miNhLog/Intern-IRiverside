using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PaymentSchemaUpgradeTests
{
    [Fact]
    public async Task AdditiveScript_PreservesLegacyRecords_AndCanRunTwice()
    {
        // A separate random, owned LocalDB database, never the business connection string.
        var fixture = new PaymentSqlServerFixture();
        try
        {
            await fixture.InitializeAsync();
            var id = await fixture.Contract(null, 902);
            await using var db = fixture.Context();
            db.Payments.Add(new() { ContractId = id, Amount = 5m, PaymentType = "Đặt cọc", StatusId = 1002 });
            await db.SaveChangesAsync();
            // Reproduce the pre-UC17 shape only in this disposable database.
            await db.Database.ExecuteSqlRawAsync("DROP INDEX [UX_ThanhToan_RequestId] ON [ThanhToan]; ALTER TABLE [ThanhToan] DROP COLUMN [RequestId]; ALTER TABLE [HopDong] DROP COLUMN [SoTienCocCamKet];");
            var upgrade = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "20261021_UC17Payments.sql"));
            await db.Database.ExecuteSqlRawAsync(upgrade);
            await db.Database.ExecuteSqlRawAsync(upgrade);
            db.ChangeTracker.Clear();
            var contract = (await db.Contracts.FindAsync(id))!;
            Assert.Null(contract.RequiredDepositAmount);
            Assert.Equal(902, contract.StatusId);
            var payment = await db.Payments.SingleAsync(p => p.ContractId == id);
            Assert.Null(payment.RequestId);
            Assert.Equal(5m, payment.Amount);
            Assert.Equal(1002, payment.StatusId);
        }
        finally { await fixture.DisposeAsync(); }
    }
}

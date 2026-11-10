using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS;
using Xunit;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PayOSDepositConcurrencySqlServerTests
{
    [PayOsSqlServerFact]
    public async Task ConcurrentDifferentOrderCodes_OnSqlServer_RecordExactlyOneDepositWithoutDeadlock()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        await SeedAsync(database.Options, includeSecondTransaction: true);

        await using var firstDb = new ApplicationDbContext(database.Options);
        await using var secondDb = new ApplicationDbContext(database.Options);
        var work = Task.WhenAll(
            CreateService(firstDb).ProcessConfirmedDepositAsync(910001, 10_000, "SQL webhook X"),
            CreateService(secondDb).ProcessConfirmedDepositAsync(910002, 10_000, "SQL webhook Y"));
        await work.WaitAsync(TimeSpan.FromSeconds(30));

        await using var assertDb = new ApplicationDbContext(database.Options);
        var booking = await assertDb.WeddingBookings.SingleAsync();
        var transactions = await assertDb.PaymentTransactions.OrderBy(item => item.OrderCode).ToListAsync();
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Single(transactions, item => item.TrangThai == PaymentTransactionStatusCodes.Paid);
        var duplicate = Assert.Single(transactions,
            item => item.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired);
        Assert.Contains(PaymentReconciliationReasons.DuplicateDepositPayment, duplicate.ReconciliationData);
        Assert.Single(await assertDb.Contracts.ToListAsync());
        Assert.Single(await assertDb.Payments.Where(item => item.PaymentType == PaymentTypes.Deposit).ToListAsync());
        Assert.Equal(2, await assertDb.AuditLogs.CountAsync());
    }

    [PayOsSqlServerFact]
    public async Task FinalizationFailure_OnSqlServer_RollsBackBookingTransactionPaymentAndAudit()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        await SeedAsync(database.Options, includeSecondTransaction: false);

        await using (var failingDb = new ApplicationDbContext(database.Options))
        {
            var service = CreateService(failingDb, new FailingPaymentStatusService());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ProcessConfirmedDepositAsync(910001, 10_000, "SQL rollback webhook"));
        }

        await using var assertDb = new ApplicationDbContext(database.Options);
        var booking = await assertDb.WeddingBookings.SingleAsync();
        var transaction = await assertDb.PaymentTransactions.SingleAsync();
        Assert.Equal(0, booking.PaidAmount);
        Assert.Equal(101, booking.StatusId);
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Empty(await assertDb.Contracts.ToListAsync());
        Assert.Empty(await assertDb.Payments.ToListAsync());
        Assert.Empty(await assertDb.AuditLogs.ToListAsync());
    }

    private static PayOSPaymentService CreateService(
        ApplicationDbContext db,
        IStatusService? statuses = null) =>
        new(
            db,
            new PayOSClient("test-client", "test-key", "test-checksum"),
            Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions
            {
                IsDemoMode = true,
                DemoScaleRatio = 100
            }),
            statuses ?? new SqlStatusService(),
            null!, null!, null!, null!,
            NullLogger<PayOSPaymentService>.Instance);

    private static async Task SeedAsync(
        DbContextOptions<ApplicationDbContext> options,
        bool includeSecondTransaction)
    {
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Statuses.AddRange(
            new Status { StatusId = 101, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" },
            new Status { StatusId = 102, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" },
            new Status { StatusId = 201, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" },
            new Status { StatusId = 202, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new Status { StatusId = 301, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Nháp" },
            new Status { StatusId = 401, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" });
        db.DataStatuses.Add(new DataStatus
        {
            DataStatusId = 1,
            DataStatusCode = DataStatusCodes.Existing,
            DataStatusName = "Tồn tại"
        });
        var hall = new Hall
        {
            HallCode = "SQL-H01", HallName = "SQL Test Hall", MinimumCapacity = 50,
            MaximumCapacity = 500, RentalPrice = 20_000_000, StatusId = 201, DataStatusId = 1
        };
        var schedule = new HallSchedule
        {
            Hall = hall, Date = DateTime.Today.AddDays(30), Shift = "Ca tối",
            StatusId = 202, DataStatusId = 1
        };
        var customer = new Customer
        {
            FullName = "SQL Test Customer", PhoneNumber = "0901234567", DataStatusId = 1
        };
        var booking = new WeddingBooking
        {
            BookingCode = "DT-SQL-PAYOS",
            Customer = customer,
            HallSchedule = schedule,
            StatusId = 101,
            GuestCount = 200,
            EstimatedTotal = 10_000_000,
            PaidAmount = 0,
            BookedAt = DateTime.Now
        };
        db.WeddingBookings.Add(booking);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Booking = booking,
            TransactionType = PaymentTransactionTypes.Deposit,
            OrderCode = 910001,
            SoTienGiaoDich = 10_000,
            SoTienGhiNhan = 1_000_000,
            TrangThai = PaymentTransactionStatusCodes.Pending
        });
        if (includeSecondTransaction)
            db.PaymentTransactions.Add(new PaymentTransaction
            {
                Booking = booking,
                TransactionType = PaymentTransactionTypes.Deposit,
                OrderCode = 910002,
                SoTienGiaoDich = 10_000,
                SoTienGhiNhan = 1_000_000,
                TrangThai = PaymentTransactionStatusCodes.Pending
            });
        await db.SaveChangesAsync();
    }

    private class SqlStatusService : IStatusService
    {
        public virtual Task<int> GetStatusIdAsync(
            string statusGroup,
            string statusCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((statusGroup, statusCode) switch
            {
                (StatusGroups.Booking, BookingStatusCodes.Confirmed) => 102,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked) => 202,
                (StatusGroups.Contract, ContractStatusCodes.Draft) => 301,
                (StatusGroups.Payment, PaymentStatusCodes.Success) => 401,
                _ => throw new InvalidOperationException($"Unknown test status {statusGroup}/{statusCode}")
            });

        public Task<Status?> GetStatusAsync(
            string statusGroup,
            string statusCode,
            CancellationToken cancellationToken = default) => Task.FromResult<Status?>(null);
    }

    private sealed class FailingPaymentStatusService : SqlStatusService
    {
        public override Task<int> GetStatusIdAsync(
            string statusGroup,
            string statusCode,
            CancellationToken cancellationToken = default)
        {
            if (statusGroup == StatusGroups.Payment && statusCode == PaymentStatusCodes.Success)
                throw new InvalidOperationException("Injected failure after booking and contract updates");
            return base.GetStatusIdAsync(statusGroup, statusCode, cancellationToken);
        }
    }

    private sealed class PayOsSqlServerFactAttribute : FactAttribute
    {
        public PayOsSqlServerFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PAYOS_SQL_INTEGRATION"), "1", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYOS_TEST_SQL_SERVER")))
                Skip = "Set PAYOS_SQL_INTEGRATION=1 and PAYOS_TEST_SQL_SERVER to run against an isolated non-production SQL Server.";
        }
    }

    private sealed class SqlTestDatabase : IAsyncDisposable
    {
        private readonly SqlConnection _masterConnection;
        private readonly string _databaseName;
        public DbContextOptions<ApplicationDbContext> Options { get; }

        private SqlTestDatabase(SqlConnection masterConnection, string databaseName, string connectionString)
        {
            _masterConnection = masterConnection;
            _databaseName = databaseName;
            Options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString, sql => sql.CommandTimeout(20))
                .Options;
        }

        public static async Task<SqlTestDatabase> CreateAsync()
        {
            var server = Environment.GetEnvironmentVariable("PAYOS_TEST_SQL_SERVER")!;
            var databaseName = $"RiversidePayOSTests_{Guid.NewGuid():N}";
            var masterString = $"Server={server};Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
            var testString = $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";
            var master = new SqlConnection(masterString);
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandTimeout = 30;
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
            return new SqlTestDatabase(master, databaseName, testString);
        }

        public async ValueTask DisposeAsync()
        {
            await using var drop = _masterConnection.CreateCommand();
            drop.CommandTimeout = 30;
            drop.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await drop.ExecuteNonQueryAsync();
            await _masterConnection.DisposeAsync();
        }
    }
}

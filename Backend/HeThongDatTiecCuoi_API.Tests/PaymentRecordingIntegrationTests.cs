using System.Security.Claims;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PaymentSqlServerFixture : IAsyncLifetime
{
    private readonly string _database = $"UC17_Test_{Guid.NewGuid():N}";
    public int ActorId { get; private set; }
    private bool _owned;
    public ApplicationDbContext Context(params IInterceptor[] interceptors)
    {
        if (!_database.StartsWith("UC17_Test_", StringComparison.Ordinal) || _database.Length != 42)
            throw new InvalidOperationException("Unsafe test database name.");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = _database,
            IntegratedSecurity = true, TrustServerCertificate = true
        };
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).AddInterceptors(interceptors).Options);
    }
    public async Task InitializeAsync()
    {
        await using var db = Context();
        _owned = await db.Database.EnsureCreatedAsync();
        if (!_owned) throw new InvalidOperationException("Refuse to use an existing database.");
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = "EXISTING", DataStatusName = "Test" });
        var statuses = new[] { (101,"ACCOUNT","ACTIVE"), (201,"HALL","ACTIVE"), (301,"HALL_SCHEDULE","BOOKED"),
            (802,"BOOKING","CONFIRMED"), (901,"CONTRACT","DRAFT"), (902,"CONTRACT","ACTIVE"),
            (903,"CONTRACT","COMPLETED"), (904,"CONTRACT","CANCELLED"), (1001,"PAYMENT","PENDING"), (1002,"PAYMENT","SUCCESS"), (1003,"PAYMENT","FAILED") };
        foreach (var (id, group, code) in statuses) db.Statuses.Add(new Status { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code });
        var role = new Role { RoleId = 2, RoleName = RoleNames.Manager };
        var actor = new User { Role = role, Email = "manager@uc17.invalid", PasswordHash = "not-a-real-login", StatusId = 101, DataStatusId = 1 };
        db.Users.Add(actor); await db.SaveChangesAsync(); ActorId = actor.UserId;
    }
    public async Task DisposeAsync()
    {
        if (!_owned) return;
        await using var db = Context();
        await db.Database.EnsureDeletedAsync(); // Only the random database created by this fixture.
    }
    public async Task<int> Contract(decimal? commitment = 30m, int status = 901)
    {
        await using var db = Context();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var contract = new Contract
        {
            ContractCode = $"HD-{suffix}", TotalValue = 100m, RequiredDepositAmount = commitment,
            ContractContent = "Synthetic contract", PaymentTerms = "Synthetic terms", StatusId = status,
            Booking = new WeddingBooking
            {
                BookingCode = $"DT-{suffix}", GuestCount = 10, StatusId = 802,
                Customer = new Customer { CustomerCode = $"KH-{suffix}", FullName = "Synthetic customer", PhoneNumber = "0900000000", DataStatusId = 1 },
                HallSchedule = new HallSchedule { Date = DateTime.Today.AddDays(30), Shift = "Ca trưa", DataStatusId = 1, StatusId = 301,
                    Hall = new Hall { HallCode = $"S-{suffix}", HallName = "Synthetic hall", MaximumCapacity = 100, RentalPrice = 1, StatusId = 201, DataStatusId = 1 } }
            }
        };
        db.Contracts.Add(contract); await db.SaveChangesAsync(); return contract.ContractId;
    }
    public T AsActor<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()), new Claim(ClaimTypes.Role, RoleNames.Manager)], "Test")) } };
        return controller;
    }
    public ManagerContractController Contracts(ApplicationDbContext db) =>
        AsActor(new ManagerContractController(db, new StatusService(db),
            new ContractTemplateProvider(new PaymentPolicyProvider()), null!, null!));
    public async Task<IActionResult> Pay(RecordPaymentRequestDto request)
    {
        await using var db = Context();
        return await AsActor(new ManagerPaymentController(db)).RecordPayment(request, default);
    }
    public static RecordPaymentRequestDto Request(int id, decimal amount, string type = "Đặt cọc") => new()
    { ContractId = id, Amount = amount, PaymentType = type, PaymentMethod = "Tiền mặt", RequestId = Guid.NewGuid() };
}

public sealed class PaymentRecordingIntegrationTests : IClassFixture<PaymentSqlServerFixture>
{
    private readonly PaymentSqlServerFixture _fixture;
    public PaymentRecordingIntegrationTests(PaymentSqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PartialDeposits_ActivateAtCommitment_ReplayAndDebtPersist()
    {
        var id = await _fixture.Contract();
        var first = PaymentSqlServerFixture.Request(id, 10m);
        Assert.IsType<OkObjectResult>(await _fixture.Pay(first));
        await using (var db = _fixture.Context()) Assert.Equal(901, (await db.Contracts.FindAsync(id))!.StatusId);
        Assert.IsType<OkObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 20m)));
        Assert.IsType<OkObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 40m, "Thanh toán đợt")));
        Assert.IsType<OkObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 30m, "Quyết toán")));
        await using var verify = _fixture.Context();
        Assert.Equal(902, (await verify.Contracts.FindAsync(id))!.StatusId); // Never auto-complete.
        Assert.Equal(100m, await verify.Payments.Where(p => p.ContractId == id).SumAsync(p => p.Amount));
        var detail = Assert.IsType<OkObjectResult>((await _fixture.Contracts(verify).GetContract(id, default)).Result);
        var dto = Assert.IsType<ManagerContractDetailDto>(detail.Value);
        Assert.Equal(0m, dto.RemainingBalance);
        Assert.Equal(30m, dto.DepositPaidAmount);
        Assert.Equal(4, dto.Payments.Count);
        var audits = await verify.AuditLogs.CountAsync();
        var replay = Assert.IsType<OkObjectResult>(await _fixture.Pay(first));
        Assert.True(Assert.IsType<PaymentActionResponseDto>(replay.Value).Replayed);
        first.Amount = 11m;
        Assert.IsType<ConflictObjectResult>(await _fixture.Pay(first));
        Assert.Equal(audits, await verify.AuditLogs.CountAsync());
        Assert.IsType<ConflictObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 1m)));
    }

    [Fact]
    public async Task ConcurrentPayments_CannotOverpay()
    {
        var id = await _fixture.Contract(10m, 902);
        var results = await Task.WhenAll(_fixture.Pay(PaymentSqlServerFixture.Request(id, 70m)), _fixture.Pay(PaymentSqlServerFixture.Request(id, 70m)));
        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());
        await using var db = _fixture.Context();
        Assert.Equal(70m, await db.Payments.Where(p => p.ContractId == id).SumAsync(p => p.Amount));
    }

    [Fact]
    public async Task ConcurrentSameIntent_CreatesOnePayment()
    {
        var id = await _fixture.Contract(10m, 902);
        var intent = PaymentSqlServerFixture.Request(id, 10m);
        var results = await Task.WhenAll(_fixture.Pay(intent), _fixture.Pay(intent));
        Assert.All(results, result => Assert.IsType<OkObjectResult>(result));
        await using var db = _fixture.Context();
        Assert.Equal(1, await db.Payments.CountAsync(p => p.ContractId == id));
        var paymentId = await db.Payments.Where(p => p.ContractId == id).Select(p => p.PaymentId).SingleAsync();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityName == "Payment" && a.EntityId == paymentId));
    }

    [Fact]
    public async Task AuditFailure_RollsBackPaymentAndActivation()
    {
        var id = await _fixture.Contract();
        await using (var db = _fixture.Context(new RejectAudit()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => _fixture.AsActor(new ManagerPaymentController(db))
                .RecordPayment(PaymentSqlServerFixture.Request(id, 30m), default));
        await using var verify = _fixture.Context();
        Assert.Equal(901, (await verify.Contracts.FindAsync(id))!.StatusId);
        Assert.False(await verify.Payments.AnyAsync(p => p.ContractId == id));
    }

    [Fact]
    public async Task LegacyConfirmation_IsOneTimeAndDoesNotRevertActive()
    {
        var id = await _fixture.Contract(null, 902);
        Assert.IsType<ConflictObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 10m)));
        await using (var db = _fixture.Context())
            Assert.IsType<OkObjectResult>((await _fixture.Contracts(db).ConfirmDeposit(id, new() { RequiredDepositAmount = 30m }, default)).Result);
        await using (var db = _fixture.Context())
            Assert.IsType<ConflictObjectResult>((await _fixture.Contracts(db).ConfirmDeposit(id, new() { RequiredDepositAmount = 40m }, default)).Result);
        await using var verify = _fixture.Context();
        Assert.Equal(902, (await verify.Contracts.FindAsync(id))!.StatusId);
        Assert.Equal(30m, (await verify.Contracts.FindAsync(id))!.RequiredDepositAmount);
        Assert.Equal(1, await verify.AuditLogs.CountAsync(a => a.EntityName == "Contract" && a.EntityId == id && a.Action == "DEPOSIT_COMMITMENT_CONFIRMED"));
    }

    [Fact]
    public async Task DraftTermsRemainEditable_ButMoneyCannotChangeAfterPartialDeposit()
    {
        var id = await _fixture.Contract();
        Assert.IsType<OkObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 10m)));
        await using (var db = _fixture.Context())
            Assert.IsType<ConflictObjectResult>((await _fixture.Contracts(db).UpdateDraft(id,
                new() { TotalValue = 200m, RequiredDepositAmount = 30m }, default)).Result);
        await using (var db = _fixture.Context())
            Assert.IsType<OkObjectResult>((await _fixture.Contracts(db).UpdateDraft(id,
                new() { TotalValue = 100m, ContractContent = "Corrected text", PaymentTerms = "Verified terms" }, default)).Result);
        await using var verify = _fixture.Context();
        var contract = (await verify.Contracts.FindAsync(id))!;
        Assert.Equal(30m, contract.RequiredDepositAmount); // Old clients omitting the additive field preserve it.
        Assert.Equal("Corrected text", contract.ContractContent);
        Assert.Equal(100m, contract.TotalValue);
    }

    [Fact]
    public async Task ManualActivation_CannotBypassDeposit_AndNonSuccessDoesNotCount()
    {
        var id = await _fixture.Contract();
        await using (var db = _fixture.Context())
        {
            db.Payments.Add(new Payment { ContractId = id, PaymentType = "Đặt cọc", Amount = 90m, StatusId = 1001 });
            await db.SaveChangesAsync();
            Assert.IsType<ConflictObjectResult>((await _fixture.Contracts(db).Activate(id, default)).Result);
        }
        Assert.IsType<OkObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 10m)));
        await using var verify = _fixture.Context();
        var result = Assert.IsType<OkObjectResult>((await _fixture.Contracts(verify).GetContract(id, default)).Result);
        var detail = Assert.IsType<ManagerContractDetailDto>(result.Value);
        Assert.Equal(10m, detail.PaidAmount);
        Assert.Equal(90m, detail.RemainingBalance);
        Assert.Equal("DRAFT", detail.StatusCode);
    }

    [Theory]
    [InlineData(903)]
    [InlineData(904)]
    public async Task ClosedContracts_DoNotAcceptNewMoney(int status)
    {
        var id = await _fixture.Contract(30m, status);
        Assert.IsType<ConflictObjectResult>(await _fixture.Pay(PaymentSqlServerFixture.Request(id, 10m)));
        await using var verify = _fixture.Context();
        Assert.False(await verify.Payments.AnyAsync(p => p.ContractId == id));
    }

    [Fact]
    public async Task ConcurrentDifferentIntents_CanExactlyPayBalance()
    {
        var id = await _fixture.Contract(10m, 902);
        var results = await Task.WhenAll(_fixture.Pay(PaymentSqlServerFixture.Request(id, 50m)), _fixture.Pay(PaymentSqlServerFixture.Request(id, 50m)));
        Assert.All(results, result => Assert.IsType<OkObjectResult>(result));
        await using var verify = _fixture.Context();
        Assert.Equal(100m, await verify.Payments.Where(p => p.ContractId == id).SumAsync(p => p.Amount));
    }

    [Fact]
    public async Task ConcurrentConfirmations_OnlyOneCanSetLegacyCommitment()
    {
        var id = await _fixture.Contract(null, 902);
        async Task<IActionResult?> Confirm(decimal amount)
        {
            await using var db = _fixture.Context();
            return (await _fixture.Contracts(db).ConfirmDeposit(id, new() { RequiredDepositAmount = amount }, default)).Result;
        }
        var results = await Task.WhenAll(Confirm(20m), Confirm(30m));
        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());
    }

    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Synthetic audit write failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using Xunit;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PayOSContractCompatibilityTests
{
    [Fact]
    public async Task ContractReservationIsCommittedBeforeGatewayAndDuplicateIsRejected()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var gateway = new TestGateway(async request =>
        {
            await using var read = new ApplicationDbContext(options);
            var transaction = await read.PaymentTransactions.SingleAsync(item => item.OrderCode == request.OrderCode);
            var payment = await read.Payments.SingleAsync(item => item.TransactionCode == request.OrderCode.ToString());
            Assert.Equal(PaymentTransactionStatusCodes.Creating, transaction.TrangThai);
            Assert.Equal(PaymentStatusCodes.Pending, (await read.Statuses.SingleAsync(item => item.StatusId == payment.StatusId)).StatusCode);
            return Link(request, PaymentLinkStatus.Pending);
        });
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);

        await service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });

        Assert.Equal(PaymentTransactionStatusCodes.Pending, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(1, gateway.CreateCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        Assert.Equal(1, gateway.CreateCalls);
    }

    [Theory]
    [InlineData(false, PaymentTransactionStatusCodes.Failed, PaymentStatusCodes.Failed)]
    [InlineData(true, PaymentTransactionStatusCodes.ReconciliationRequired, PaymentStatusCodes.Pending)]
    public async Task LinkFailureDistinguishesDefinitiveFromAmbiguous(bool ambiguous, string txStatus, string paymentStatus)
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var gateway = new TestGateway(_ => Task.FromException<CreatePaymentLinkResponse>(ambiguous
            ? new HttpRequestException("uncertain transport result")
            : new ArgumentException("definite rejection")));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));

        Assert.Equal(txStatus, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        var payment = await db.Payments.Include(item => item.Status).SingleAsync();
        Assert.Equal(paymentStatus, payment.Status.StatusCode);
        if (ambiguous)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
            { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
            Assert.Equal(1, gateway.CreateCalls);
        }
    }

    [Fact]
    public async Task PollingReplayRecordsReceiptOnlyOnceAndOverCapOrStaleContractRequiresReconciliation()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var gateway = new TestGateway(request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)), _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid)));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);
        var created = await service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });

        await service.CheckContractPaymentStatusAsync(created.OrderCode);
        await service.CheckContractPaymentStatusAsync(created.OrderCode);
        Assert.Equal(PaymentTransactionStatusCodes.Paid, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(100_000m, (await db.Payments.SingleAsync(item => item.TransactionCode == created.OrderCode.ToString())).Amount);
        Assert.Equal(100_000m, (await db.WeddingBookings.SingleAsync()).PaidAmount);

        var overCapOptions = CreateOptions();
        await SeedAsync(overCapOptions, contractTotal: 100_000m);
        await using var overCapDb = new ApplicationDbContext(overCapOptions);
        var overCapGateway = new TestGateway(request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)), _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid)));
        var overCapService = CreateService(overCapDb, overCapGateway);
        var overCap = await overCapService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });
        overCapDb.Payments.Add(new Payment { ContractId = 1, PaymentType = PaymentTypes.Deposit, Amount = 1_000m, PaymentDate = DateTime.Now, TransactionCode = "prior", StatusId = 5 });
        await overCapDb.SaveChangesAsync();
        await overCapService.CheckContractPaymentStatusAsync(overCap.OrderCode);
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, (await overCapDb.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(0m, (await overCapDb.WeddingBookings.SingleAsync()).PaidAmount);

        var staleOptions = CreateOptions();
        await SeedAsync(staleOptions);
        await using var staleDb = new ApplicationDbContext(staleOptions);
        var staleService = CreateService(staleDb, new TestGateway(request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)), _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid))));
        var stale = await staleService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });
        var contract = await staleDb.Contracts.SingleAsync();
        contract.StatusId = 3;
        await staleDb.SaveChangesAsync();
        await staleService.CheckContractPaymentStatusAsync(stale.OrderCode);
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, (await staleDb.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(0m, (await staleDb.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Equal(PaymentStatusCodes.Pending, (await staleDb.Payments.Include(item => item.Status).SingleAsync()).Status.StatusCode);
    }

    [Fact]
    public async Task AmbiguousCreateRecoversToPaidViaPollingWithExactReplayRecordingReceiptOnce()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var gateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid)));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.AmbiguousContractCreate, transaction.ReconciliationData);
        Assert.Equal(PaymentStatusCodes.Pending, (await db.Payments.Include(item => item.Status).SingleAsync()).Status.StatusCode);

        await service.CheckContractPaymentStatusAsync(transaction.OrderCode);
        await service.CheckContractPaymentStatusAsync(transaction.OrderCode);

        transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Paid, transaction.TrangThai);
        Assert.NotNull(transaction.NgayThanhToan);
        var payment = await db.Payments.Include(item => item.Status).SingleAsync(item => item.TransactionCode == transaction.OrderCode.ToString());
        Assert.Equal(PaymentStatusCodes.Success, payment.Status.StatusCode);
        Assert.Equal(100_000m, payment.Amount);
        Assert.Equal(100_000m, (await db.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal(1, gateway.GetCalls);
    }

    [Fact]
    public async Task AmbiguousCreateRecoversToPaidViaVerifiedWebhookAndDuplicateWebhookIsIgnored()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var gateway = new TestGateway(_ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        var orderCode = (await db.PaymentTransactions.SingleAsync()).OrderCode;

        var client = new PayOSClient("test", "test", "test");
        var tampered = BuildWebhook(client, orderCode, 100_000);
        tampered.Signature = "invalid-signature";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ProcessWebhookAsync(tampered));
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, (await db.PaymentTransactions.SingleAsync()).TrangThai);

        var webhook = BuildWebhook(client, orderCode, 100_000);
        Assert.True(await service.ProcessWebhookAsync(webhook));

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Paid, transaction.TrangThai);
        var payment = await db.Payments.Include(item => item.Status).SingleAsync(item => item.TransactionCode == orderCode.ToString());
        Assert.Equal(PaymentStatusCodes.Success, payment.Status.StatusCode);
        Assert.Equal(100_000m, payment.Amount);
        Assert.Equal(100_000m, (await db.WeddingBookings.SingleAsync()).PaidAmount);

        Assert.True(await service.ProcessWebhookAsync(webhook));
        Assert.Equal(PaymentTransactionStatusCodes.Paid, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(100_000m, (await db.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Single(await db.Payments.Where(item => item.TransactionCode == orderCode.ToString()).ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task AmbiguousCreatePaidForStaleContractOrOverCapStaysReconciledAndIsNeverPolledAgain()
    {
        var staleOptions = CreateOptions();
        await SeedAsync(staleOptions);
        var staleGateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid)));
        await using var staleDb = new ApplicationDbContext(staleOptions);
        var staleService = CreateService(staleDb, staleGateway);
        await Assert.ThrowsAsync<InvalidOperationException>(() => staleService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        var staleTransaction = await staleDb.PaymentTransactions.SingleAsync();
        (await staleDb.Contracts.SingleAsync()).StatusId = 3;
        await staleDb.SaveChangesAsync();

        await staleService.CheckContractPaymentStatusAsync(staleTransaction.OrderCode);

        staleTransaction = await staleDb.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, staleTransaction.TrangThai);
        Assert.DoesNotContain(PaymentReconciliationReasons.AmbiguousContractCreate, staleTransaction.ReconciliationData);
        Assert.Contains("CONTRACT_NOT_ACTIVE", staleTransaction.ReconciliationData);
        Assert.Equal(0m, (await staleDb.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Equal(PaymentStatusCodes.Pending, (await staleDb.Payments.Include(item => item.Status).SingleAsync()).Status.StatusCode);

        await staleService.CheckContractPaymentStatusAsync(staleTransaction.OrderCode);
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, (await staleDb.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(1, staleGateway.GetCalls);

        var overCapOptions = CreateOptions();
        await SeedAsync(overCapOptions, contractTotal: 100_000m);
        var overCapGateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Paid)));
        await using var overCapDb = new ApplicationDbContext(overCapOptions);
        var overCapService = CreateService(overCapDb, overCapGateway);
        await Assert.ThrowsAsync<InvalidOperationException>(() => overCapService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        overCapDb.Payments.Add(new Payment { ContractId = 1, PaymentType = PaymentTypes.Deposit, Amount = 1_000m, PaymentDate = DateTime.Now, TransactionCode = "prior", StatusId = 5 });
        await overCapDb.SaveChangesAsync();
        var overCapOrderCode = (await overCapDb.PaymentTransactions.SingleAsync()).OrderCode;

        await overCapService.CheckContractPaymentStatusAsync(overCapOrderCode);

        var overCapTransaction = await overCapDb.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, overCapTransaction.TrangThai);
        Assert.DoesNotContain(PaymentReconciliationReasons.AmbiguousContractCreate, overCapTransaction.ReconciliationData);
        Assert.Contains("PAYMENT_EXCEEDS_EFFECTIVE_DUE_OR_RESERVATION_MISMATCH", overCapTransaction.ReconciliationData);
        Assert.Equal(0m, (await overCapDb.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Equal(PaymentStatusCodes.Pending, (await overCapDb.Payments.Include(item => item.Status).SingleAsync(item => item.TransactionCode == overCapOrderCode.ToString())).Status.StatusCode);

        await overCapService.CheckContractPaymentStatusAsync(overCapOrderCode);
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, (await overCapDb.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(1, overCapGateway.GetCalls);
    }

    [Fact]
    public async Task AmbiguousCreateTerminalNoReceiptReleasesReservationAndAllowsReplacement()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var ambiguousGateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Expired)));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, ambiguousGateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        var releasedOrderCode = (await db.PaymentTransactions.SingleAsync()).OrderCode;

        await service.CheckContractPaymentStatusAsync(releasedOrderCode);

        var released = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Cancelled, released.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PayOsLinkTerminalNoReceipt, released.ReconciliationData);
        var releasedPayment = await db.Payments.Include(item => item.Status)
            .SingleAsync(item => item.TransactionCode == releasedOrderCode.ToString());
        Assert.Equal(PaymentStatusCodes.Cancelled, releasedPayment.Status.StatusCode);

        var replacementGateway = new TestGateway(request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)));
        var replacementService = CreateService(db, replacementGateway);
        var replacement = await replacementService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });

        Assert.NotEqual(releasedOrderCode, replacement.OrderCode);
        Assert.Equal(2, await db.PaymentTransactions.CountAsync());
        Assert.Equal(PaymentTransactionStatusCodes.Pending,
            (await db.PaymentTransactions.SingleAsync(item => item.OrderCode == replacement.OrderCode)).TrangThai);
    }

    [Fact]
    public async Task ContractTerminalPollRequiresMatchingOrderZeroPaidAndNoProviderTransactions()
    {
        async Task AssertStillReservedAsync(Func<long, PaymentLink> providerLink)
        {
            var options = CreateOptions();
            await SeedAsync(options);
            var gateway = new TestGateway(
                _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
                paymentLink: orderCode => Task.FromResult(providerLink(orderCode)));
            await using var db = new ApplicationDbContext(options);
            var service = CreateService(db, gateway);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
            { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
            var orderCode = (await db.PaymentTransactions.SingleAsync()).OrderCode;
            await service.GetPaymentStatusAsync(orderCode);

            Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired,
                (await db.PaymentTransactions.SingleAsync()).TrangThai);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
            { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        }

        await AssertStillReservedAsync(orderCode => new PaymentLink
        {
            OrderCode = orderCode + 1, Amount = 100_000, AmountPaid = 0, Status = PaymentLinkStatus.Expired
        });
        await AssertStillReservedAsync(orderCode => new PaymentLink
        {
            OrderCode = orderCode, Amount = 100_000, AmountPaid = 1, Status = PaymentLinkStatus.Expired
        });
        await AssertStillReservedAsync(orderCode => new PaymentLink
        {
            OrderCode = orderCode, Amount = 100_000, AmountPaid = 0, Status = PaymentLinkStatus.Expired,
            Transactions = [new PayOS.Models.V2.PaymentRequests.PaymentTransaction { Amount = 1 }]
        });
    }

    [Fact]
    public async Task StaleTerminalPollCannotOverwriteConcurrentPaidContractTransaction()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var providerReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProviderRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            paymentLink: async orderCode =>
            {
                providerReadStarted.TrySetResult();
                await releaseProviderRead.Task;
                return new PaymentLink { OrderCode = orderCode, Amount = 100_000, AmountPaid = 0, Status = PaymentLinkStatus.Expired };
            });
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        var orderCode = (await db.PaymentTransactions.SingleAsync()).OrderCode;
        var poll = service.CheckContractPaymentStatusAsync(orderCode);
        await providerReadStarted.Task;

        await using (var writer = new ApplicationDbContext(options))
        {
            (await writer.PaymentTransactions.SingleAsync(item => item.OrderCode == orderCode)).TrangThai = PaymentTransactionStatusCodes.Paid;
            await writer.SaveChangesAsync();
        }
        releaseProviderRead.TrySetResult();
        await poll;

        Assert.Equal(PaymentTransactionStatusCodes.Paid,
            (await db.PaymentTransactions.SingleAsync(item => item.OrderCode == orderCode)).TrangThai);
    }

    [Fact]
    public async Task LatePaidWebhookAfterTerminalReleaseIsPreservedForReconciliation()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var ambiguousGateway = new TestGateway(
            _ => Task.FromException<CreatePaymentLinkResponse>(new HttpRequestException("uncertain transport result")),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Cancelled)));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, ambiguousGateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));
        var orderCode = (await db.PaymentTransactions.SingleAsync()).OrderCode;
        await service.CheckContractPaymentStatusAsync(orderCode);
        Assert.Equal(PaymentTransactionStatusCodes.Cancelled, (await db.PaymentTransactions.SingleAsync()).TrangThai);

        var client = new PayOSClient("test", "test", "test");
        Assert.True(await service.ProcessWebhookAsync(BuildWebhook(client, orderCode, 100_000)));

        var reconciled = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, reconciled.TrangThai);
        Assert.Contains("PAYMENT_ARRIVED_AFTER_RESERVATION_RELEASED", reconciled.ReconciliationData);
        Assert.NotNull(reconciled.NgayThanhToan);
        Assert.Equal(0m, (await db.WeddingBookings.SingleAsync()).PaidAmount);
    }

    [Fact]
    public async Task ExpiredContractIntentStaysReservedUntilProviderConfirmsTerminalNoReceipt()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        var createGateway = new TestGateway(request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)));
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db, createGateway);
        await service.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment });

        var intent = await db.PaymentTransactions.SingleAsync();
        intent.ExpiresAt = DateTime.Now.AddMinutes(-1);
        await db.SaveChangesAsync();

        var replacementGateway = new TestGateway(
            request => Task.FromResult(Link(request, PaymentLinkStatus.Pending)),
            _ => Task.FromResult(Link(null, PaymentLinkStatus.Pending)));
        var replacementService = CreateService(db, replacementGateway);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => replacementService.CreateContractPaymentAsync(new CreateContractPayOsLinkRequestDto
        { ContractId = 1, Amount = 100_000, PaymentType = PaymentTypes.Installment }));

        Assert.Contains("Không thể tạo thêm link thanh toán", error.Message);
        Assert.Equal(PaymentTransactionStatusCodes.Pending, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Equal(0, replacementGateway.CreateCalls);
    }

    [Fact]
    public async Task CashUsesAmendedRefundAdjustedLedgerAndUc18RejectsActivePayOsLink()
    {
        var options = CreateOptions();
        await SeedAsync(options, contractTotal: 1_000_000m, seedLedger: true);
        await using var db = new ApplicationDbContext(options);
        var controller = new ManagerPaymentController(db, new TestStatuses(), null!, NullLogger<ManagerPaymentController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        var result = await controller.RecordCashPayment(new RecordCashPaymentRequestDto
        { ContractId = 1, Amount = 900_000m, PaymentType = PaymentTypes.Settlement, Note = "ledger" }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal(1_100_000m, (await db.WeddingBookings.SingleAsync()).PaidAmount);

        db.PaymentTransactions.Add(new HeThongDatTiecCuoi_API.Models.PaymentTransaction
        { DatTiecId = 1, TransactionType = PaymentTransactionTypes.Installment, OrderCode = 999, SoTienGiaoDich = 10_000, SoTienGhiNhan = 100_000, TrangThai = PaymentTransactionStatusCodes.Pending });
        await db.SaveChangesAsync();
        var changes = new ManagerBookingChangesController(db, new TestStatuses());
        var blocked = await changes.CancelQuote(1, new ManagerBookingCancelQuoteRequestDto(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(blocked);
    }

    private static PayOSPaymentService CreateService(ApplicationDbContext db, TestGateway gateway) => new(
        db, new PayOSClient("test", "test", "test"),
        Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = false }), new TestStatuses(),
        null!, null!, null!, null!, NullLogger<PayOSPaymentService>.Instance,
        depositPaymentGateway: gateway);

    private static DbContextOptions<ApplicationDbContext> CreateOptions() => new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
        .ConfigureWarnings(warnings => warnings
            .Ignore(InMemoryEventId.TransactionIgnoredWarning)
            .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
        .Options;

    private static async Task SeedAsync(DbContextOptions<ApplicationDbContext> options, decimal contractTotal = 1_000_000m, bool seedLedger = false)
    {
        await using var db = new ApplicationDbContext(options);
        db.Statuses.AddRange(
            new Status { StatusId = 1, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Confirmed" },
            new Status { StatusId = 2, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Active" },
            new Status { StatusId = 3, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Completed, StatusName = "Completed" },
            new Status { StatusId = 4, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Pending, StatusName = "Pending" },
            new Status { StatusId = 5, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Success" },
            new Status { StatusId = 6, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Failed, StatusName = "Failed" },
            new Status { StatusId = 7, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Cancelled, StatusName = "Cancelled" });
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" });
        var hall = new Hall { HallId = 1, HallCode = "H1", HallName = "Hall", MinimumCapacity = 1, MaximumCapacity = 500, RentalPrice = 1, StatusId = 1, DataStatusId = 1 };
        var schedule = new HallSchedule { HallScheduleId = 1, HallId = 1, Hall = hall, Date = DateTime.Today.AddDays(30), Shift = "Evening", StatusId = 1, DataStatusId = 1 };
        var customer = new Customer { CustomerId = 1, FullName = "Customer", PhoneNumber = "0900000000", DataStatusId = 1 };
        var booking = new WeddingBooking { BookingId = 1, BookingCode = "B-1", CustomerId = 1, Customer = customer, HallScheduleId = 1, HallSchedule = schedule, StatusId = 1, GuestCount = 50, EstimatedTotal = contractTotal, BookedAt = DateTime.Now };
        var contract = new Contract { ContractId = 1, BookingId = 1, Booking = booking, ContractCode = "C-1", CreatedDate = DateTime.Today, TotalValue = contractTotal, StatusId = 2 };
        db.WeddingBookings.Add(booking);
        db.Contracts.Add(contract);
        if (seedLedger)
        {
            db.Payments.Add(new Payment { ContractId = 1, Contract = contract, PaymentType = PaymentTypes.Deposit, Amount = 300_000m, PaymentDate = DateTime.Now, TransactionCode = "old", StatusId = 5 });
            var operation = new Uc18BookingOperation
            { RequestId = Guid.NewGuid(), BookingId = 1, ContractId = 1, ActorUserId = 1, Kind = Uc18BookingOperation.Reschedule, PayloadHash = "x", RecordedAtUtc = DateTime.UtcNow, SignedPriceDelta = 200_000m, PrincipalRefundDue = 100_000m, DepositPrincipalPortion = 100_000m };
            db.Set<Uc18BookingOperation>().Add(operation);
            db.Set<Uc18RefundConfirmation>().Add(new Uc18RefundConfirmation
            { RequestId = Guid.NewGuid(), Operation = operation, OperationRequestId = operation.RequestId, ActorUserId = 1, PayloadHash = "refund", ExternalEvidenceReference = "proof", ConfirmedAtUtc = DateTime.UtcNow, PrincipalPaid = 100_000m, DepositPrincipalPaid = 100_000m });
        }
        await db.SaveChangesAsync();
    }

    private static CreatePaymentLinkResponse Link(CreatePaymentLinkRequest? request, PaymentLinkStatus status) => new()
    {
        OrderCode = request?.OrderCode ?? 0, Amount = request?.Amount ?? 100_000,
        Status = status, CheckoutUrl = "https://pay.invalid", QrCode = "test"
    };

    private static Webhook BuildWebhook(PayOSClient client, long orderCode, long amount)
    {
        var data = new WebhookData
        {
            OrderCode = orderCode,
            Amount = amount,
            Description = "HD1 Dot 2",
            AccountNumber = "12345678",
            Reference = "TF260110000001",
            TransactionDateTime = "2026-01-01 00:00:00",
            Currency = "VND",
            PaymentLinkId = "b7d6cecc-e5bd-4a44-a72f-51df10e99c8e",
            Code = "00",
            Description2 = "Thành công"
        };
        return new Webhook
        {
            Code = "00",
            Description = "success",
            Success = true,
            Data = data,
            Signature = client.Crypto.CreateSignatureFromObject(data, client.ChecksumKey) ?? ""
        };
    }

    private sealed class TestGateway(
        Func<CreatePaymentLinkRequest, Task<CreatePaymentLinkResponse>> create,
        Func<long, Task<CreatePaymentLinkResponse>>? get = null,
        Func<long, Task<PaymentLink>>? paymentLink = null) : IPayOSDepositPaymentGateway
    {
        public int CreateCalls { get; private set; }
        public int GetCalls { get; private set; }
        public async Task<CreatePaymentLinkResponse> CreateAsync(CreatePaymentLinkRequest request, CancellationToken cancellationToken = default)
        { CreateCalls++; return await create(request); }
        public async Task<PaymentLink> GetAsync(long orderCode, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            if (paymentLink is not null)
                return await paymentLink(orderCode);
            var response = await (get ?? (_ => Task.FromResult(Link(null, PaymentLinkStatus.Pending))))(orderCode);
            return new PaymentLink
            {
                OrderCode = orderCode,
                Amount = response.Amount,
                AmountPaid = response.Status == PaymentLinkStatus.Paid ? response.Amount : 0,
                Status = response.Status
            };
        }
    }

    private sealed class TestStatuses : IStatusService
    {
        public Task<int> GetStatusIdAsync(string group, string code, CancellationToken cancellationToken = default) => Task.FromResult((group, code) switch
        {
            (StatusGroups.Payment, PaymentStatusCodes.Pending) => 4,
            (StatusGroups.Payment, PaymentStatusCodes.Success) => 5,
            (StatusGroups.Payment, PaymentStatusCodes.Failed) => 6,
            (StatusGroups.Payment, PaymentStatusCodes.Cancelled) => 7,
            _ => throw new InvalidOperationException($"Unknown status {group}/{code}")
        });
        public Task<Status?> GetStatusAsync(string group, string code, CancellationToken cancellationToken = default) => Task.FromResult<Status?>(null);
    }
}

using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using Xunit;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;
using PaymentTransactionEntity = HeThongDatTiecCuoi_API.Models.PaymentTransaction;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class LatePayOSPaymentTests
{
    private const int PendingBookingStatusId = 101;
    private const int ConfirmedBookingStatusId = 102;
    private const int CancelledBookingStatusId = 103;
    private const int AvailableScheduleStatusId = 201;
    private const int BookedScheduleStatusId = 202;
    private const int DraftContractStatusId = 301;
    private const int SuccessfulPaymentStatusId = 401;

    [Fact]
    public async Task TC01_CancelWhilePaymentPending_CancelsBookingAndReleasesSchedule()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var controller = CreateCancellationController(db);

        var result = await controller.CancelBooking(1, "Khách đổi kế hoạch", "DT-LATE-001", "0901234567");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.FindAsync(1))!.StatusId);
        Assert.Equal(AvailableScheduleStatusId, (await db.HallSchedules.FindAsync(1))!.StatusId);
        Assert.Equal(PaymentTransactionStatusCodes.Pending, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Single(await db.AuditLogs.Where(item => item.Action == AuditActions.BookingHoldCancelled).ToListAsync());
    }

    [Fact]
    public async Task TC02_LatePaymentAfterCancellation_OnlyCreatesReconciliationEvidence()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "valid late webhook");

        var booking = await db.WeddingBookings.Include(item => item.Contract).SingleAsync();
        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(CancelledBookingStatusId, booking.StatusId);
        Assert.Equal(0, booking.PaidAmount);
        Assert.Null(booking.Contract);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
        Assert.NotNull(transaction.NgayThanhToan);
        var status = await service.GetPaymentStatusAsync(900001);
        Assert.Equal(
            "Thanh toán được ghi nhận sau khi đơn đặt tiệc đã hủy. Giao dịch đang chờ bộ phận quản lý đối soát.",
            status.Message);
    }

    [Fact]
    public async Task TC03_DuplicateLateWebhook_IsIdempotent()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "webhook 1");
        await service.ProcessConfirmedDepositAsync(900001, 10_000, "webhook duplicate");

        Assert.Equal(0, (await db.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Single(await db.AuditLogs.Where(item =>
            item.Action == AuditActions.Payment &&
            item.NewData != null && item.NewData.Contains(PaymentTransactionStatusCodes.ReconciliationRequired)).ToListAsync());
    }

    [Fact]
    public async Task TC04_PollingAfterCancellation_UsesSameReconciliationPath()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(
            900001, 10_000, "Auto-polled from PayOS PaymentRequests.GetAsync");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.SingleAsync()).StatusId);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC05_NormalSuccessfulDeposit_ConfirmsAndRecordsExactlyOnce()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "valid webhook");
        await service.ProcessConfirmedDepositAsync(900001, 10_000, "duplicate webhook");

        var booking = await db.WeddingBookings.SingleAsync();
        Assert.Equal(ConfirmedBookingStatusId, booking.StatusId);
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Equal(PaymentTransactionStatusCodes.Paid, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Single(await db.Contracts.ToListAsync());
        Assert.Single(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC06_ConcurrentPaymentAndCancellation_EndsInOneConsistentOutcome()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = $"late-payment-race-{Guid.NewGuid()}";
        var options = CreateOptions(databaseName, root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked, options)) { }

        await using var cancellationDb = new ApplicationDbContext(options);
        await using var paymentDb = new ApplicationDbContext(options);
        var controller = CreateCancellationController(cancellationDb);
        var service = CreatePaymentService(paymentDb);

        var cancellationTask = controller.CancelBooking(1, "Hủy đồng thời", "DT-LATE-001", "0901234567");
        var paymentTask = service.ProcessConfirmedDepositAsync(900001, 10_000, "concurrent webhook");
        await Task.WhenAll(cancellationTask, paymentTask);

        await using var assertDb = new ApplicationDbContext(options);
        var booking = await assertDb.WeddingBookings.SingleAsync();
        var transaction = await assertDb.PaymentTransactions.SingleAsync();
        var payments = await assertDb.Payments.CountAsync();

        var cancellationWon = booking.StatusId == CancelledBookingStatusId &&
                              transaction.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired &&
                              booking.PaidAmount == 0 && payments == 0;
        var paymentWon = booking.StatusId == ConfirmedBookingStatusId &&
                         transaction.TrangThai == PaymentTransactionStatusCodes.Paid &&
                         booking.PaidAmount == 1_000_000 && payments == 1;
        Assert.True(cancellationWon || paymentWon,
            $"Inconsistent result: booking={booking.StatusId}, tx={transaction.TrangThai}, paid={booking.PaidAmount}, payments={payments}");
    }

    [Fact]
    public async Task TC07_InvalidPaymentAmount_GoesToReconciliationWithoutConfirming()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 9_999, "wrong amount webhook");

        var transaction = await db.PaymentTransactions.SingleAsync();
        var booking = await db.WeddingBookings.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaymentAmountMismatch, transaction.ReconciliationData);
        Assert.Equal(PendingBookingStatusId, booking.StatusId);
        Assert.Equal(0, booking.PaidAmount);
        Assert.Empty(await db.Contracts.ToListAsync());
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC08_HallReassignedAfterCancellation_LatePaymentDoesNotTouchNewBookingSchedule()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Booked);
        var confirmedStatus = await db.Statuses.SingleAsync(item => item.StatusId == ConfirmedBookingStatusId);
        var customer = new Customer { CustomerId = 2, FullName = "Khách B", PhoneNumber = "0907654321", DataStatusId = 1 };
        db.Customers.Add(customer);
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 2,
            BookingCode = "DT-NEW-002",
            CustomerId = 2,
            HallScheduleId = 1,
            StatusId = ConfirmedBookingStatusId,
            Status = confirmedStatus,
            GuestCount = 100,
            BookedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "late webhook");

        Assert.Equal(BookedScheduleStatusId, (await db.HallSchedules.FindAsync(1))!.StatusId);
        Assert.Equal(ConfirmedBookingStatusId, (await db.WeddingBookings.FindAsync(2))!.StatusId);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.FindAsync(1))!.StatusId);
    }

    [Fact]
    public async Task CancelPaymentLinkFailure_IsRecordedWithoutUndoingBookingCancellation()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db, new ThrowingCancellationClient());

        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Null(transaction.ReconciliationData);
        Assert.Single(await db.PaymentTransactionEvents.Where(item =>
            item.EventType == PaymentTransactionEventTypes.PayOsCancelRequestFailed).ToListAsync());
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.SingleAsync()).StatusId);
    }

    [Fact]
    public async Task CancelPaymentLinkRequest_DoesNotClaimPayOsCancellationWasConfirmed()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db, new SuccessfulCancellationClient());

        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Null(transaction.ReconciliationData);
        Assert.Single(await db.PaymentTransactionEvents.Where(item =>
            item.EventType == PaymentTransactionEventTypes.PayOsCancelRequested).ToListAsync());
    }

    [Fact]
    public async Task OutOfOrderPaidWebhook_AfterCancelledStatus_StillRecordsReceivedMoneyForReconciliation()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var transaction = await db.PaymentTransactions.SingleAsync();
        transaction.TrangThai = PaymentTransactionStatusCodes.Cancelled;
        await db.SaveChangesAsync();

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "late paid webhook");

        transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
    }

    [Fact]
    public async Task TC12_CreateDepositLink_WhenBookingCancelledDuringPayOsCall()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"create-cancel-race-{Guid.NewGuid()}", root);
        await RemoveSeedTransactionAsync(options);
        var gateway = ControlledDepositGateway.BlockingCreate();
        var cancellation = new CountingCancellationClient();

        await using var createDb = new ApplicationDbContext(options);
        await using var cancelDb = new ApplicationDbContext(options);
        var service = CreatePaymentService(createDb, cancellation, gateway);
        var createTask = service.CreateDepositPaymentAsync(DepositRequest());
        await gateway.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var cancelResult = await CreateCancellationController(cancelDb)
            .CancelBooking(1, "Hủy khi PayOS đang tạo link", "DT-LATE-001", "0901234567");
        Assert.IsType<OkObjectResult>(cancelResult);
        gateway.ReleaseCreate();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await createTask);
        await using var assertDb = new ApplicationDbContext(options);
        var transaction = await assertDb.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Failed, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.BookingCancelledDuringLinkCreation, transaction.ReconciliationData);
        Assert.Equal(CancelledBookingStatusId, (await assertDb.WeddingBookings.SingleAsync()).StatusId);
        Assert.Equal(1, cancellation.Calls);
        Assert.Single(await assertDb.PaymentTransactionEvents.Where(item =>
            item.EventType == PaymentTransactionEventTypes.PayOsCancelRequested).ToListAsync());
    }

    [Fact]
    public async Task TC13_CreateDepositLink_TwoConcurrentRequests()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"two-create-{Guid.NewGuid()}", root);
        await RemoveSeedTransactionAsync(options);
        var gateway = ControlledDepositGateway.Immediate();

        await using var db1 = new ApplicationDbContext(options);
        await using var db2 = new ApplicationDbContext(options);
        var first = TryCreateAsync(CreatePaymentService(db1, depositGateway: gateway));
        var second = TryCreateAsync(CreatePaymentService(db2, depositGateway: gateway));
        var results = await Task.WhenAll(first, second);

        await using var assertDb = new ApplicationDbContext(options);
        Assert.Single(results, item => item);
        Assert.Equal(1, gateway.CreateCalls);
        Assert.Single(await assertDb.PaymentTransactions.Where(item =>
            item.TransactionType == PaymentTransactionTypes.Deposit &&
            item.TrangThai == PaymentTransactionStatusCodes.Pending).ToListAsync());
    }

    [Fact]
    public async Task TC14_SecondOrderCodePaidAfterFirstDeposit()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        await AddSecondDepositTransactionAsync(db);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "first paid");
        await service.ProcessConfirmedDepositAsync(900002, 10_000, "second paid");

        var booking = await db.WeddingBookings.SingleAsync();
        var second = await db.PaymentTransactions.SingleAsync(item => item.OrderCode == 900002);
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, second.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.DuplicateDepositPayment, second.ReconciliationData);
        Assert.Single(await db.Payments.Where(item => item.PaymentType == PaymentTypes.Deposit).ToListAsync());
        Assert.Single(await db.Contracts.ToListAsync());
    }

    [Fact]
    public async Task TC15_TwoDifferentOrderCodesPaidConcurrently()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"two-paid-{Guid.NewGuid()}", root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked, options))
            await AddSecondDepositTransactionAsync(seed);

        await using var db1 = new ApplicationDbContext(options);
        await using var db2 = new ApplicationDbContext(options);
        await Task.WhenAll(
            CreatePaymentService(db1).ProcessConfirmedDepositAsync(900001, 10_000, "webhook X"),
            CreatePaymentService(db2).ProcessConfirmedDepositAsync(900002, 10_000, "webhook Y"));

        await AssertExactlyOneDepositAsync(options);
    }

    [Fact]
    public async Task TC16_PollingAndWebhookForDifferentOrderCodes()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"poll-webhook-{Guid.NewGuid()}", root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked, options))
            await AddSecondDepositTransactionAsync(seed);

        var gateway = ControlledDepositGateway.ForPaidStatus();
        await using var pollDb = new ApplicationDbContext(options);
        await using var webhookDb = new ApplicationDbContext(options);
        await Task.WhenAll(
            CreatePaymentService(pollDb, depositGateway: gateway).GetPaymentStatusAsync(900001),
            CreatePaymentService(webhookDb).ProcessConfirmedDepositAsync(900002, 10_000, "webhook Y"));

        await AssertExactlyOneDepositAsync(options);
    }

    [Fact]
    public async Task TC17_CancelRequestResultArrivesAfterLatePaymentWebhook()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"cancel-after-paid-{Guid.NewGuid()}", root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available, options)) { }
        var cancellation = new BlockingCancellationClient(false);

        await using var cancelDb = new ApplicationDbContext(options);
        await using var webhookDb = new ApplicationDbContext(options);
        var cancelTask = CreatePaymentService(cancelDb, cancellation)
            .CancelPendingDepositLinksAsync(1, "Booking cancelled");
        await cancellation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await CreatePaymentService(webhookDb)
            .ProcessConfirmedDepositAsync(900001, 10_000, "late paid webhook");
        cancellation.Release();
        await cancelTask;

        await AssertReconciliationPreservedAfterCancelAsync(
            options, PaymentTransactionEventTypes.PayOsCancelRequested);
    }

    [Fact]
    public async Task TC18_CancelRequestFailedAfterReconciliation()
    {
        var root = new InMemoryDatabaseRoot();
        var options = CreateOptions($"cancel-failed-after-paid-{Guid.NewGuid()}", root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available, options)) { }
        var cancellation = new BlockingCancellationClient(true);

        await using var cancelDb = new ApplicationDbContext(options);
        await using var webhookDb = new ApplicationDbContext(options);
        var cancelTask = CreatePaymentService(cancelDb, cancellation)
            .CancelPendingDepositLinksAsync(1, "Booking cancelled");
        await cancellation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await CreatePaymentService(webhookDb)
            .ProcessConfirmedDepositAsync(900001, 10_000, "late paid webhook");
        cancellation.Release();
        await cancelTask;

        await AssertReconciliationPreservedAfterCancelAsync(
            options, PaymentTransactionEventTypes.PayOsCancelRequestFailed);
    }

    [Fact]
    public async Task TC19_RepeatedCancelLinkRequests()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var cancellation = new CountingCancellationClient();
        var service = CreatePaymentService(db, cancellation);

        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled");
        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled again");

        Assert.Equal(1, cancellation.Calls);
        Assert.Single(await db.PaymentTransactionEvents.Where(item =>
            item.EventType == PaymentTransactionEventTypes.PayOsCancelRequested).ToListAsync());
        Assert.Equal(PaymentTransactionStatusCodes.Pending, (await db.PaymentTransactions.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task TC20_PaidWebhookAfterConfirmedCancellation()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var transaction = await db.PaymentTransactions.SingleAsync();
        transaction.TrangThai = PaymentTransactionStatusCodes.Cancelled;
        await db.SaveChangesAsync();

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "paid after cancel confirmation");

        transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
        Assert.NotNull(transaction.PayloadWebhook);
    }

    [Fact]
    public async Task TC21_ExistingNormalDepositRegression()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "normal paid webhook");

        var booking = await db.WeddingBookings.SingleAsync();
        Assert.Equal(ConfirmedBookingStatusId, booking.StatusId);
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Single(await db.Contracts.ToListAsync());
        Assert.Single(await db.Payments.Where(item => item.PaymentType == PaymentTypes.Deposit).ToListAsync());
    }

    private static CreateDepositPaymentRequestDto DepositRequest() => new()
    {
        BookingId = 1,
        BookingCode = "DT-LATE-001",
        PhoneNumber = "0901234567"
    };

    private static async Task<bool> TryCreateAsync(PayOSPaymentService service)
    {
        try
        {
            await service.CreateDepositPaymentAsync(DepositRequest());
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task RemoveSeedTransactionAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var seed = await CreateDatabaseAsync(
            BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked, options);
        seed.PaymentTransactions.RemoveRange(await seed.PaymentTransactions.ToListAsync());
        await seed.SaveChangesAsync();
        seed.ChangeTracker.Clear();
    }

    private static async Task AddSecondDepositTransactionAsync(ApplicationDbContext db)
    {
        db.ChangeTracker.Clear();
        var booking = await db.WeddingBookings.SingleAsync(item => item.BookingId == 1);
        db.PaymentTransactions.Add(new PaymentTransactionEntity
        {
            GiaoDichId = 2,
            DatTiecId = 1,
            Booking = booking,
            TransactionType = PaymentTransactionTypes.Deposit,
            OrderCode = 900002,
            SoTienGiaoDich = 10_000,
            SoTienGhiNhan = 1_000_000,
            TrangThai = PaymentTransactionStatusCodes.Pending,
            NgayTao = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task AssertExactlyOneDepositAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        var booking = await db.WeddingBookings.SingleAsync();
        var transactions = await db.PaymentTransactions.OrderBy(item => item.OrderCode).ToListAsync();
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Single(transactions, item => item.TrangThai == PaymentTransactionStatusCodes.Paid);
        var duplicate = Assert.Single(transactions,
            item => item.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired);
        Assert.Contains(PaymentReconciliationReasons.DuplicateDepositPayment, duplicate.ReconciliationData);
        Assert.Single(await db.Payments.Where(item => item.PaymentType == PaymentTypes.Deposit).ToListAsync());
        Assert.Single(await db.Contracts.ToListAsync());
    }

    private static async Task AssertReconciliationPreservedAfterCancelAsync(
        DbContextOptions<ApplicationDbContext> options,
        string eventType)
    {
        await using var db = new ApplicationDbContext(options);
        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
        Assert.DoesNotContain(eventType, transaction.ReconciliationData);
        Assert.NotNull(transaction.PayloadWebhook);
        Assert.Single(await db.PaymentTransactionEvents.Where(item => item.EventType == eventType).ToListAsync());
    }

    private static MyBookingsController CreateCancellationController(ApplicationDbContext db)
    {
        var controller = new MyBookingsController(db, new TestStatusService(), new CoordinationStub());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        return controller;
    }

    private static PayOSPaymentService CreatePaymentService(
        ApplicationDbContext db,
        IPayOSPaymentLinkCancellation? cancellation = null,
        IPayOSDepositPaymentGateway? depositGateway = null) =>
        new(
            db,
            new PayOSClient("test-client", "test-api-key", "test-checksum-key"),
            Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 100 }),
            new TestStatusService(),
            null!,
            null!,
            null!,
            null!,
            NullLogger<PayOSPaymentService>.Instance,
            cancellation,
            depositGateway);

    private static DbContextOptions<ApplicationDbContext> CreateOptions(
        string? databaseName = null,
        InMemoryDatabaseRoot? root = null) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), root ?? new InMemoryDatabaseRoot())
            .ConfigureWarnings(warnings => warnings
                .Ignore(InMemoryEventId.TransactionIgnoredWarning)
                .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private static async Task<ApplicationDbContext> CreateDatabaseAsync(
        string bookingStatusCode,
        string scheduleStatusCode,
        DbContextOptions<ApplicationDbContext>? options = null)
    {
        var db = new ApplicationDbContext(options ?? CreateOptions());
        var statuses = new[]
        {
            new Status { StatusId = PendingBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" },
            new Status { StatusId = ConfirmedBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" },
            new Status { StatusId = CancelledBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new Status { StatusId = AvailableScheduleStatusId, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" },
            new Status { StatusId = BookedScheduleStatusId, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new Status { StatusId = DraftContractStatusId, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Nháp" },
            new Status { StatusId = SuccessfulPaymentStatusId, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" }
        };
        db.Statuses.AddRange(statuses);
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });

        var bookingStatus = statuses.Single(item => item.StatusCode == bookingStatusCode && item.StatusGroup == StatusGroups.Booking);
        var scheduleStatus = statuses.Single(item => item.StatusCode == scheduleStatusCode && item.StatusGroup == StatusGroups.HallSchedule);
        var hall = new Hall
        {
            HallId = 1, HallCode = "H01", HallName = "Sảnh Riverside", MinimumCapacity = 50,
            MaximumCapacity = 500, RentalPrice = 20_000_000, StatusId = AvailableScheduleStatusId, DataStatusId = 1
        };
        var schedule = new HallSchedule
        {
            HallScheduleId = 1, HallId = 1, Hall = hall, Date = DateTime.Today.AddDays(30), Shift = "Ca tối",
            StatusId = scheduleStatus.StatusId, Status = scheduleStatus, DataStatusId = 1
        };
        var customer = new Customer
        {
            CustomerId = 1, FullName = "Nguyễn Khách", PhoneNumber = "0901234567", DataStatusId = 1
        };
        var booking = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-LATE-001",
            CustomerId = 1,
            Customer = customer,
            HallScheduleId = 1,
            HallSchedule = schedule,
            StatusId = bookingStatus.StatusId,
            Status = bookingStatus,
            GuestCount = 200,
            EstimatedTotal = 10_000_000,
            PaidAmount = 0,
            BookedAt = DateTime.Now
        };
        db.Halls.Add(hall);
        db.HallSchedules.Add(schedule);
        db.Customers.Add(customer);
        db.WeddingBookings.Add(booking);
        db.PaymentTransactions.Add(new PaymentTransactionEntity
        {
            GiaoDichId = 1,
            DatTiecId = 1,
            Booking = booking,
            TransactionType = PaymentTransactionTypes.Deposit,
            OrderCode = 900001,
            SoTienGiaoDich = 10_000,
            SoTienGhiNhan = 1_000_000,
            TrangThai = PaymentTransactionStatusCodes.Pending,
            NgayTao = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class TestStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult((statusGroup, statusCode) switch
            {
                (StatusGroups.Booking, BookingStatusCodes.Pending) => PendingBookingStatusId,
                (StatusGroups.Booking, BookingStatusCodes.Confirmed) => ConfirmedBookingStatusId,
                (StatusGroups.Booking, BookingStatusCodes.Cancelled) => CancelledBookingStatusId,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Available) => AvailableScheduleStatusId,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked) => BookedScheduleStatusId,
                (StatusGroups.Contract, ContractStatusCodes.Draft) => DraftContractStatusId,
                (StatusGroups.Payment, PaymentStatusCodes.Success) => SuccessfulPaymentStatusId,
                _ => throw new InvalidOperationException($"Unknown status {statusGroup}/{statusCode}")
            });

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(null);
    }

    private sealed class ThrowingCancellationClient : IPayOSPaymentLinkCancellation
    {
        public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("PayOS unavailable");
    }

    private sealed class SuccessfulCancellationClient : IPayOSPaymentLinkCancellation
    {
        public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CountingCancellationClient : IPayOSPaymentLinkCancellation
    {
        private int _calls;
        public int Calls => _calls;

        public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingCancellationClient(bool fail) : IPayOSPaymentLinkCancellation
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            if (fail)
                throw new HttpRequestException("PayOS cancellation failed after webhook");
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class ControlledDepositGateway : IPayOSDepositPaymentGateway
    {
        private readonly bool _blockCreate;
        private readonly TaskCompletionSource _createRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _createCalls;

        private ControlledDepositGateway(bool blockCreate)
        {
            _blockCreate = blockCreate;
            if (!blockCreate)
                _createRelease.TrySetResult();
        }

        public int CreateCalls => _createCalls;
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static ControlledDepositGateway BlockingCreate() => new(true);
        public static ControlledDepositGateway Immediate() => new(false);
        public static ControlledDepositGateway ForPaidStatus() => new(false);

        public async Task<CreatePaymentLinkResponse> CreateAsync(
            CreatePaymentLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _createCalls);
            CreateStarted.TrySetResult();
            if (_blockCreate)
                await _createRelease.Task.WaitAsync(cancellationToken);
            return new CreatePaymentLinkResponse
            {
                OrderCode = request.OrderCode,
                Amount = request.Amount,
                Status = PaymentLinkStatus.Pending,
                CheckoutUrl = $"https://pay.test/{request.OrderCode}",
                QrCode = $"QR-{request.OrderCode}",
                Bin = "970422",
                AccountNumber = "123456789",
                AccountName = "RIVERSIDE PALACE"
            };
        }

        public Task<PaymentLink> GetAsync(long orderCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentLink
            {
                OrderCode = orderCode,
                Amount = 10_000,
                AmountPaid = 10_000,
                Status = PaymentLinkStatus.Paid
            });

        public void ReleaseCreate() => _createRelease.TrySetResult();
    }

    private sealed class CoordinationStub : ICoordinationAssignmentService
    {
        public Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId, HallManagerCoordinationQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId, CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId, ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId, CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode, int? actorUserId, string? reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId, int? excludedBookingId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

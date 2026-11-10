using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;
using PaymentTransactionEntity = HeThongDatTiecCuoi_API.Models.PaymentTransaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class PayOSPaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly PayOSClient _payOSClient;
    private readonly IOptions<AppPayOSOptions> _options;
    private readonly IStatusService _statusService;
    private readonly IEmailService _emailService;
    private readonly IInvoicePdfGenerator _invoicePdfGenerator;
    private readonly IInvoiceDigitalSignatureService _signatureService;
    private readonly ILogger<PayOSPaymentService> _logger;
    private readonly IScheduleChangeService _scheduleChanges;
    private readonly IPayOSPaymentLinkCancellation _paymentLinkCancellation;
    private readonly IPayOSDepositPaymentGateway _depositPaymentGateway;

    public PayOSPaymentService(
        ApplicationDbContext context,
        PayOSClient payOSClient,
        IOptions<AppPayOSOptions> options,
        IStatusService statusService,
        IEmailService emailService,
        IInvoicePdfGenerator invoicePdfGenerator,
        IInvoiceDigitalSignatureService signatureService,
        IScheduleChangeService scheduleChanges,
        ILogger<PayOSPaymentService> logger,
        IPayOSPaymentLinkCancellation? paymentLinkCancellation = null,
        IPayOSDepositPaymentGateway? depositPaymentGateway = null)
    {
        _context = context;
        _payOSClient = payOSClient;
        _options = options;
        _statusService = statusService;
        _emailService = emailService;
        _invoicePdfGenerator = invoicePdfGenerator;
        _signatureService = signatureService;
        _scheduleChanges = scheduleChanges;
        _logger = logger;
        _paymentLinkCancellation = paymentLinkCancellation ?? new PayOSPaymentLinkCancellation(payOSClient);
        _depositPaymentGateway = depositPaymentGateway ?? new PayOSDepositPaymentGateway(payOSClient);
    }

    public async Task<CreateDepositPaymentResponseDto> CreateDepositPaymentAsync(
        CreateDepositPaymentRequestDto request,
        int? userId = null)
    {
        if (request.BookingId <= 0)
            throw new ArgumentException("Mã đặt tiệc ID không hợp lệ.", nameof(request.BookingId));

        var accessBooking = await _context.WeddingBookings
            .AsNoTracking()
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.BookingId == request.BookingId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn đặt tiệc cưới này.");
        EnsureDepositPaymentAccess(accessBooking, request, userId);
        await ResolveExpiredDepositIntentAsync(request.BookingId);
        _context.ChangeTracker.Clear();

        PaymentTransactionEntity intent;
        string bookingCode;
        decimal businessDeposit;
        int actualAmount;
        string description;
        DateTime expiresAt;

        using (await BookingPaymentConcurrency.AcquireAsync(request.BookingId))
        await using (var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(_context))
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, request.BookingId);
            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .SingleOrDefaultAsync(b => b.BookingId == request.BookingId)
                ?? throw new KeyNotFoundException("Không tìm thấy đơn đặt tiệc cưới này.");

            EnsureDepositPaymentAccess(booking, request, userId);
            await EnsureBookingCanCreateDepositAsync(booking);

            var activeIntent = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(item => item.DatTiecId == booking.BookingId &&
                    item.TransactionType == PaymentTransactionTypes.Deposit &&
                    (item.TrangThai == PaymentTransactionStatusCodes.Creating ||
                     item.TrangThai == PaymentTransactionStatusCodes.Pending))
                .OrderByDescending(item => item.NgayTao)
                .FirstOrDefaultAsync();
            if (activeIntent is not null)
                throw new InvalidOperationException(
                    $"Đơn đặt tiệc đang có giao dịch cọc hoạt động (OrderCode: {activeIntent.OrderCode}). Vui lòng tiếp tục hoặc kiểm tra trạng thái giao dịch hiện tại trước khi tạo link mới.");

            var totalEstimate = booking.EstimatedTotal ?? 0m;
            if (totalEstimate <= 0)
                throw new InvalidOperationException("Tổng dự toán tiệc không hợp lệ để tính tiền cọc.");

            businessDeposit = Math.Round(totalEstimate * 0.20m, 0);
            actualAmount = CalculateActualPayOsAmount(businessDeposit);
            bookingCode = booking.BookingCode;
            description = $"RSP {booking.BookingId}";
            if (description.Length > 25)
                description = description[..25];

            intent = new PaymentTransactionEntity
            {
                DatTiecId = booking.BookingId,
                TransactionType = PaymentTransactionTypes.Deposit,
                OrderCode = await GenerateUniqueOrderCodeAsync(),
                SoTienGiaoDich = actualAmount,
                SoTienGhiNhan = businessDeposit,
                PhuongThuc = "VIETQR_PAYOS",
                TrangThai = PaymentTransactionStatusCodes.Creating,
                NgayTao = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(10)
            };
            _context.PaymentTransactions.Add(intent);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }

        _context.ChangeTracker.Clear();

        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = intent.OrderCode,
            Amount = actualAmount,
            Description = description,
            Items =
            [
                new PaymentLinkItem
                {
                    Name = $"Coc tiec {bookingCode}",
                    Quantity = 1,
                    Price = actualAmount
                }
            ],
            CancelUrl = _options.Value.CancelUrl,
            ReturnUrl = _options.Value.ReturnUrl
        };

        CreatePaymentLinkResponse paymentResult;
        try
        {
            paymentResult = await _depositPaymentGateway.CreateAsync(paymentRequest);
        }
        catch (Exception ex)
        {
            await MarkDepositIntentFailedAsync(
                request.BookingId, intent.GiaoDichId,
                PaymentReconciliationReasons.PayOsLinkCreationFailed, ex.Message);
            _logger.LogError(ex,
                "Lỗi khi tạo link PayOS cho BookingId={BookingId}, OrderCode={OrderCode}",
                request.BookingId, intent.OrderCode);
            throw new InvalidOperationException($"Không thể tạo liên kết thanh toán VietQR lúc này: {ex.Message}", ex);
        }

        _context.ChangeTracker.Clear();
        var activated = false;
        string invalidReason;
        using (await BookingPaymentConcurrency.AcquireAsync(request.BookingId))
        await using (var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(_context))
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, request.BookingId);
            var booking = await _context.WeddingBookings
                .Include(item => item.Status)
                .SingleAsync(item => item.BookingId == request.BookingId);
            var currentIntent = await _context.PaymentTransactions
                .SingleAsync(item => item.GiaoDichId == intent.GiaoDichId);

            var depositAlreadyRecorded = await HasOtherRecordedDepositAsync(
                booking, currentIntent.GiaoDichId, CancellationToken.None);
            activated = currentIntent.TrangThai == PaymentTransactionStatusCodes.Creating &&
                booking.Status.StatusCode is BookingStatusCodes.Pending or BookingStatusCodes.Confirmed &&
                !depositAlreadyRecorded;

            if (activated)
            {
                currentIntent.TrangThai = PaymentTransactionStatusCodes.Pending;
                expiresAt = DateTime.Now.AddMinutes(10);
                currentIntent.ExpiresAt = expiresAt;
                booking.DepositAmount = businessDeposit;
                invalidReason = string.Empty;
            }
            else
            {
                expiresAt = currentIntent.ExpiresAt ?? DateTime.Now;
                invalidReason = booking.Status.StatusCode == BookingStatusCodes.Cancelled
                    ? PaymentReconciliationReasons.BookingCancelledDuringLinkCreation
                    : depositAlreadyRecorded
                        ? PaymentReconciliationReasons.DuplicateDepositPayment
                        : $"INTENT_STATUS_{currentIntent.TrangThai}";
                if (currentIntent.TrangThai == PaymentTransactionStatusCodes.Creating)
                {
                    currentIntent.TrangThai = PaymentTransactionStatusCodes.Failed;
                    currentIntent.ReconciliationData = JsonSerializer.Serialize(new
                    {
                        reason = invalidReason,
                        intent.OrderCode,
                        bookingStatus = booking.Status.StatusCode,
                        recordedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }

        if (!activated)
        {
            await CancelDepositLinkAndRecordAsync(
                request.BookingId, intent.GiaoDichId, intent.OrderCode,
                "Booking is no longer eligible for this deposit link", CancellationToken.None);
            throw new InvalidOperationException(
                $"Liên kết PayOS đã được tạo nhưng booking không còn đủ điều kiện thanh toán ({invalidReason}). Link đã được đưa vào quy trình hủy và không được trả về cho khách.");
        }

        _logger.LogInformation(
            "Đã kích hoạt link PayOS: BookingId={BookingId}, OrderCode={OrderCode}, BusinessDeposit={BusinessDeposit}, ActualAmount={ActualAmount}, IsDemo={IsDemo}",
            request.BookingId, intent.OrderCode, businessDeposit, actualAmount, _options.Value.IsDemoMode);

        return new CreateDepositPaymentResponseDto
        {
            OrderCode = intent.OrderCode,
            QrCode = paymentResult.QrCode ?? string.Empty,
            CheckoutUrl = paymentResult.CheckoutUrl ?? string.Empty,
            AccountNumber = paymentResult.AccountNumber,
            AccountName = paymentResult.AccountName,
            BankBin = paymentResult.Bin,
            BankName = paymentResult.Bin,
            Description = description,
            BusinessDeposit = businessDeposit,
            ActualAmount = actualAmount,
            IsDemoMode = _options.Value.IsDemoMode,
            ExpiresAt = expiresAt
        };
    }

    public async Task<bool> ProcessWebhookAsync(Webhook webhookBody)
    {
        if (webhookBody == null)
            throw new ArgumentNullException(nameof(webhookBody));

        // 1. Xác thực chữ ký số webhook SHA256 bằng PayOSClient
        WebhookData webhookData;
        try
        {
            webhookData = await _payOSClient.Webhooks.VerifyAsync(webhookBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xác thực chữ ký Webhook PayOS thất bại. Dữ liệu có thể bị giả mạo!");
            throw new UnauthorizedAccessException("Chữ ký Webhook không hợp lệ.", ex);
        }

        _logger.LogInformation(
            "Nhận Webhook PayOS hợp lệ: OrderCode={OrderCode}, Amount={Amount}, Code={Code}, Desc={Desc}",
            webhookData.OrderCode, webhookData.Amount, webhookData.Code, webhookData.Description);

        // Chỉ đọc dữ liệu định tuyến ở ngoài transaction. Việc quyết định ghi nhận
        // tiền luôn tải lại transaction + booking bên trong vùng khóa bảo vệ.
        var tx = await _context.PaymentTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.OrderCode == webhookData.OrderCode);

        if (tx == null)
        {
            _logger.LogWarning("Không tìm thấy GiaoDichThanhToan với OrderCode={OrderCode} trong hệ thống.", webhookData.OrderCode);
            return true; // Trả về true để PayOS không retry vô tận
        }

        if (tx.TransactionType == PaymentTransactionTypes.ScheduleChange)
        {
            return await _scheduleChanges.ProcessVerifiedPaymentAsync(
                webhookData.OrderCode, webhookData.Amount, webhookData.Code ?? webhookBody.Code,
                JsonSerializer.Serialize(webhookBody), CancellationToken.None);
        }

        if (PaymentTransactionStatusCodes.PaymentReceived.Contains(tx.TrangThai))
        {
            _logger.LogInformation(
                "Giao dịch OrderCode={OrderCode} đã ở trạng thái kết thúc {Status}. Bỏ qua webhook trùng lặp.",
                webhookData.OrderCode, tx.TrangThai);
            return true;
        }

        // 4. Kiểm tra mã phản hồi thành công từ PayOS (code == "00")
        if (webhookData.Code == "00" || webhookBody.Code == "00")
        {
            if (tx.TransactionType == PaymentTransactionTypes.Installment || tx.TransactionType == PaymentTransactionTypes.Settlement)
            {
                await CompleteContractPaymentTransactionAsync(tx, webhookData.OrderCode, JsonSerializer.Serialize(webhookBody));
                return true;
            }

            await ProcessConfirmedDepositAsync(
                webhookData.OrderCode,
                webhookData.Amount,
                JsonSerializer.Serialize(webhookBody));
            return true;
        }

        // Không cập nhật lùi giao dịch đã được xử lý bởi một request đồng thời.
        await MarkFailedIfUnprocessedAsync(
            webhookData.OrderCode,
            JsonSerializer.Serialize(webhookBody));
        return true;
    }

    public async Task<PaymentStatusResponseDto> GetPaymentStatusAsync(long orderCode)
    {
        var tx = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(t => t.Booking)
            .SingleOrDefaultAsync(t => t.OrderCode == orderCode);

        if (tx == null)
        {
            return new PaymentStatusResponseDto
            {
                OrderCode = orderCode,
                Status = "NOT_FOUND",
                Message = "Không tìm thấy thông tin giao dịch."
            };
        }

        // Fallback kiểm tra trực tiếp PayOS nếu vẫn PENDING sau một khoảng thời gian
        // (Đặc biệt hữu ích khi chạy Localhost chưa bật Webhook qua Ngrok)
        if (tx.TrangThai == PaymentTransactionStatusCodes.Pending)
        {
            try
            {
                PaymentLink linkInfo = tx.TransactionType == PaymentTransactionTypes.Deposit
                    ? await _depositPaymentGateway.GetAsync(orderCode)
                    : await _payOSClient.PaymentRequests.GetAsync(orderCode);
                var statusStr = linkInfo?.Status.ToString();
                if (linkInfo != null && string.Equals(statusStr, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Fallback Sync: PayOS báo PAID cho OrderCode={OrderCode}, tự động hoàn tất giao dịch.", orderCode);
                    if (tx.TransactionType == PaymentTransactionTypes.Installment || tx.TransactionType == PaymentTransactionTypes.Settlement)
                    {
                        await CompleteContractPaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                    }
                    else
                    {
                        await ProcessConfirmedDepositAsync(
                            orderCode,
                            linkInfo.Amount,
                            "Auto-polled from PayOS PaymentRequests.GetAsync");
                    }
                }
                else if (linkInfo != null && string.Equals(statusStr, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    await MarkCancelledIfPendingAsync(orderCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Kiểm tra PayOS fallback cho OrderCode={OrderCode} chưa có kết quả mới.", orderCode);
            }
        }

        tx = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(t => t.Booking)
            .SingleAsync(t => t.OrderCode == orderCode);

        return new PaymentStatusResponseDto
        {
            OrderCode = tx.OrderCode,
            BookingId = tx.DatTiecId,
            BookingCode = tx.Booking.BookingCode,
            Status = tx.TrangThai,
            BusinessDeposit = tx.SoTienGhiNhan,
            ActualAmount = tx.SoTienGiaoDich,
            PaidAt = tx.NgayThanhToan,
            Message = tx.TrangThai == PaymentTransactionStatusCodes.Paid
                ? "Thanh toán đặt cọc thành công!"
                : tx.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired
                    ? tx.ReconciliationData?.Contains(
                        PaymentReconciliationReasons.PaidAfterBookingCancelled,
                        StringComparison.Ordinal) == true
                        ? "Thanh toán được ghi nhận sau khi đơn đặt tiệc đã hủy. Giao dịch đang chờ bộ phận quản lý đối soát."
                        : "Thanh toán đã được PayOS ghi nhận nhưng cần bộ phận quản lý đối soát trước khi cập nhật đơn."
                    : (tx.TrangThai == PaymentTransactionStatusCodes.Cancelled ? "Giao dịch đã bị hủy." : "Đang chờ thanh toán...")
        };
    }

    private async Task MarkFailedIfUnprocessedAsync(long orderCode, string payload)
    {
        if (_context.Database.IsRelational())
        {
            await _context.PaymentTransactions
                .Where(item => item.OrderCode == orderCode &&
                    item.TrangThai != PaymentTransactionStatusCodes.Paid &&
                    item.TrangThai != PaymentTransactionStatusCodes.Cancelled &&
                    item.TrangThai != PaymentTransactionStatusCodes.ReconciliationRequired)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.TrangThai, PaymentTransactionStatusCodes.Failed)
                    .SetProperty(item => item.PayloadWebhook, payload));
            return;
        }

        var current = await _context.PaymentTransactions.SingleAsync(item => item.OrderCode == orderCode);
        if (!PaymentTransactionStatusCodes.Terminal.Contains(current.TrangThai))
        {
            current.TrangThai = PaymentTransactionStatusCodes.Failed;
            current.PayloadWebhook = payload;
            await _context.SaveChangesAsync();
        }
    }

    private async Task MarkCancelledIfPendingAsync(long orderCode)
    {
        if (_context.Database.IsRelational())
        {
            await _context.PaymentTransactions
                .Where(item => item.OrderCode == orderCode &&
                    (item.TrangThai == PaymentTransactionStatusCodes.Creating ||
                     item.TrangThai == PaymentTransactionStatusCodes.Pending))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.TrangThai, PaymentTransactionStatusCodes.Cancelled));
            return;
        }

        var pending = await _context.PaymentTransactions.SingleOrDefaultAsync(item =>
            item.OrderCode == orderCode &&
            (item.TrangThai == PaymentTransactionStatusCodes.Creating ||
             item.TrangThai == PaymentTransactionStatusCodes.Pending));
        if (pending is not null)
        {
            pending.TrangThai = PaymentTransactionStatusCodes.Cancelled;
            await _context.SaveChangesAsync();
        }
    }

    public async Task CancelPendingDepositLinksAsync(
        int bookingId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // This method must be called only after the booking cancellation transaction
        // commits. Network I/O is deliberately kept outside SQL transactions/locks.
        List<PaymentTransactionEntity> pendingLinks;
        try
        {
            pendingLinks = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(item => item.DatTiecId == bookingId &&
                    item.TransactionType == PaymentTransactionTypes.Deposit &&
                    (item.TrangThai == PaymentTransactionStatusCodes.Creating ||
                     item.TrangThai == PaymentTransactionStatusCodes.Pending))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Không thể tải danh sách link PayOS cần hủy cho BookingId={BookingId}. Booking vẫn giữ trạng thái đã hủy.",
                bookingId);
            return;
        }

        foreach (var pendingLink in pendingLinks)
            await CancelDepositLinkAndRecordAsync(
                bookingId,
                pendingLink.GiaoDichId,
                pendingLink.OrderCode,
                string.IsNullOrWhiteSpace(reason) ? "Booking cancelled" : reason,
                cancellationToken);
    }

    private static void EnsureDepositPaymentAccess(
        WeddingBooking booking,
        CreateDepositPaymentRequestDto request,
        int? userId)
    {
        if (userId is > 0)
        {
            if (booking.Customer.UserId != userId.Value &&
                !IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thanh toán cho đơn tiệc này.");
            return;
        }

        if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
            throw new UnauthorizedAccessException("Thông tin mã đặt tiệc hoặc số điện thoại không khớp với hồ sơ.");
    }

    private async Task EnsureBookingCanCreateDepositAsync(WeddingBooking booking)
    {
        if (await BookingPaymentConcurrency.HasRecordedDepositAsync(
                _context, booking.BookingId, booking.PaidAmount))
            throw new InvalidOperationException("Đơn đặt tiệc này đã được ghi nhận thanh toán hoặc đang chờ đối soát tiền cọc.");

        if (booking.Status.StatusCode is not (BookingStatusCodes.Pending or BookingStatusCodes.Confirmed))
            throw new InvalidOperationException(
                $"Chỉ có thể đặt cọc khi tiệc ở trạng thái Chờ xác nhận hoặc Đã xác nhận (Hiện tại: {booking.Status.StatusName}).");
    }

    private int CalculateActualPayOsAmount(decimal businessAmount)
    {
        if (!_options.Value.IsDemoMode)
            return (int)businessAmount;

        var ratio = _options.Value.DemoScaleRatio > 0 ? _options.Value.DemoScaleRatio : 10000;
        return Math.Max(2000, (int)Math.Round(businessAmount / ratio, MidpointRounding.AwayFromZero));
    }

    private async Task<long> GenerateUniqueOrderCodeAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var orderCode = checked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L + Random.Shared.Next(1000));
            if (!await _context.PaymentTransactions.AnyAsync(item => item.OrderCode == orderCode))
                return orderCode;
        }

        throw new InvalidOperationException("Không thể sinh OrderCode PayOS duy nhất. Vui lòng thử lại.");
    }

    private async Task ResolveExpiredDepositIntentAsync(int bookingId)
    {
        PaymentTransactionEntity? expiredIntent;
        using (await BookingPaymentConcurrency.AcquireAsync(bookingId))
        await using (var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(_context))
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, bookingId);
            expiredIntent = await _context.PaymentTransactions
                .AsNoTracking()
                .Where(item => item.DatTiecId == bookingId &&
                    item.TransactionType == PaymentTransactionTypes.Deposit &&
                    (item.TrangThai == PaymentTransactionStatusCodes.Creating ||
                     item.TrangThai == PaymentTransactionStatusCodes.Pending) &&
                    item.ExpiresAt != null && item.ExpiresAt <= DateTime.Now)
                .OrderByDescending(item => item.NgayTao)
                .FirstOrDefaultAsync();
            await dbTransaction.CommitAsync();
        }

        if (expiredIntent is null)
            return;

        PaymentLink linkInfo;
        try
        {
            linkInfo = await _depositPaymentGateway.GetAsync(expiredIntent.OrderCode);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Giao dịch cọc cũ OrderCode={expiredIntent.OrderCode} đã hết hạn hiển thị nhưng chưa thể xác minh với PayOS. Không tạo link thay thế để tránh ghi nhận tiền trùng.", ex);
        }

        var status = linkInfo.Status.ToString();
        if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessConfirmedDepositAsync(
                expiredIntent.OrderCode,
                linkInfo.Amount,
                "Verified expired deposit intent via PayOS before replacement");
            return;
        }

        if (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            await MarkCancelledIfPendingAsync(expiredIntent.OrderCode);
            return;
        }

        if (string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
        {
            await MarkDepositIntentFailedAsync(
                bookingId,
                expiredIntent.GiaoDichId,
                PaymentReconciliationReasons.PayOsLinkExpired,
                $"PayOS status={status}");
            return;
        }

        throw new InvalidOperationException(
            $"Giao dịch cọc cũ OrderCode={expiredIntent.OrderCode} vẫn có trạng thái PayOS={status}. Không thể tạo thêm link cọc.");
    }

    private async Task MarkDepositIntentFailedAsync(
        int bookingId,
        long transactionId,
        string reason,
        string? error)
    {
        _context.ChangeTracker.Clear();
        using (await BookingPaymentConcurrency.AcquireAsync(bookingId))
        await using (var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(_context))
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, bookingId);
            var current = await _context.PaymentTransactions.SingleAsync(item => item.GiaoDichId == transactionId);
            if (current.TrangThai is PaymentTransactionStatusCodes.Creating or PaymentTransactionStatusCodes.Pending)
            {
                current.TrangThai = PaymentTransactionStatusCodes.Failed;
                current.ReconciliationData = JsonSerializer.Serialize(new
                {
                    reason,
                    error,
                    current.OrderCode,
                    recordedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            await dbTransaction.CommitAsync();
        }
    }

    private async Task<bool> HasOtherRecordedDepositAsync(
        WeddingBooking booking,
        long currentTransactionId,
        CancellationToken cancellationToken)
    {
        if (booking.PaidAmount > 0)
            return true;

        if (await _context.PaymentTransactions.AnyAsync(item =>
                item.DatTiecId == booking.BookingId &&
                item.GiaoDichId != currentTransactionId &&
                item.TransactionType == PaymentTransactionTypes.Deposit &&
                item.TrangThai == PaymentTransactionStatusCodes.Paid,
                cancellationToken))
            return true;

        return await _context.Payments.AnyAsync(item =>
            item.Contract.BookingId == booking.BookingId &&
            item.PaymentType == PaymentTypes.Deposit &&
            item.Status.StatusGroup == StatusGroups.Payment &&
            item.Status.StatusCode == PaymentStatusCodes.Success,
            cancellationToken);
    }

    private async Task CancelDepositLinkAndRecordAsync(
        int bookingId,
        long transactionId,
        long orderCode,
        string reason,
        CancellationToken cancellationToken)
    {
        if (await _context.PaymentTransactionEvents.AsNoTracking().AnyAsync(item =>
                item.PaymentTransactionId == transactionId &&
                item.EventType == PaymentTransactionEventTypes.PayOsCancelRequested,
                cancellationToken))
            return;

        string eventType;
        string? error = null;
        try
        {
            await _paymentLinkCancellation.CancelAsync(orderCode, reason, cancellationToken);
            eventType = PaymentTransactionEventTypes.PayOsCancelRequested;
        }
        catch (Exception ex)
        {
            eventType = PaymentTransactionEventTypes.PayOsCancelRequestFailed;
            error = ex.Message;
            _logger.LogWarning(ex,
                "Không thể gửi yêu cầu hủy PayOS OrderCode={OrderCode}, BookingId={BookingId}.",
                orderCode, bookingId);
        }

        await RecordPaymentTransactionEventAsync(
            bookingId, transactionId, orderCode, eventType, error, cancellationToken);
    }

    private async Task RecordPaymentTransactionEventAsync(
        int bookingId,
        long transactionId,
        long orderCode,
        string eventType,
        string? error,
        CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();
        using (await BookingPaymentConcurrency.AcquireAsync(bookingId, cancellationToken))
        await using (var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(
            _context, cancellationToken))
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, bookingId, cancellationToken);
            var current = await _context.PaymentTransactions
                .AsNoTracking()
                .SingleAsync(item => item.GiaoDichId == transactionId, cancellationToken);
            var alreadyRecorded = await _context.PaymentTransactionEvents.AnyAsync(item =>
                item.PaymentTransactionId == transactionId && item.EventType == eventType,
                cancellationToken);
            if (!alreadyRecorded)
            {
                _context.PaymentTransactionEvents.Add(new PaymentTransactionEvent
                {
                    PaymentTransactionId = transactionId,
                    EventType = eventType,
                    OccurredAt = DateTime.Now,
                    Details = JsonSerializer.Serialize(new
                    {
                        bookingId,
                        orderCode,
                        transactionStatus = current.TrangThai,
                        error
                    })
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
            await dbTransaction.CommitAsync(cancellationToken);
        }
    }

    public async Task ProcessConfirmedDepositAsync(
        long orderCode,
        decimal actualAmount,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var route = await _context.PaymentTransactions
            .AsNoTracking()
            .Where(item => item.OrderCode == orderCode)
            .Select(item => new { item.DatTiecId, item.TransactionType })
            .SingleOrDefaultAsync(cancellationToken);
        if (route is null)
            return;
        if (route.TransactionType != PaymentTransactionTypes.Deposit)
            throw new InvalidOperationException("Chỉ giao dịch DEPOSIT được xử lý bởi luồng xác nhận đặt cọc.");

        using var bookingLock = await BookingPaymentConcurrency.AcquireAsync(route.DatTiecId, cancellationToken);
        await using var dbTransaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(_context, cancellationToken);
        try
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, route.DatTiecId, cancellationToken);

            var tx = await _context.PaymentTransactions
                .SingleAsync(item => item.OrderCode == orderCode, cancellationToken);

            // Idempotency is checked after taking the booking/database lock.
            if (PaymentTransactionStatusCodes.PaymentReceived.Contains(tx.TrangThai))
            {
                await dbTransaction.CommitAsync(cancellationToken);
                return;
            }

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.Menu)
                .Include(b => b.DecorationPackage)
                .Include(b => b.Contract)
                .SingleAsync(b => b.BookingId == tx.DatTiecId, cancellationToken);

            var duplicateDeposit = await HasOtherRecordedDepositAsync(
                booking, tx.GiaoDichId, cancellationToken);
            var reconciliationReason = booking.Status.StatusCode == BookingStatusCodes.Cancelled
                ? PaymentReconciliationReasons.PaidAfterBookingCancelled
                : duplicateDeposit
                    ? PaymentReconciliationReasons.DuplicateDepositPayment
                : actualAmount != tx.SoTienGiaoDich
                    ? PaymentReconciliationReasons.PaymentAmountMismatch
                    : booking.Status.StatusCode is not (BookingStatusCodes.Pending or BookingStatusCodes.Confirmed)
                        ? PaymentReconciliationReasons.BookingStatusNotEligible
                        : null;

            if (reconciliationReason is not null)
            {
                var previousTransactionStatus = tx.TrangThai;
                tx.TrangThai = PaymentTransactionStatusCodes.ReconciliationRequired;
                tx.NgayThanhToan = DateTime.Now;
                tx.PayloadWebhook = payload;
                tx.ReconciliationData = JsonSerializer.Serialize(new
                {
                    reason = reconciliationReason,
                    orderCode,
                    expectedAmount = tx.SoTienGiaoDich,
                    actualAmount,
                    bookingStatus = booking.Status.StatusCode,
                    recordedAt = DateTime.Now
                });

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = null,
                    Action = AuditActions.Payment,
                    EntityName = AuditEntityNames.Booking,
                    EntityId = booking.BookingId,
                    OldData = $"OrderCode={orderCode};TransactionStatus={previousTransactionStatus}",
                    NewData = $"OrderCode={orderCode};TransactionStatus={PaymentTransactionStatusCodes.ReconciliationRequired};Reason={reconciliationReason}",
                    Timestamp = DateTime.Now,
                    Outcome = AuditOutcomes.Blocked,
                    Notes = reconciliationReason == PaymentReconciliationReasons.PaidAfterBookingCancelled
                        ? "PayOS xác nhận đã nhận tiền sau khi booking bị hủy. Không phục hồi booking; chuyển quản lý đối soát."
                        : "PayOS xác nhận đã nhận tiền nhưng giao dịch không đủ điều kiện ghi nhận nghiệp vụ; chuyển quản lý đối soát."
                });

                await _context.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);
                _logger.LogWarning(
                    "Giao dịch OrderCode={OrderCode} chuyển đối soát. Reason={Reason}, BookingId={BookingId}",
                    orderCode, reconciliationReason, booking.BookingId);
                return;
            }

            tx.TrangThai = PaymentTransactionStatusCodes.Paid;
            tx.NgayThanhToan = DateTime.Now;
            tx.PayloadWebhook = payload;

            // Cập nhật DatTiec: Trạng thái -> CONFIRMED, Tiền cọc & Số tiền đã thanh toán ghi nhận đủ cọc thật
            var confirmedStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Confirmed);
            booking.StatusId = confirmedStatusId;
            booking.DepositAmount = tx.SoTienGhiNhan;
            booking.PaidAmount = booking.PaidAmount + tx.SoTienGhiNhan;

            // Khóa chính thức lịch sảnh: LichSanh -> BOOKED (TrangThaiID = 302)
            if (booking.HallSchedule != null)
            {
                var bookedScheduleId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
                booking.HallSchedule.StatusId = bookedScheduleId;
            }

            // Đồng bộ hợp đồng (HopDong) và biên lai thanh toán (ThanhToan) để hiển thị trong Manager/Admin
            if (booking.Contract == null)
            {
                var draftContractStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Draft);
                var contract = new Contract
                {
                    BookingId = booking.BookingId,
                    ContractCode = $"HD-{booking.BookingCode}",
                    CreatedDate = DateTime.Today,
                    TotalValue = booking.EstimatedTotal ?? tx.SoTienGhiNhan,
                    PaymentTerms = "Đặt cọc 20% trực tuyến qua VietQR PayOS",
                    StatusId = draftContractStatusId
                };
                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();
                booking.Contract = contract;
            }

            var paymentSuccessStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success);
            var payment = await _context.Payments
                .SingleOrDefaultAsync(item => item.TransactionCode == orderCode.ToString(), cancellationToken);
            if (payment is null)
            {
                payment = new Payment
                {
                    ContractId = booking.Contract.ContractId,
                    PaymentType = PaymentTypes.Deposit,
                    Amount = tx.SoTienGhiNhan,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VIETQR_PAYOS",
                    TransactionCode = orderCode.ToString(),
                    StatusId = paymentSuccessStatusId
                };
                _context.Payments.Add(payment);
            }

            // Ghi nhật ký hệ thống (AuditLog)
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = AuditActions.Payment,
                EntityName = AuditEntityNames.Booking,
                EntityId = booking.BookingId,
                OldData = $"BookingCode={booking.BookingCode};TrangThai=PENDING;PaidAmount=0",
                NewData = $"BookingCode={booking.BookingCode};TrangThai=CONFIRMED;PaidAmount={booking.PaidAmount};OrderCode={orderCode}",
                Timestamp = DateTime.Now,
                Outcome = AuditOutcomes.Success,
                Notes = $"Khách hàng đã thanh toán cọc thành công {tx.SoTienGhiNhan:N0} đ qua VietQR PayOS. Lịch sảnh đã được khóa chính thức."
            });

            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Đã hoàn tất thanh toán cọc thành công: BookingCode={BookingCode}, OrderCode={OrderCode}, Số tiền ghi nhận={Amount:N0} VNĐ",
                booking.BookingCode, orderCode, tx.SoTienGhiNhan);

            // Gửi email xác nhận đặt cọc thành công kèm Hóa đơn / Biên lai điện tử có chữ ký số (PAdES)
            if (!string.IsNullOrWhiteSpace(booking.Customer.Email))
            {
                try
                {
                        var remaining = Math.Max(0, (booking.EstimatedTotal ?? tx.SoTienGhiNhan) - booking.PaidAmount);
                        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
                        {
                            InvoiceNumber = $"HDDT-{booking.BookingCode}-01",
                            BookingCode = booking.BookingCode,
                            CustomerName = booking.Customer.FullName,
                            CustomerPhone = booking.Customer.PhoneNumber,
                            CustomerEmail = booking.Customer.Email ?? string.Empty,
                            PaymentType = "Đặt cọc (20%)",
                            PaymentMethod = "VIETQR_PAYOS",
                            TransactionCode = orderCode.ToString(),
                            InvoiceDate = DateTime.Now,
                            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
                            Shift = booking.HallSchedule?.Shift ?? "Tối",
                            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                            GuestCount = booking.GuestCount,
                            TotalTables = (booking.GuestCount + 9) / 10,
                            MenuName = booking.Menu?.MenuName,
                            DecorName = booking.DecorationPackage?.TenGoi,
                            TotalEstimatedCost = booking.EstimatedTotal ?? tx.SoTienGhiNhan,
                            AmountPaid = tx.SoTienGhiNhan,
                            AccumulatedPaid = booking.PaidAmount,
                            RemainingDebt = remaining,
                            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
                            IsDigitallySigned = true,
                            SignedAt = DateTime.Now
                        };

                        bool isFullSettlement = remaining <= 0 || booking.PaidAmount >= (booking.EstimatedTotal ?? tx.SoTienGhiNhan);
                        if (isFullSettlement)
                        {
                            invoiceDto.PaymentType = "Thanh toán toàn bộ (100%)";
                        }

                        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
                        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

                        if (isFullSettlement)
                        {
                            await _emailService.SendFullPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                booking.PaidAmount,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}.pdf");
                        }
                        else
                        {
                            await _emailService.SendDepositPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                orderCode,
                                tx.SoTienGhiNhan,
                                remaining,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"BienLaiDatCoc_{booking.BookingCode}.pdf");
                        }
                }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Không thể gửi email xác nhận đặt cọc thành công cho {Email}", booking.Customer.Email);
                }
            }
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi cập nhật CSDL sau khi thanh toán cọc OrderCode={OrderCode}", orderCode);
            throw;
        }
    }

    private static bool IsGuestVerified(WeddingBooking booking, string? inputCode, string? inputPhone)
    {
        if (string.IsNullOrWhiteSpace(inputCode) || string.IsNullOrWhiteSpace(inputPhone))
            return false;

        var cleanCode = inputCode.Trim().ToUpperInvariant();
        var cleanPhone = inputPhone.Trim();

        return booking.BookingCode.Equals(cleanCode, StringComparison.OrdinalIgnoreCase) &&
               booking.Customer.PhoneNumber.Equals(cleanPhone, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(byte[] PdfBytes, string FileName, InvoiceSignatureResult Signature)> GetSignedInvoicePdfAsync(string bookingCode, string phone)
    {
        if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Mã đặt tiệc và số điện thoại không được để trống.");

        var cleanCode = bookingCode.Trim().ToUpperInvariant();
        var cleanPhone = phone.Trim();

        var booking = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.Menu)
            .Include(b => b.DecorationPackage)
            .Include(b => b.Contract).ThenInclude(c => c!.Payments)
            .Include(b => b.PaymentTransactions)
            .SingleOrDefaultAsync(b => b.BookingCode == cleanCode);

        if (booking == null)
            throw new KeyNotFoundException($"Không tìm thấy đơn đặt tiệc với mã {cleanCode}.");

        if (!booking.Customer.PhoneNumber.Equals(cleanPhone, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Số điện thoại không khớp với thông tin đơn đặt tiệc.");

        var latestPayment = booking.Contract?.Payments?
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault();

        var latestTx = booking.PaymentTransactions?
            .OrderByDescending(t => t.NgayTao)
            .FirstOrDefault(t => t.TrangThai == "SUCCESS");

        var amountPaid = booking.PaidAmount > 0
            ? (latestPayment?.Amount ?? latestTx?.SoTienGhiNhan ?? booking.PaidAmount)
            : 0;

        var remaining = Math.Max(0, (booking.EstimatedTotal ?? amountPaid) - booking.PaidAmount);
        var paymentDate = latestPayment?.PaymentDate ?? latestTx?.NgayTao ?? DateTime.Now;
        var orderCodeStr = latestPayment?.TransactionCode ?? latestTx?.OrderCode.ToString() ?? booking.BookingCode;

        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
        {
            InvoiceNumber = $"HDDT-{booking.BookingCode}-01",
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer.FullName,
            CustomerPhone = booking.Customer.PhoneNumber,
            CustomerEmail = booking.Customer.Email ?? string.Empty,
            PaymentType = booking.PaidAmount >= (booking.EstimatedTotal ?? 0) && booking.PaidAmount > 0
                ? "Thanh toán toàn bộ (100%)"
                : "Đặt cọc (20%)",
            PaymentMethod = latestPayment?.PaymentMethod ?? "VIETQR_PAYOS",
            TransactionCode = orderCodeStr,
            InvoiceDate = paymentDate,
            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
            Shift = booking.HallSchedule?.Shift ?? "Tối",
            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
            GuestCount = booking.GuestCount,
            TotalTables = (booking.GuestCount + 9) / 10,
            MenuName = booking.Menu?.MenuName,
            DecorName = booking.DecorationPackage?.TenGoi,
            TotalEstimatedCost = booking.EstimatedTotal ?? amountPaid,
            AmountPaid = amountPaid,
            AccumulatedPaid = booking.PaidAmount,
            RemainingDebt = remaining,
            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
            IsDigitallySigned = true,
            SignedAt = DateTime.Now
        };

        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

        return (signResult.SignedPdfBytes, $"HoaDonDienTu_{booking.BookingCode}.pdf", signResult);
    }
    public async Task<CreateContractPayOsLinkResponseDto> CreateContractPaymentAsync(
        CreateContractPayOsLinkRequestDto request,
        int? managerUserId = null)
    {
        if (request.ContractId <= 0)
            throw new ArgumentException("Mã hợp đồng không hợp lệ.", nameof(request.ContractId));

        if (request.Amount <= 0)
            throw new ArgumentException("Số tiền thanh toán phải lớn hơn 0.", nameof(request.Amount));

        var validTypes = new[] { PaymentTypes.Installment, PaymentTypes.Settlement };
        var normalizedType = validTypes.FirstOrDefault(t => string.Equals(t, request.PaymentType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? PaymentTypes.Installment;

        var contract = await _context.Contracts
            .Include(c => c.Status)
            .Include(c => c.Booking).ThenInclude(b => b.Customer)
            .Include(c => c.Payments).ThenInclude(p => p.Status)
            .SingleOrDefaultAsync(c => c.ContractId == request.ContractId);

        if (contract == null)
            throw new KeyNotFoundException("Không tìm thấy hợp đồng này.");

        if (contract.Status.StatusCode != ContractStatusCodes.Active)
            throw new InvalidOperationException($"Chỉ có thể tạo thanh toán cho hợp đồng đang có hiệu lực (Hiện tại: {contract.Status.StatusName}).");

        // SEC-05: Kiểm tra trần số tiền thanh toán không được vượt quá số dư công nợ còn lại
        var paidSuccess = contract.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(p => p.Amount);
        var remainingBalance = Math.Max(0, contract.TotalValue - paidSuccess);

        if (remainingBalance <= 0)
            throw new InvalidOperationException("Hợp đồng này đã được thanh toán đầy đủ 100%, không còn số dư cần thanh toán.");

        if (request.Amount > remainingBalance)
            throw new InvalidOperationException($"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá số dư công nợ còn lại của hợp đồng ({remainingBalance:N0} đ).");

        if (request.Amount < 2000)
            throw new InvalidOperationException("Số tiền thanh toán tối thiểu là 2.000 đ.");

        decimal businessAmount = request.Amount;
        int actualAmount;
        if (_options.Value.IsDemoMode)
        {
            int ratio = _options.Value.DemoScaleRatio > 0 ? _options.Value.DemoScaleRatio : 10000;
            actualAmount = Math.Max(2000, (int)Math.Round(businessAmount / ratio, MidpointRounding.AwayFromZero));
        }
        else
        {
            actualAmount = (int)businessAmount;
        }

        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string typeShort = normalizedType == PaymentTypes.Installment ? "Dot 2" : "Quyet toan";
        string description = $"HD{contract.ContractId} {typeShort}";
        if (description.Length > 25) description = description.Substring(0, 25);

        var items = new List<PaymentLinkItem>
        {
            new PaymentLinkItem
            {
                Name = $"TT {contract.ContractCode}",
                Quantity = 1,
                Price = actualAmount
            }
        };

        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = actualAmount,
            Description = description,
            Items = items,
            CancelUrl = _options.Value.CancelUrl,
            ReturnUrl = _options.Value.ReturnUrl
        };

        CreatePaymentLinkResponse paymentResult;
        try
        {
            paymentResult = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PayOS SDK PaymentRequests.CreateAsync cho ContractId={ContractId}", contract.ContractId);
            throw new InvalidOperationException($"Không thể tạo liên kết VietQR lúc này: {ex.Message}", ex);
        }

        var txType = normalizedType == PaymentTypes.Installment
            ? PaymentTransactionTypes.Installment
            : PaymentTransactionTypes.Settlement;

        var transaction = new PaymentTransactionEntity
        {
            DatTiecId = contract.BookingId,
            TransactionType = txType,
            OrderCode = orderCode,
            SoTienGiaoDich = actualAmount,
            SoTienGhiNhan = businessAmount,
            PhuongThuc = "VIETQR_PAYOS",
            TrangThai = "PENDING",
            NgayTao = DateTime.Now
        };
        _context.PaymentTransactions.Add(transaction);

        var pendingStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Pending);
        var payment = new Payment
        {
            ContractId = contract.ContractId,
            PaymentType = normalizedType,
            Amount = businessAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "VIETQR_PAYOS",
            TransactionCode = orderCode.ToString(),
            StatusId = pendingStatusId
        };
        _context.Payments.Add(payment);

        await _context.SaveChangesAsync();

        // Gửi email thông báo thanh toán hợp đồng từ xa kèm liên kết VietQR PayOS cho khách hàng
        if (!string.IsNullOrWhiteSpace(contract.Booking?.Customer?.Email))
        {
            var customerEmail = contract.Booking.Customer.Email;
            var customerName = contract.Booking.Customer.FullName;
            var bookingCode = contract.Booking.BookingCode;
            var contractCode = contract.ContractCode;
            var checkoutUrl = paymentResult.CheckoutUrl ?? string.Empty;
            var eventDate = contract.Booking.HallSchedule?.Date ?? DateTime.Today.AddDays(30);
            var deadline = eventDate.AddDays(-7);
            var shift = contract.Booking.HallSchedule?.Shift ?? "Tối";
            var hallName = contract.Booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside";
            var customerPhone = contract.Booking.Customer.PhoneNumber;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendContractPaymentRequestEmailAsync(
                        customerEmail, customerName, bookingCode, contractCode,
                        normalizedType, businessAmount, checkoutUrl, deadline,
                        eventDate, shift, hallName, customerPhone);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email thông báo thanh toán hợp đồng cho {Email}", customerEmail);
                }
            });
        }

        return new CreateContractPayOsLinkResponseDto
        {
            OrderCode = orderCode,
            QrCode = paymentResult.QrCode ?? string.Empty,
            CheckoutUrl = paymentResult.CheckoutUrl ?? string.Empty,
            AccountNumber = paymentResult.AccountNumber,
            AccountName = paymentResult.AccountName,
            BankBin = paymentResult.Bin,
            BankName = paymentResult.Bin,
            Description = description,
            BusinessAmount = businessAmount,
            ActualAmount = actualAmount,
            IsDemoMode = _options.Value.IsDemoMode,
            ExpiresAt = DateTime.Now.AddMinutes(15)
        };
    }

    public async Task<PaymentStatusResponseDto> CheckContractPaymentStatusAsync(long orderCode)
    {
        var tx = await _context.PaymentTransactions
            .Include(t => t.Booking)
            .SingleOrDefaultAsync(t => t.OrderCode == orderCode);

        if (tx == null)
        {
            return new PaymentStatusResponseDto
            {
                OrderCode = orderCode,
                Status = "NOT_FOUND",
                Message = "Không tìm thấy thông tin giao dịch."
            };
        }

        if (tx.TrangThai == "PENDING")
        {
            try
            {
                PaymentLink linkInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);
                var statusStr = linkInfo?.Status.ToString();
                if (linkInfo != null && string.Equals(statusStr, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Fallback Sync Contract: PayOS báo PAID cho OrderCode={OrderCode}, tự động hoàn tất giao dịch.", orderCode);
                    await CompleteContractPaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                }
                else if (linkInfo != null && string.Equals(statusStr, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    tx.TrangThai = "CANCELLED";
                    var payment = await _context.Payments.SingleOrDefaultAsync(p => p.TransactionCode == orderCode.ToString());
                    if (payment != null)
                    {
                        var cancelledStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Cancelled);
                        payment.StatusId = cancelledStatusId;
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Kiểm tra PayOS fallback cho OrderCode={OrderCode} chưa có kết quả mới.", orderCode);
            }
        }

        return new PaymentStatusResponseDto
        {
            OrderCode = tx.OrderCode,
            BookingId = tx.DatTiecId,
            BookingCode = tx.Booking?.BookingCode ?? string.Empty,
            Status = tx.TrangThai,
            BusinessDeposit = tx.SoTienGhiNhan,
            ActualAmount = tx.SoTienGiaoDich,
            PaidAt = tx.NgayThanhToan,
            Message = tx.TrangThai == "PAID"
                ? "Thanh toán hợp đồng thành công!"
                : (tx.TrangThai == "CANCELLED" ? "Giao dịch đã bị hủy." : "Đang chờ thanh toán...")
        };
    }

    private async Task CompleteContractPaymentTransactionAsync(PaymentTransactionEntity tx, long orderCode, string payload)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            tx.TrangThai = "PAID";
            tx.NgayThanhToan = DateTime.Now;
            tx.PayloadWebhook = payload;

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.Menu)
                .Include(b => b.DecorationPackage)
                .Include(b => b.Contract).ThenInclude(c => c!.Payments)
                .SingleAsync(b => b.BookingId == tx.DatTiecId);

            booking.PaidAmount = (booking.PaidAmount) + tx.SoTienGhiNhan;

            var successStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success);
            var payment = await _context.Payments
                .SingleOrDefaultAsync(p => p.TransactionCode == orderCode.ToString());

            if (payment != null)
            {
                payment.StatusId = successStatusId;
                payment.PaymentDate = DateTime.Now;
            }
            else if (booking.Contract != null)
            {
                payment = new Payment
                {
                    ContractId = booking.Contract.ContractId,
                    PaymentType = tx.TransactionType == PaymentTransactionTypes.Settlement ? PaymentTypes.Settlement : PaymentTypes.Installment,
                    Amount = tx.SoTienGhiNhan,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VIETQR_PAYOS",
                    TransactionCode = orderCode.ToString(),
                    StatusId = successStatusId
                };
                _context.Payments.Add(payment);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = AuditActions.Payment,
                EntityName = AuditEntityNames.Contract,
                EntityId = booking.Contract?.ContractId ?? booking.BookingId,
                OldData = "OrderCode=" + orderCode + ";TrangThai=PENDING",
                NewData = "OrderCode=" + orderCode + ";TrangThai=PAID;Amount=" + tx.SoTienGhiNhan,
                Timestamp = DateTime.Now,
                Outcome = AuditOutcomes.Success,
                Notes = "Đã thanh toán thành công " + tx.SoTienGhiNhan.ToString("N0") + " đ qua VietQR PayOS cho hợp đồng."
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            _logger.LogInformation("Hoàn tất thanh toán hợp đồng OrderCode={OrderCode}, Số tiền={Amount:N0} đ", orderCode, tx.SoTienGhiNhan);

            // Gửi email xác nhận thanh toán hợp đồng kèm Hóa đơn điện tử có chữ ký số (PAdES)
            if (!string.IsNullOrWhiteSpace(booking.Customer?.Email))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var contractTotal = booking.Contract?.TotalValue ?? booking.EstimatedTotal ?? tx.SoTienGhiNhan;
                        var remaining = Math.Max(0, contractTotal - booking.PaidAmount);
                        var isFullSettlement = remaining <= 0 || booking.PaidAmount >= contractTotal;
                        var paymentTypeName = isFullSettlement
                            ? "Quyết toán (100%)"
                            : (tx.TransactionType == PaymentTransactionTypes.Settlement ? "Quyết toán sau tiệc" : "Thanh toán Đợt 2 (50%)");

                        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
                        {
                            InvoiceNumber = $"HDDT-{booking.BookingCode}-02",
                            BookingCode = booking.BookingCode,
                            CustomerName = booking.Customer.FullName,
                            CustomerPhone = booking.Customer.PhoneNumber,
                            CustomerEmail = booking.Customer.Email ?? string.Empty,
                            PaymentType = paymentTypeName,
                            PaymentMethod = "VIETQR_PAYOS",
                            TransactionCode = orderCode.ToString(),
                            InvoiceDate = DateTime.Now,
                            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
                            Shift = booking.HallSchedule?.Shift ?? "Tối",
                            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                            GuestCount = booking.GuestCount,
                            TotalTables = (booking.GuestCount + 9) / 10,
                            MenuName = booking.Menu?.MenuName,
                            DecorName = booking.DecorationPackage?.TenGoi,
                            TotalEstimatedCost = contractTotal,
                            AmountPaid = tx.SoTienGhiNhan,
                            AccumulatedPaid = booking.PaidAmount,
                            RemainingDebt = remaining,
                            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
                            IsDigitallySigned = true,
                            SignedAt = DateTime.Now
                        };

                        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
                        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

                        if (isFullSettlement)
                        {
                            await _emailService.SendFullPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                booking.PaidAmount,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}_QuyetToan.pdf");
                        }
                        else
                        {
                            await _emailService.SendContractPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                paymentTypeName,
                                tx.SoTienGhiNhan,
                                remaining,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}_Dot2.pdf");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email xác nhận thanh toán hợp đồng kèm chữ ký số cho BookingCode={BookingCode}", booking.BookingCode);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi hoàn tất thanh toán hợp đồng OrderCode={OrderCode}", orderCode);
            throw;
        }
    }

    public async Task<CreateContractPayOsLinkResponseDto> CreateCustomerContractPaymentAsync(
        CreateCustomerContractPaymentRequestDto request, int? userId = null)
    {
        var query = _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .Include(b => b.Contract).ThenInclude(c => c!.Status)
            .Include(b => b.Contract).ThenInclude(c => c!.Payments).ThenInclude(p => p.Status)
            .AsQueryable();

        WeddingBooking? booking;
        if (request.BookingId > 0)
            booking = await query.SingleOrDefaultAsync(b => b.BookingId == request.BookingId);
        else
            booking = await query.SingleOrDefaultAsync(b => b.BookingCode == request.BookingCode!.Trim());

        if (booking == null)
            throw new KeyNotFoundException("Không tìm thấy đơn đặt tiệc cưới này.");

        // Xác thực quyền sở hữu
        if (userId.HasValue && userId.Value > 0)
        {
            if (booking.Customer.UserId != userId.Value)
            {
                if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                    throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thanh toán cho đơn tiệc này.");
            }
        }
        else
        {
            if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                throw new UnauthorizedAccessException("Thông tin mã đặt tiệc hoặc số điện thoại không khớp với hồ sơ.");
        }

        if (booking.Contract == null)
            throw new InvalidOperationException("Đơn tiệc chưa có hợp đồng để thanh toán.");

        if (booking.Contract.Status != null && booking.Contract.Status.StatusCode != ContractStatusCodes.Active)
            throw new InvalidOperationException($"Chỉ có thể thanh toán cho hợp đồng đang có hiệu lực (Hiện tại: {booking.Contract.Status.StatusName}).");

        var paidSuccess = booking.Contract.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(p => p.Amount);
        var remainingBalance = Math.Max(0, booking.Contract.TotalValue - paidSuccess);

        if (remainingBalance <= 0)
            throw new InvalidOperationException("Hợp đồng này đã được thanh toán đầy đủ 100%, không còn số dư cần thanh toán.");

        // Mặc định Đợt 2 là 50% tổng giá trị hợp đồng (hoặc toàn bộ số dư còn lại nếu còn ít hơn)
        decimal amountToPay;
        if (request.Amount.HasValue && request.Amount.Value > 0)
        {
            if (request.Amount.Value > remainingBalance)
                throw new InvalidOperationException($"Số tiền thanh toán ({request.Amount.Value:N0} đ) vượt quá số dư công nợ còn lại của hợp đồng ({remainingBalance:N0} đ).");
            amountToPay = request.Amount.Value;
        }
        else
        {
            amountToPay = Math.Min(remainingBalance, Math.Round(booking.Contract.TotalValue * 0.50m, 0));
        }

        var contractRequest = new CreateContractPayOsLinkRequestDto
        {
            ContractId = booking.Contract.ContractId,
            Amount = amountToPay,
            PaymentType = PaymentTypes.Installment
        };

        return await CreateContractPaymentAsync(contractRequest, userId);
    }
}

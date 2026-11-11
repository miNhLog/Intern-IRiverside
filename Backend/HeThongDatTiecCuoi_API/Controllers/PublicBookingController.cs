using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HeThongDatTiecCuoi_API.Controllers;

/// <summary>
/// UC14 &amp; UC15: Tra cứu + hủy giữ chỗ tiệc cưới trực tuyến (public, không cần đăng nhập).
/// Route mới /api/v1/public/bookings/* — giữ nguyên route cũ /api/my-bookings để tương thích.
/// </summary>
[ApiController]
[Route("api/v1/public/bookings")]
[AllowAnonymous]
public sealed class PublicBookingController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailedWindow = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PublicBookingController> _logger;
    private readonly ICoordinationAssignmentService _coordinationAssignments;
    private readonly IContractPdfGenerator _contractPdfGenerator;
    private readonly IPaymentService? _paymentService;

    public PublicBookingController(
        ApplicationDbContext context,
        IStatusService statusService,
        IMemoryCache cache,
        ILogger<PublicBookingController> logger,
        ICoordinationAssignmentService coordinationAssignments, IContractPdfGenerator contractPdfGenerator,
        IPaymentService? paymentService = null)
    {
        _context = context;
        _statusService = statusService;
        _cache = cache;
        _logger = logger;
        _coordinationAssignments = coordinationAssignments;
        _contractPdfGenerator = contractPdfGenerator;
        _paymentService = paymentService;
    }

    /// <summary>UC14: POST /api/v1/public/bookings/lookup — tra cứu bằng mã + email hoặc SĐT.</summary>
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] PublicBookingLookupRequestDto? request)
    {
        try
        {
            if (request is null)
                return BadRequest(new { success = false, message = "Thiếu dữ liệu tra cứu." });

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateKey = $"public-lookup-fail:{clientIp}";
            if (_cache.TryGetValue<int>(rateKey, out var fails) && fails >= MaxFailedAttempts)
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    success = false,
                    message = "Bạn đã tra cứu sai quá 5 lần trong 15 phút. Vui lòng thử lại sau."
                });

            var code = request.BookingCode.Trim().ToUpperInvariant();
            var contact = request.PhoneNumber.Trim();
            var normalizedEmail = contact.ToLowerInvariant();
            var lookupByEmail = contact.Contains('@');
            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync();

            var booking = await _context.WeddingBookings.AsNoTracking()
                .AsSplitQuery()
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .Include(b => b.Menu!).ThenInclude(m => m!.MenuDishes).ThenInclude(md => md.Dish)
                .Include(b => b.DecorationPackage)
                .Include(b => b.BookingServices).ThenInclude(bs => bs.ServiceItem)
                .Include(b => b.Contract!).ThenInclude(c => c!.Payments).ThenInclude(p => p.Status)
                .Include(b => b.Contract!).ThenInclude(c => c!.Status)
                .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Coordinator)
                .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Status)
                .SingleOrDefaultAsync(b =>
                    b.BookingCode == code &&
                    (lookupByEmail ? b.Customer.Email != null && b.Customer.Email.ToLower() == normalizedEmail
                        : b.Customer.PhoneNumber == contact) &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
            {
                var next = fails + 1;
                _cache.Set(rateKey, next, FailedWindow);
                return NotFound(new
                {
                    success = false,
                    message = "Mã đặt tiệc hoặc email/số điện thoại không chính xác."
                });
            }

            // Tra cứu đúng -> reset đếm sai của IP này.
            _cache.Remove(rateKey);

            var ledger = booking.Contract is null
                ? null
                : await new Uc18FinancialLedger(_context).ReadAsync(booking.Contract);
            var detail = BuildDetailDto(booking, ledger);
            return Ok(new { success = true, data = detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PublicBooking lookup thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể tra cứu lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>UC15: POST /api/v1/public/bookings/{id}/cancel-hold — hủy giữ chỗ online.</summary>
    [HttpPost("{id:int}/cancel-hold")]
    public async Task<IActionResult> CancelHold(int id, [FromBody] PublicBookingCancelHoldRequestDto? request)
    {
        using var bookingLock = await BookingPaymentConcurrency.AcquireAsync(id, HttpContext.RequestAborted);
        await using var transaction = await BookingPaymentConcurrency.BeginSerializableTransactionAsync(
            _context, HttpContext.RequestAborted);
        try
        {
            await BookingPaymentConcurrency.AcquireDatabaseLockAsync(_context, id, HttpContext.RequestAborted);
            if (request is null)
                return BadRequest(new { success = false, message = "Thiếu dữ liệu hủy giữ chỗ." });
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);
            if (request.Reason?.Trim().Length > 500)
                return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

            var code = request.BookingCode.Trim().ToUpperInvariant();
            var contact = request.PhoneNumber.Trim();
            var normalizedEmail = contact.ToLowerInvariant();
            var lookupByEmail = contact.Contains('@');
            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync();

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .SingleOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.BookingCode == code &&
                    (lookupByEmail ? b.Customer.Email != null && b.Customer.Email.ToLower() == normalizedEmail
                        : b.Customer.PhoneNumber == contact) &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
                return NotFound(new
                {
                    success = false,
                    message = "Mã đặt tiệc hoặc email/số điện thoại không chính xác."
                });

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                booking.Status.StatusCode != BookingStatusCodes.Pending)
                return BadRequest(new
                {
                    success = false,
                    code = "ERR_BOOKING_CANNOT_CANCEL",
                    message = "Chỉ có thể hủy online khi tiệc đang ở trạng thái Chờ xác nhận. Tiệc đã xác nhận/cọc vui lòng liên hệ nhân viên."
                });

            var paymentAlreadyReceived = await BookingPaymentConcurrency.HasRecordedDepositAsync(
                _context, booking.BookingId, booking.PaidAmount, HttpContext.RequestAborted);
            if (paymentAlreadyReceived)
                return Conflict(new
                {
                    success = false,
                    code = "ERR_PAID_BOOKING_REQUIRES_MANAGED_CANCELLATION",
                    message = "Khoản cọc đã được ghi nhận. Vui lòng liên hệ quản lý để thực hiện quy trình hủy booking đã thanh toán."
                });

            var oldStatusName = booking.Status.StatusName;
            booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled);
            booking.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách hủy giữ chỗ online"
                : request.Reason.Trim();
            await _coordinationAssignments.SynchronizeBookingAssignmentsAsync(booking.BookingId,
                BookingStatusCodes.Cancelled, null, booking.CancellationReason, HttpContext.RequestAborted);

            // Giải phóng lịch sảnh về TRỐNG (AVAILABLE) theo BR03 trong cùng transaction.
            // Dùng AVAILABLE qua IStatusService đúng domain HALL_SCHEDULE (không hardcode số).
            // Bỏ qua nếu schedule null (default-available: không có dòng vẫn coi trống).
            // LƯU Ý: TrangThaiID=401 là MENU/Đang áp dụng, KHÔNG phải LichSanh —
            // dùng 401 cho LichSanh sẽ sai domain, nên dùng AVAILABLE qua IStatusService.
            var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
                item.BookingId != booking.BookingId &&
                item.HallScheduleId == booking.HallScheduleId &&
                item.Status != null &&
                item.Status.StatusGroup == StatusGroups.Booking &&
                BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
            var hasActiveScheduleHold = await _context.ScheduleChangeHolds.AnyAsync(hold =>
                hold.HallScheduleId == booking.HallScheduleId &&
                hold.Status == ScheduleChangeHoldStatusCodes.Active &&
                hold.ExpiresAt > RiversideBusinessClock.Now);
            if (!hasOtherActiveBooking && !hasActiveScheduleHold && booking.HallSchedule is not null)
                booking.HallSchedule.StatusId =
                    await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = AuditActions.BookingHoldCancelled,
                EntityName = AuditEntityNames.Booking,
                EntityId = booking.BookingId,
                OldData = $"MaDatTiec={booking.BookingCode};TrangThai={oldStatusName}",
                NewData = $"MaDatTiec={booking.BookingCode};TrangThai=Đã hủy;LyDoHuy={booking.CancellationReason}",
                Timestamp = DateTime.Now,
                Outcome = AuditOutcomes.Success,
                Notes = "Khách hủy giữ chỗ online (public cancel-hold)"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            bookingLock.Dispose();
            if (_paymentService is not null)
                await _paymentService.CancelPendingDepositLinksAsync(
                    booking.BookingId, booking.CancellationReason, CancellationToken.None);
            return Ok(new
            {
                success = true,
                message = "Đã hủy giữ chỗ thành công và giải phóng lịch sảnh."
            });
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { }
            _logger.LogError(ex, "PublicBooking cancel-hold thất bại (id={Id}). traceId={TraceId}. Chi tiết: {Detail}",
                id, HttpContext.TraceIdentifier, ex.ToString());
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể hủy giữ chỗ lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private static PublicBookingDetailDto BuildDetailDto(WeddingBooking booking, Uc18FinancialLedgerSnapshot? ledger)
    {
        var official = (int)Math.Ceiling(booking.GuestCount / 10m);
        // LỖI B: khôi phục bàn dự phòng từ marker trong SpecialRequests
        // (RecommendationController lưu "[Bàn dự phòng: N]"). DatTiec không có cột riêng
        // nên không được hardcode spare=0 — phải dùng giá trị thật để khớp 100% với lúc đặt.
        var spare = TryParseSpareTables(booking.SpecialRequests);
        var totalTables = official + spare;

        var dishes = booking.Menu?.MenuDishes
            .OrderBy(md => md.SortOrder)
            .Select(md => new PublicBookingDishDto
            {
                DishId = md.Dish.DishId,
                DishCode = md.Dish.DishCode,
                DishName = md.Dish.DishName,
                Category = md.Dish.Category,
                SortOrder = md.SortOrder
            }).ToList() ?? [];

        var services = booking.BookingServices
            .OrderBy(bs => bs.ServiceItem.TenDichVu)
            .Select(bs => new PublicBookingServiceDto
            {
                ServiceId = bs.ServiceId,
                ServiceCode = bs.ServiceItem.MaDichVu,
                ServiceName = bs.ServiceItem.TenDichVu,
                Quantity = bs.Quantity,
                UnitPrice = bs.FinalUnitPrice,
                LineTotal = bs.FinalUnitPrice * bs.Quantity
            }).ToList();

        var total = booking.EstimatedTotal
            ?? (booking.FinalHallPrice ?? 0m)
                + (booking.FinalMenuPrice ?? 0m) * totalTables
                + (booking.FinalDecorationPrice ?? 0m)
                + services.Sum(s => s.LineTotal);

        // Đã cọc = tổng ThanhToan SUCCESS của HopDong (mọi loại: Đặt cọc/đợt/quyết toán).
        var grossPaid = booking.Contract?.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == "SUCCESS")
            .Sum(p => p.Amount) ?? 0m;
        var deposit = ledger is null ? grossPaid : ledger.Receipts.GrossCollected - ledger.Receipts.TotalPrincipalReturned;
        var contractTotal = ledger?.EffectiveTotal ?? booking.Contract?.TotalValue ?? total;
        var remaining = Math.Max(0m, (booking.Contract is null ? total : contractTotal) - deposit);

        var stage2 = booking.Contract?.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == "SUCCESS" && p.PaymentType == PaymentTypes.Installment)
            .Sum(p => p.Amount) ?? 0m;
        var hasPaidStage2 = stage2 > 0 || (contractTotal > 0 && deposit >= contractTotal * 0.59m);

        var consultant = booking.CoordinationAssignments
            .Where(a => a.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        return new PublicBookingDetailDto
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer.FullName,
            PhoneNumber = booking.Customer.PhoneNumber,
            Email = booking.Customer.Email,
            EventDate = booking.HallSchedule.Date,
            EventDateString = booking.HallSchedule.Date.ToString("yyyy-MM-dd"),
            Shift = booking.HallSchedule.Shift,
            HallId = booking.HallSchedule.HallId,
            HallName = booking.HallSchedule.Hall.HallName,
            HallPrice = booking.FinalHallPrice ?? booking.HallSchedule.Hall.RentalPrice,
            HallCapacity = booking.HallSchedule.Hall.MaximumCapacity,
            GuestCount = booking.GuestCount,
            OfficialTables = official,
            SpareTables = spare,
            TotalTables = totalTables,
            MenuId = booking.MenuId,
            MenuName = booking.Menu?.MenuName,
            MenuPricePerTable = booking.FinalMenuPrice ?? booking.Menu?.PricePerTable,
            Dishes = dishes,
            DecorId = booking.DecorationPackageId,
            DecorName = booking.DecorationPackage?.TenGoi,
            DecorPrice = booking.FinalDecorationPrice ?? booking.DecorationPackage?.Gia,
            DecorStyle = booking.DecorationPackage?.PhongCach,
            Services = services,
            TotalAmount = total,
            DepositPaid = deposit,
            RemainingDebt = remaining,
            HasPaidStage2 = hasPaidStage2,
            Stage2Paid = stage2,
            StatusCode = booking.Status.StatusCode,
            StatusName = booking.Status.StatusName,
            CanCancelOnline = booking.Status.StatusGroup == StatusGroups.Booking
                && booking.Status.StatusCode == BookingStatusCodes.Pending,
            HoldExpiresAt = booking.BookedAt.AddHours(48),
            BookedAt = booking.BookedAt,
            CancellationReason = booking.CancellationReason,
            SpecialRequests = booking.SpecialRequests,
            ConsultantName = consultant?.Coordinator.FullName,
            ConsultantPhone = consultant?.Coordinator.PhoneNumber,
            ContractId = booking.Contract?.ContractId,
            ContractCode = booking.Contract?.ContractCode,
            ContractStatusCode = booking.Contract?.Status?.StatusCode,
            ContractStatusName = booking.Contract?.Status?.StatusName,
            ContractContent = booking.Contract?.ContractContent,
            PaymentTerms = booking.Contract?.PaymentTerms,
            ContractTotalValue = booking.Contract is null ? null : contractTotal,
            ContractPaidAmount = deposit,
                    };
    }

    private static int TryParseSpareTables(string? specialRequests)
    {
        if (string.IsNullOrWhiteSpace(specialRequests)) return 0;
        // Khớp "[Bàn dự phòng: 2]", "dự phòng: 2", "spare: 2", "sơ cua ... 2".
        var m = System.Text.RegularExpressions.Regex.Match(
            specialRequests, @"(?:dự\s*phòng|spare|sơ\s*cua)\s*[:\-]?\s*(\d{1,2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var v) && v >= 0 && v <= 50)
            return v;
        return 0;
    }
    /// <summary>
    /// Tải PDF hợp đồng tiệc cưới chính thức cho khách hàng.
    /// </summary>
    [HttpGet("{bookingCode}/contract/pdf")]
    public async Task<IActionResult> DownloadContractPdf(string bookingCode, [FromQuery] string phone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { success = false, message = "Thiếu mã đặt tiệc hoặc số điện thoại." });

        var code = bookingCode.Trim().ToUpperInvariant();
        var contact = phone.Trim();
        var normalizedEmail = contact.ToLowerInvariant();
        var lookupByEmail = contact.Contains('@');

        var booking = await _context.WeddingBookings.AsNoTracking()
                .AsSplitQuery()
                .Include(b => b.Customer)
            .Include(b => b.Contract)
            .SingleOrDefaultAsync(b => b.BookingCode == code &&
                (lookupByEmail ? b.Customer.Email != null && b.Customer.Email.ToLower() == normalizedEmail
                    : b.Customer.PhoneNumber == contact), cancellationToken);

        if (booking == null)
            return NotFound(new { success = false, message = "Không tìm thấy thông tin đơn tiệc." });

        if (booking.Contract == null)
            return NotFound(new { success = false, message = "Đơn tiệc này chưa được lập hợp đồng." });

        var detail = await BuildContractDetailAsync(booking.Contract.ContractId, cancellationToken);
        if (detail == null)
            return NotFound(new { success = false, message = "Không tìm thấy chi tiết hợp đồng." });

        var bytes = _contractPdfGenerator.Generate(detail);
        return File(bytes, "application/pdf", $"HopDong_{detail.ContractCode}.pdf");
    }

    private async Task<ManagerContractDetailDto?> BuildContractDetailAsync(int contractId, CancellationToken cancellationToken)
    {
        var detail = await _context.Contracts.AsNoTracking().Where(contract => contract.ContractId == contractId)
            .Select(contract => new ManagerContractDetailDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                CreatedDate = contract.CreatedDate,
                TotalValue = contract.TotalValue,
                ContractContent = contract.ContractContent ?? string.Empty,
                PaymentTerms = contract.PaymentTerms ?? string.Empty,
                StatusCode = contract.Status.StatusCode,
                StatusName = contract.Status.StatusName,
                PaidAmount = contract.Payments.Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m,
                Booking = new ManagerContractBookingDto
                {
                    BookingId = contract.BookingId,
                    BookingCode = contract.Booking.BookingCode,
                    CustomerName = contract.Booking.Customer.FullName,
                    CustomerPhone = contract.Booking.Customer.PhoneNumber,
                    CustomerEmail = contract.Booking.Customer.Email,
                    EventDate = contract.Booking.HallSchedule.Date,
                    Shift = contract.Booking.HallSchedule.Shift,
                    HallName = contract.Booking.HallSchedule.Hall.HallName,
                    GuestCount = contract.Booking.GuestCount,
                    MenuName = contract.Booking.Menu != null ? contract.Booking.Menu.MenuName : null,
                    DecorationPackageName = contract.Booking.DecorationPackage != null ? contract.Booking.DecorationPackage.TenGoi : null,
                    Services = contract.Booking.BookingServices
                        .Where(item => item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                        .OrderBy(item => item.BookingServiceId)
                        .Select(item => item.ServiceItem.TenDichVu).ToList(),
                    EstimatedTotal = contract.Booking.EstimatedTotal ?? 0m,
                    StatusCode = contract.Booking.Status.StatusCode,
                    StatusName = contract.Booking.Status.StatusName
                },
                Payments = contract.Payments.OrderByDescending(payment => payment.PaymentDate)
                    .Select(payment => new ManagerPaymentListItemDto
                    {
                        PaymentId = payment.PaymentId,
                        TransactionCode = payment.TransactionCode,
                        ContractId = contract.ContractId,
                        ContractCode = contract.ContractCode,
                        BookingCode = contract.Booking.BookingCode,
                        CustomerName = contract.Booking.Customer.FullName,
                        CustomerPhone = contract.Booking.Customer.PhoneNumber,
                        PaymentType = payment.PaymentType,
                        Amount = payment.Amount,
                        PaymentDate = payment.PaymentDate,
                        PaymentMethod = payment.PaymentMethod,
                        StatusCode = payment.Status.StatusCode,
                        StatusName = payment.Status.StatusName
                    }).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        if (detail is null) return null;
        var contract = await _context.Contracts.AsNoTracking().SingleAsync(item => item.ContractId == contractId, cancellationToken);
        var ledger = await new Uc18FinancialLedger(_context).ReadAsync(contract, cancellationToken);
        detail.EffectiveTotal = ledger.EffectiveTotal;
        detail.RefundedPrincipal = ledger.Receipts.TotalPrincipalReturned;
        detail.NetPaidAmount = ledger.Receipts.GrossCollected - ledger.Receipts.TotalPrincipalReturned;
        detail.EffectiveRemainingBalance = ledger.EffectiveTotal - detail.NetPaidAmount;
        detail.TotalValue = ledger.EffectiveTotal;
        detail.PaidAmount = detail.NetPaidAmount.Value;
        return detail;
    }
}

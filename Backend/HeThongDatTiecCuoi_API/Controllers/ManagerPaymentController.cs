using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/payments")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerPaymentController : ControllerBase
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ManagerPaymentController> _logger;

    public ManagerPaymentController(
        ApplicationDbContext context,
        IStatusService statusService,
        IPaymentService paymentService,
        ILogger<ManagerPaymentController> logger)
    {
        _context = context;
        _statusService = statusService;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ManagerPaymentListResponseDto>> GetPayments(
        [FromQuery] ManagerPaymentFilterDto filter,
        CancellationToken cancellationToken)
    {
        var query = _context.Payments.AsNoTracking().AsQueryable();
        var keyword = Normalize(filter.Keyword);
        if (keyword is not null)
        {
            query = query.Where(payment =>
                (payment.TransactionCode != null && payment.TransactionCode.Contains(keyword)) ||
                payment.Contract.ContractCode.Contains(keyword) ||
                payment.Contract.Booking.BookingCode.Contains(keyword) ||
                payment.Contract.Booking.Customer.FullName.Contains(keyword) ||
                payment.Contract.Booking.Customer.PhoneNumber.Contains(keyword));
        }

        var type = Normalize(filter.Type);
        if (type is not null && !type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (type is not (PaymentTypes.Deposit or PaymentTypes.Installment or PaymentTypes.Settlement))
                return BadRequest(new { message = "Loại thanh toán không hợp lệ." });
            query = query.Where(payment => payment.PaymentType == type);
        }

        var status = Normalize(filter.Status)?.ToUpperInvariant();
        if (status is not null && status != "ALL")
        {
            if (status is not (PaymentStatusCodes.Pending or PaymentStatusCodes.Success or
                PaymentStatusCodes.Failed or PaymentStatusCodes.Cancelled))
                return BadRequest(new { message = "Trạng thái thanh toán không hợp lệ." });
            query = query.Where(payment => payment.Status.StatusCode == status);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(payment => payment.PaymentDate >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(payment => payment.PaymentDate < filter.ToDate.Value.Date.AddDays(1));

        var page = Math.Max(1, filter.Page);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.PaymentId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(payment => new ManagerPaymentListItemDto
            {
                PaymentId = payment.PaymentId,
                TransactionCode = payment.TransactionCode,
                ContractId = payment.ContractId,
                ContractCode = payment.Contract.ContractCode,
                BookingCode = payment.Contract.Booking.BookingCode,
                CustomerName = payment.Contract.Booking.Customer.FullName,
                CustomerPhone = payment.Contract.Booking.Customer.PhoneNumber,
                PaymentType = payment.PaymentType,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate,
                PaymentMethod = payment.PaymentMethod,
                StatusCode = payment.Status.StatusCode,
                StatusName = payment.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(new ManagerPaymentListResponseDto
        {
            Items = items,
            Page = page,
            PageSize = PageSize,
            TotalItems = totalItems
        });
    }

    /// <summary>
    /// Ghi nhận thanh toán tiền mặt trực tiếp tại quầy (Đợt 2 hoặc Quyết toán).
    /// </summary>
    [HttpPost("record-cash")]
    public async Task<IActionResult> RecordCashPayment(
        [FromBody] RecordCashPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.ContractId <= 0 || request.Amount <= 0)
            return BadRequest(new { success = false, message = "Dữ liệu thanh toán không hợp lệ hoặc số tiền phải lớn hơn 0." });

        var validTypes = new[] { PaymentTypes.Installment, PaymentTypes.Settlement };
        var normalizedType = validTypes.FirstOrDefault(t => string.Equals(t, request.PaymentType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedType is null)
            return BadRequest(new { success = false, message = "Loại thanh toán phải là 'Thanh toán đợt' hoặc 'Quyết toán'." });

        var contract = await _context.Contracts
            .Include(c => c.Status)
            .Include(c => c.Booking)
            .Include(c => c.Payments).ThenInclude(p => p.Status)
            .SingleOrDefaultAsync(c => c.ContractId == request.ContractId, cancellationToken);

        if (contract == null)
            return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });

        if (contract.Status.StatusCode != ContractStatusCodes.Active)
            return Conflict(new { success = false, message = $"Chỉ hợp đồng đang có hiệu lực mới được ghi nhận thanh toán (Trạng thái hiện tại: {contract.Status.StatusName})." });

        // SEC-05: Kiểm tra trần số tiền thanh toán tiền mặt không vượt quá công nợ còn lại
        var oldPaid = contract.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(p => p.Amount);
        var remainingBalance = Math.Max(0, contract.TotalValue - oldPaid);

        if (remainingBalance <= 0)
            return BadRequest(new { success = false, message = "Hợp đồng này đã được thanh toán đầy đủ 100%, không còn số dư cần thanh toán." });

        if (request.Amount > remainingBalance)
            return BadRequest(new { success = false, message = $"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá số dư công nợ còn lại của hợp đồng ({remainingBalance:N0} đ)." });

        if (request.Amount < 2000)
            return BadRequest(new { success = false, message = "Số tiền thanh toán tối thiểu là 2.000 đ." });

        int? actorId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            actorId = uid;

        var successStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success, cancellationToken);
        var txCode = $"TM-{contract.ContractCode}-{DateTime.Now:yyyyMMddHHmmss}";

        var payment = new Payment
        {
            ContractId = contract.ContractId,
            PaymentType = normalizedType,
            Amount = request.Amount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "CASH",
            TransactionCode = txCode,
            StatusId = successStatusId
        };
        _context.Payments.Add(payment);

        contract.Booking.PaidAmount = (contract.Booking.PaidAmount) + request.Amount;

        var newPaid = oldPaid + request.Amount;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = AuditActions.PaymentRecorded,
            EntityName = AuditEntityNames.Contract,
            EntityId = contract.ContractId,
            OldData = JsonSerializer.Serialize(new { contract.ContractCode, PaidAmount = oldPaid }),
            NewData = JsonSerializer.Serialize(new { contract.ContractCode, PaidAmount = newPaid, PaymentType = normalizedType, Amount = request.Amount, Method = "CASH", Note = request.Note }),
            Timestamp = DateTime.Now,
            Outcome = AuditOutcomes.Success,
            Notes = $"Quản lý ghi nhận thu tiền mặt {request.Amount:N0} đ ({normalizedType}) cho hợp đồng {contract.ContractCode}. Ghi chú: {request.Note ?? "Không có"}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Đã ghi nhận thanh toán tiền mặt {request.Amount:N0} đ ({normalizedType}) thành công.",
            data = new
            {
                paymentId = payment.PaymentId,
                transactionCode = payment.TransactionCode,
                amount = payment.Amount,
                paymentType = payment.PaymentType,
                paymentMethod = payment.PaymentMethod,
                paymentDate = payment.PaymentDate,
                totalPaid = newPaid,
                remainingBalance = Math.Max(0, contract.TotalValue - newPaid)
            }
        });
    }

    /// <summary>
    /// Tạo liên kết thanh toán VietQR PayOS cho hợp đồng (Đợt 2 hoặc Quyết toán).
    /// </summary>
    [HttpPost("create-payos-link")]
    public async Task<IActionResult> CreatePayOsLink(
        [FromBody] CreateContractPayOsLinkRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.ContractId <= 0 || request.Amount <= 0)
            return BadRequest(new { success = false, message = "Dữ liệu tạo link thanh toán không hợp lệ hoặc số tiền phải lớn hơn 0." });

        int? actorId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            actorId = uid;

        try
        {
            var result = await _paymentService.CreateContractPaymentAsync(request, actorId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo PayOS link cho ContractId={ContractId}. traceId={TraceId}", request.ContractId, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi khi tạo liên kết thanh toán PayOS. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái thanh toán PayOS của hợp đồng phục vụ Polling từ Manager UI.
    /// </summary>
    [HttpGet("check-payos/{orderCode:long}")]
    public async Task<IActionResult> CheckPayOsPayment(long orderCode)
    {
        try
        {
            var result = await _paymentService.CheckContractPaymentStatusAsync(orderCode);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kiểm tra trạng thái PayOS OrderCode={OrderCode}. traceId={TraceId}", orderCode, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi khi kiểm tra trạng thái thanh toán PayOS.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

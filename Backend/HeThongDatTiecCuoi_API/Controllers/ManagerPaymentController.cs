using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.Data.SqlClient;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/payments")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerPaymentController : ControllerBase
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;
    public ManagerPaymentController(ApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        if (request.RequestId == Guid.Empty || !PaymentBusinessRules.IsMoney(request.Amount) ||
            string.IsNullOrWhiteSpace(request.PaymentMethod) || request.PaymentMethod.Length > 50 ||
            request.TransactionCode?.Length > 100)
            return BadRequest(new { message = "Số tiền, phương thức hoặc mã yêu cầu không hợp lệ." });
        var method = request.PaymentMethod.Trim();
        var reference = string.IsNullOrWhiteSpace(request.TransactionCode) ? null : request.TransactionCode.Trim();
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // Serialize the aggregate before reading its balance, including contracts with no payments yet.
            var contract = await _context.Contracts.FromSqlInterpolated(
                    $"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {request.ContractId}")
                .Include(c => c.Status).Include(c => c.Booking).ThenInclude(b => b.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
            var previous = await _context.Payments.AsNoTracking()
                .SingleOrDefaultAsync(p => p.RequestId == request.RequestId, cancellationToken);
            if (previous is not null)
            {
                if (previous.ContractId != request.ContractId || previous.Amount != request.Amount ||
                    previous.PaymentType != request.PaymentType || previous.PaymentMethod != method || previous.TransactionCode != reference)
                    return Conflict(new { message = "Mã yêu cầu đã được dùng với dữ liệu khác." });
                return Ok(new PaymentActionResponseDto { PaymentId = previous.PaymentId, Replayed = true, Message = "Khoản thu đã được ghi nhận trước đó." });
            }
            if (contract.Status.StatusGroup != StatusGroups.Contract || contract.Booking.Status.StatusGroup != StatusGroups.Booking)
                return Conflict(new { message = "Trạng thái hợp đồng hoặc booking sai nhóm nghiệp vụ." });
            var successful = await _context.Payments.AsNoTracking()
                .Where(p => p.ContractId == contract.ContractId && p.Status.StatusGroup == StatusGroups.Payment &&
                    p.Status.StatusCode == PaymentStatusCodes.Success)
                .Select(p => new { p.Amount, p.PaymentType }).ToListAsync(cancellationToken);
            var paid = successful.Sum(p => p.Amount);
            var error = PaymentBusinessRules.Validate(contract.Status.StatusCode, contract.Booking.Status.StatusCode,
                contract.TotalValue, contract.RequiredDepositAmount, paid, request.PaymentType, request.Amount);
            if (error is not null) return Conflict(new { message = error });
            var deposits = successful.Where(p => p.PaymentType == PaymentTypes.Deposit).Sum(p => p.Amount) +
                (request.PaymentType == PaymentTypes.Deposit ? request.Amount : 0m);
            var activate = contract.Status.StatusCode == ContractStatusCodes.Draft && deposits >= contract.RequiredDepositAmount!.Value;
            if (activate)
            {
                error = ContractBusinessRules.ValidateActivation(contract.Status.StatusCode, contract.TotalValue,
                    contract.ContractContent, contract.PaymentTerms, contract.Booking.Status.StatusCode, contract.RequiredDepositAmount, deposits);
                if (error is not null) return Conflict(new { message = error });
                contract.StatusId = await _context.Statuses.Where(s => s.StatusGroup == StatusGroups.Contract && s.StatusCode == ContractStatusCodes.Active)
                    .Select(s => s.StatusId).SingleAsync(cancellationToken);
            }
            var payment = new Payment
            {
                ContractId = contract.ContractId, RequestId = request.RequestId, Amount = request.Amount,
                PaymentType = request.PaymentType, PaymentMethod = method, TransactionCode = reference, PaymentDate = DateTime.Now,
                StatusId = await _context.Statuses.Where(s => s.StatusGroup == StatusGroups.Payment && s.StatusCode == PaymentStatusCodes.Success)
                    .Select(s => s.StatusId).SingleAsync(cancellationToken)
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actor, Action = AuditActions.PaymentRecorded, EntityName = AuditEntityNames.Payment, EntityId = payment.PaymentId,
                Timestamp = DateTime.Now, OldData = JsonSerializer.Serialize(new { PaidAmount = paid, RemainingBalance = contract.TotalValue - paid }),
                NewData = JsonSerializer.Serialize(new { payment.ContractId, payment.RequestId, payment.PaymentType, payment.Amount,
                    PaidAmount = paid + payment.Amount, RemainingBalance = contract.TotalValue - paid - payment.Amount }),
                Notes = $"Ghi nhận khoản thu cho hợp đồng {contract.ContractCode}."
            });
            if (activate) _context.AuditLogs.Add(new AuditLog
            {
                UserId = actor, Action = AuditActions.ContractActivated, EntityName = AuditEntityNames.Contract, EntityId = contract.ContractId,
                Timestamp = DateTime.Now, OldData = JsonSerializer.Serialize(new { Status = ContractStatusCodes.Draft }),
                NewData = JsonSerializer.Serialize(new { Status = ContractStatusCodes.Active, SuccessfulDeposits = deposits }),
                Notes = "Kích hoạt sau khi nhận đủ cọc cam kết."
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new PaymentActionResponseDto { PaymentId = payment.PaymentId, Message = "Đã ghi nhận thanh toán." });
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 } ||
            ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 1205 or 1222 } })
        {
            return Conflict(new { message = "Có giao dịch đồng thời. Vui lòng thử lại với cùng mã yêu cầu." });
        }
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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

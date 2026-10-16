using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
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
    public ManagerPaymentController(ApplicationDbContext context) => _context = context;

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

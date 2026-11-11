using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/bookings")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerBookingRefundObligationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ManagerBookingRefundObligationsController(ApplicationDbContext db) => _db = db;

    [HttpGet("{id:int}/refund-obligations")]
    public async Task<ActionResult<IReadOnlyList<RefundObligationResponse>>> GetRefundObligations(
        int id,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from operation in _db.Set<Uc18BookingOperation>().AsNoTracking()
            join contract in _db.Contracts.AsNoTracking()
                on operation.ContractId equals (int?)contract.ContractId
            where operation.BookingId == id
                && contract.BookingId == id
                && (operation.PrincipalRefundDue > 0 || operation.CompensationDue > 0)
            orderby operation.RecordedAtUtc, operation.RequestId
            select new
            {
                operation.RequestId,
                operation.Kind,
                operation.RecordedAtUtc,
                operation.PrincipalRefundDue,
                operation.DepositPrincipalPortion,
                operation.CompensationDue,
                ConfirmedPrincipal = operation.RefundConfirmations.Sum(confirmation => confirmation.PrincipalPaid),
                ConfirmedDeposit = operation.RefundConfirmations.Sum(confirmation => confirmation.DepositPrincipalPaid),
                ConfirmedCompensation = operation.RefundConfirmations.Sum(confirmation => confirmation.CompensationPaid)
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(row =>
        {
            var dueDeposit = row.DepositPrincipalPortion;
            var duePrincipal = row.PrincipalRefundDue;
            return new RefundObligationResponse(
                row.RequestId,
                row.Kind,
                row.RecordedAtUtc,
                duePrincipal,
                dueDeposit,
                row.CompensationDue,
                row.ConfirmedPrincipal,
                row.ConfirmedDeposit,
                row.ConfirmedCompensation,
                Math.Max(0m, duePrincipal - row.ConfirmedPrincipal),
                Math.Max(0m, dueDeposit - row.ConfirmedDeposit),
                Math.Max(0m, row.CompensationDue - row.ConfirmedCompensation));
        }).ToArray());
    }

    public sealed record RefundObligationResponse(
        Guid RequestId,
        string Kind,
        DateTime RecordedAtUtc,
        decimal DuePrincipal,
        decimal DueDeposit,
        decimal DueCompensation,
        decimal ConfirmedPrincipal,
        decimal ConfirmedDeposit,
        decimal ConfirmedCompensation,
        decimal RemainingPrincipal,
        decimal RemainingDeposit,
        decimal RemainingCompensation);
}

using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/bookings")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerRefundConfirmationController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ManagerRefundConfirmationController(ApplicationDbContext db) => _db = db;

    [HttpPost("{id:int}/refund-confirmations")]
    public async Task<IActionResult> Confirm(int id, [FromBody] RefundConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var evidence = request.ExternalEvidenceReference?.Trim();
        if (id <= 0 || request.RequestId == Guid.Empty || request.OperationRequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(evidence) || evidence.Length > 300 ||
            !Uc18FinancialLedger.IsValidRefundMoney(request.PrincipalPaid) ||
            !Uc18FinancialLedger.IsValidRefundMoney(request.DepositPrincipalPaid) ||
            !Uc18FinancialLedger.IsValidRefundMoney(request.CompensationPaid))
            return BadRequest(new { code = "ERR_INVALID_REFUND_CONFIRMATION" });

        var payloadHash = ComputePayloadHash(id, request, evidence);
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            var operationKey = await _db.Set<Uc18BookingOperation>().AsNoTracking()
                .Where(x => x.RequestId == request.OperationRequestId)
                .Select(x => new { x.BookingId, x.ContractId }).SingleOrDefaultAsync(cancellationToken);
            if (operationKey is null || operationKey.BookingId != id || !operationKey.ContractId.HasValue)
                return Conflict(new { code = "ERR_REFUND_OPERATION_NOT_ELIGIBLE" });

            Contract? contract;
            if (_db.Database.IsSqlServer())
                contract = await _db.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {operationKey.ContractId.Value}")
                    .SingleOrDefaultAsync(cancellationToken);
            else
                contract = await _db.Contracts.SingleOrDefaultAsync(x => x.ContractId == operationKey.ContractId.Value, cancellationToken);
            if (contract is null || contract.BookingId != id) return Conflict(new { code = "ERR_REFUND_OPERATION_NOT_ELIGIBLE" });

            var previous = await _db.Set<Uc18RefundConfirmation>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == request.RequestId, cancellationToken);
            if (previous is not null)
            {
                if (previous.ActorUserId != actorId || previous.PayloadHash != payloadHash)
                    return Conflict(new { code = "ERR_REQUEST_ID_REUSED" });
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Ok(new RefundConfirmationResponse(previous.RequestId, previous.OperationRequestId,
                    previous.ExternalEvidenceReference, previous.PrincipalPaid, previous.DepositPrincipalPaid,
                    previous.CompensationPaid, previous.ConfirmedAtUtc, true));
            }

            var evidenceUsed = await _db.Set<Uc18RefundConfirmation>().AnyAsync(x => x.ExternalEvidenceReference == evidence, cancellationToken);
            if (evidenceUsed) return Conflict(new { code = "ERR_EVIDENCE_REFERENCE_REUSED" });

            var operation = await _db.Set<Uc18BookingOperation>().SingleAsync(x => x.RequestId == request.OperationRequestId, cancellationToken);
            if (operation.BookingId != id || operation.ContractId != contract.ContractId ||
                operation.Kind is not (Uc18BookingOperation.Cancel or Uc18BookingOperation.PriorRefund or Uc18BookingOperation.Reschedule or Uc18BookingOperation.MenuChange))
                return Conflict(new { code = "ERR_REFUND_OPERATION_NOT_ELIGIBLE" });

            var ledger = await new Uc18FinancialLedger(_db).ReadAsync(contract, cancellationToken);

            var confirmations = await _db.Set<Uc18RefundConfirmation>().Where(x => x.OperationRequestId == operation.RequestId)
                .ToListAsync(cancellationToken);
            var confirmation = new Uc18RefundConfirmation
            {
                RequestId = request.RequestId, OperationRequestId = request.OperationRequestId, ActorUserId = actorId,
                PayloadHash = payloadHash, ExternalEvidenceReference = evidence, ConfirmedAtUtc = DateTime.UtcNow,
                PrincipalPaid = request.PrincipalPaid, DepositPrincipalPaid = request.DepositPrincipalPaid,
                CompensationPaid = request.CompensationPaid
            };
            var error = Uc18FinancialLedger.ValidateRefundConfirmation(operation, confirmation,
                confirmations.Sum(x => x.PrincipalPaid), confirmations.Sum(x => x.DepositPrincipalPaid),
                confirmations.Sum(x => x.CompensationPaid));
            if (error is not null) return Conflict(new { code = "ERR_REFUND_CAP_EXCEEDED", message = error });
            var remainingPrincipal = ledger.Receipts.GrossCollected - ledger.Receipts.TotalPrincipalReturned;
            var remainingDeposit = ledger.Receipts.GrossDeposit - ledger.Receipts.DepositPrincipalReturned;
            var remainingNonDeposit = remainingPrincipal - remainingDeposit;
            if (confirmation.PrincipalPaid > remainingPrincipal ||
                confirmation.DepositPrincipalPaid > remainingDeposit ||
                confirmation.PrincipalPaid - confirmation.DepositPrincipalPaid > remainingNonDeposit)
                return Conflict(new { code = "ERR_CONTRACT_REFUND_CAP_EXCEEDED" });

            _db.Set<Uc18RefundConfirmation>().Add(confirmation);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = "REFUND_CONFIRMED", EntityName = "XacNhanHoanTien",
                EntityId = id, Timestamp = DateTime.Now,
                NewData = JsonSerializer.Serialize(new { confirmation.RequestId, confirmation.OperationRequestId,
                    confirmation.ExternalEvidenceReference, confirmation.PrincipalPaid, confirmation.DepositPrincipalPaid,
                    confirmation.CompensationPaid, confirmation.PayloadHash }),
                Notes = "UC18: xác nhận hoàn tiền ngoài hệ thống; không gọi cổng thanh toán."
            });
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new RefundConfirmationResponse(confirmation.RequestId, confirmation.OperationRequestId,
                confirmation.ExternalEvidenceReference, confirmation.PrincipalPaid, confirmation.DepositPrincipalPaid,
                confirmation.CompensationPaid, confirmation.ConfirmedAtUtc, false));
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } ||
            ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_REFUND_CONCURRENT_CHANGE" });
        }
    }

    private static string ComputePayloadHash(int bookingId, RefundConfirmationRequest request, string evidence)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            bookingId,
            operationRequestId = request.OperationRequestId.ToString("D").ToLowerInvariant(),
            evidence,
            principalPaid = request.PrincipalPaid.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            depositPrincipalPaid = request.DepositPrincipalPaid.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            compensationPaid = request.CompensationPaid.ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public sealed record RefundConfirmationRequest(Guid RequestId, Guid OperationRequestId,
        string? ExternalEvidenceReference, decimal PrincipalPaid, decimal DepositPrincipalPaid, decimal CompensationPaid);

    public sealed record RefundConfirmationResponse(Guid RequestId, Guid OperationRequestId, string ExternalEvidenceReference,
        decimal PrincipalPaid, decimal DepositPrincipalPaid, decimal CompensationPaid, DateTime ConfirmedAtUtc, bool Replayed);
}

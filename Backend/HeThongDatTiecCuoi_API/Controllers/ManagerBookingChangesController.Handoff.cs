using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

public sealed partial class ManagerBookingChangesController
{
    [HttpPost("{id:int}/handoff")]
    [Authorize(Roles = RoleNames.HallManager)]
    public async Task<IActionResult> Handoff(
        int id,
        [FromBody] ManagerBookingHandoffRequestDto request,
        [FromServices] INotificationService notifications,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var operationName = request.Operation?.Trim().ToUpperInvariant();
        var reason = request.Reason?.Trim();
        if (!ModelState.IsValid || request.RequestId == Guid.Empty || operationName is not ("RESCHEDULE" or "CANCEL") ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return BadRequest(new { code = "ERR_INVALID_HANDOFF" });

        var payloadHash = ComputeHandoffPayloadHash(id, operationName, reason);
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await LockBookingAsync(id, cancellationToken);
            var loaded = await LoadBookingAsync(id, cancellationToken);
            if (loaded.Error is not null) return loaded.Error;
            var booking = loaded.Booking!;
            var scopeError = await ValidateHallScopeAsync(booking.HallSchedule.HallId, cancellationToken);
            if (scopeError is not null) return scopeError;

            var previous = await _db.Set<Uc18BookingOperation>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == request.RequestId, cancellationToken);
            if (previous is not null)
            {
                if (previous.ActorUserId != actorId || previous.BookingId != id ||
                    previous.Kind != Uc18BookingOperation.Handoff || previous.PayloadHash != payloadHash)
                    return Conflict(new { code = "ERR_REQUEST_ID_REUSED" });
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Ok(new ManagerBookingHandoffResultDto(request.RequestId, id, operationName, true));
            }

            if (operationName == "RESCHEDULE")
            {
                var expiredPendingHold = booking.Status.StatusCode == BookingStatusCodes.Pending &&
                    booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now;
                var activeRequest = await _db.WeddingScheduleChanges.AsNoTracking().AnyAsync(change =>
                    change.BookingId == id && change.Status.StatusGroup == StatusGroups.ScheduleChange &&
                    ScheduleChangeStatusCodes.Open.Contains(change.Status.StatusCode) &&
                    !(expiredPendingHold && change.Status.StatusCode == ScheduleChangeStatusCodes.Pending),
                    cancellationToken);
                if (activeRequest) return Conflict(new { code = "ERR_RESCHEDULE_OPERATION_ACTIVE" });
            }

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                return Conflict(new { code = "ERR_BOOKING_TERMINAL" });

            var managerId = await _db.Users.Where(user => user.Role.RoleName == RoleNames.Manager &&
                    user.Status.StatusGroup == StatusGroups.Account && user.Status.StatusCode == AccountStatusCodes.Active &&
                    user.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                .OrderBy(user => user.UserId).Select(user => (int?)user.UserId).FirstOrDefaultAsync(cancellationToken);
            if (!managerId.HasValue) return Conflict(new { code = "ERR_MANAGER_UNAVAILABLE" });

            var nowUtc = DateTime.UtcNow;
            _db.Set<Uc18BookingOperation>().Add(new Uc18BookingOperation
            {
                RequestId = request.RequestId, BookingId = id, ContractId = booking.Contract?.ContractId,
                ActorUserId = actorId, Kind = Uc18BookingOperation.Handoff, PayloadHash = payloadHash,
                RecordedAtUtc = nowUtc,
                SnapshotJson = JsonSerializer.Serialize(new { Operation = operationName, Reason = reason })
            });
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = "BOOKING_HANDED_OFF_TO_MANAGER", EntityName = "DatTiec", EntityId = id,
                OldData = JsonSerializer.Serialize(new { booking.Status.StatusCode, booking.HallScheduleId }),
                NewData = JsonSerializer.Serialize(new { request.RequestId, Operation = operationName, Reason = reason }),
                Timestamp = DateTime.Now,
                Notes = "UC18: Quản lý sảnh bàn giao yêu cầu xử lý booking cho Quản lý; không thay đổi booking hoặc lịch."
            });
            await notifications.AddAsync(managerId.Value, actorId, "BOOKING_HANDOFF",
                "Yêu cầu xử lý booking được bàn giao",
                $"Quản lý sảnh đã bàn giao yêu cầu {operationName} cho booking {booking.BookingCode}. Lý do: {reason}",
                "DatTiec", id, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new ManagerBookingHandoffResultDto(request.RequestId, id, operationName, false));
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } ||
                                   ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_BOOKING_STATE_CONFLICT" });
        }
    }

    private static string ComputeHandoffPayloadHash(int bookingId, string operation, string reason)
    {
        var canonical = JsonSerializer.Serialize(new { bookingId, Operation = operation, Reason = reason });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

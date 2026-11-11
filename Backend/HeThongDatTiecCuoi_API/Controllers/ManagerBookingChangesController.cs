using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/bookings")]
[Authorize(Roles = RoleNames.Manager + "," + RoleNames.HallManager)]
public sealed partial class ManagerBookingChangesController : ControllerBase
{
    private const decimal MaxStoredMoney = 9999999999999999.99m;
    private readonly ApplicationDbContext _db;
    private readonly IStatusService _statuses;

    public ManagerBookingChangesController(ApplicationDbContext db, IStatusService statuses)
    {
        _db = db;
        _statuses = statuses;
    }

    [HttpGet]
    public async Task<ActionResult<BookingListResponseDto>> List([FromQuery] BookingFilterRequestDto filter, CancellationToken cancellationToken)
    {
        var scopedQuery = _db.WeddingBookings.AsNoTracking().Where(x => x.Status.StatusGroup == StatusGroups.Booking);
        if (User.IsInRole(RoleNames.HallManager))
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
            var today = DateTime.Today;
            scopedQuery = scopedQuery.Where(x => _db.HallManagerAssignments.Any(a => a.HallId == x.HallSchedule.HallId &&
                a.HallManager.UserId == userId && a.FromDate <= today && (!a.ToDate.HasValue || a.ToDate.Value >= today) &&
                a.HallManager.DataStatusId == existingId && a.HallManager.User.DataStatusId == existingId &&
                a.HallManager.User.Status.StatusCode == BusinessStatusCodes.Active &&
                a.HallManager.User.Role.RoleName == RoleNames.HallManager));
        }
        var query = scopedQuery;
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(x => x.BookingCode.Contains(keyword) || x.Customer.FullName.Contains(keyword) || x.Customer.PhoneNumber.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(filter.StatusCode) && !filter.StatusCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status.StatusCode == filter.StatusCode.Trim().ToUpperInvariant());
        if (filter.HallId is > 0) query = query.Where(x => x.HallSchedule.HallId == filter.HallId);
        if (filter.FromDate.HasValue) query = query.Where(x => x.HallSchedule.Date >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(x => x.HallSchedule.Date <= filter.ToDate.Value.Date);
        var counts = await scopedQuery.GroupBy(x => x.Status.StatusCode).Select(x => new { Code = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Code, x => x.Count, cancellationToken);
        var items = await query.OrderByDescending(x => x.BookedAt).Skip((filter.PageIndex - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new BookingSummaryDto
            {
                BookingId = x.BookingId, BookingCode = x.BookingCode, CustomerName = x.Customer.FullName, CustomerPhone = x.Customer.PhoneNumber,
                HallName = x.HallSchedule.Hall.HallName, EventDate = x.HallSchedule.Date, Shift = x.HallSchedule.Shift,
                GuestCount = x.GuestCount, EstimatedTotal = x.EstimatedTotal, StatusCode = x.Status.StatusCode, StatusName = x.Status.StatusName,
                TableCount = (int)Math.Ceiling(x.GuestCount / 10m),
                BookedAt = x.BookedAt, HasContract = x.Contract != null, ContractId = x.Contract != null ? x.Contract.ContractId : null,
                ContractCode = x.Contract != null ? x.Contract.ContractCode : null,
                ContractStatusCode = x.Contract != null ? x.Contract.Status.StatusCode : null
            }).ToListAsync(cancellationToken);
        return new BookingListResponseDto
        {
            Items = items, TotalCount = await query.CountAsync(cancellationToken), PageIndex = filter.PageIndex, PageSize = filter.PageSize,
            PendingCount = counts.GetValueOrDefault(BookingStatusCodes.Pending), ConfirmedCount = counts.GetValueOrDefault(BookingStatusCodes.Confirmed),
            PreparingCount = counts.GetValueOrDefault(BookingStatusCodes.Preparing), CompletedCount = counts.GetValueOrDefault(BookingStatusCodes.Completed),
            CancelledCount = counts.GetValueOrDefault(BookingStatusCodes.Cancelled)
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var booking = await _db.WeddingBookings.AsNoTracking().Where(x => x.BookingId == id)
            .Select(x => new { x.HallSchedule.HallId, x.BookingId }).SingleOrDefaultAsync(cancellationToken);
        if (booking is null) return NotFound();
        var scopeError = await ValidateHallScopeAsync(booking.HallId, cancellationToken);
        if (scopeError is not null) return scopeError;
        var existingId = await _db.DataStatuses.Where(x => x.DataStatusCode == DataStatusCodes.Existing)
            .Select(x => x.DataStatusId).SingleAsync(cancellationToken);
        var detail = await _db.WeddingBookings.AsNoTracking().Where(x => x.BookingId == id).Select(x => new BookingDetailDto
        {
            BookingId = x.BookingId, BookingCode = x.BookingCode, CustomerId = x.CustomerId,
            CustomerName = x.Customer.FullName, CustomerPhone = x.Customer.PhoneNumber, CustomerEmail = x.Customer.Email,
            HallScheduleId = x.HallScheduleId, HallId = x.HallSchedule.HallId, HallName = x.HallSchedule.Hall.HallName,
            HallCode = x.HallSchedule.Hall.HallCode, EventDate = x.HallSchedule.Date, Shift = x.HallSchedule.Shift,
            MenuId = x.MenuId, MenuName = x.Menu != null ? x.Menu.MenuName : null, CustomMenuSnapshotJson = x.CustomMenuSnapshotJson,
            DecorationPackageId = x.DecorationPackageId, DecorationPackageName = x.DecorationPackage != null ? x.DecorationPackage.TenGoi : null,
            GuestCount = x.GuestCount, BilledTableCount = x.BilledTableCount, SpareTableCount = x.SpareTableCount,
            SpecialRequests = x.SpecialRequests, FinalMenuPrice = x.FinalMenuPrice,
            FinalDecorationPrice = x.FinalDecorationPrice, FinalHallPrice = x.FinalHallPrice, EstimatedTotal = x.EstimatedTotal,
            StatusCode = x.Status.StatusCode, StatusName = x.Status.StatusName, CancellationReason = x.CancellationReason,
            BookedAt = x.BookedAt, HasContract = x.Contract != null, ContractId = x.Contract != null ? x.Contract.ContractId : null,
            ContractCode = x.Contract != null ? x.Contract.ContractCode : null,
            ContractStatusCode = x.Contract != null ? x.Contract.Status.StatusCode : null,
            Services = x.BookingServices.Where(s => s.DataStatusId == existingId).Select(s => new BookingServiceDto
            {
                ServiceId = s.ServiceId, ServiceCode = s.ServiceItem.MaDichVu, ServiceName = s.ServiceItem.TenDichVu,
                Quantity = s.Quantity, FinalUnitPrice = s.FinalUnitPrice
            }).ToList()
        }).SingleOrDefaultAsync(cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("{id:int}/reschedule-quote")]
    public async Task<IActionResult> Quote(int id, [FromQuery] ManagerBookingRescheduleQuoteRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        await LockBookingAsync(id, cancellationToken);
        var loaded = await LoadBookingAsync(id, cancellationToken);
        if (loaded.Error is not null) return loaded.Error;
        var uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
        if (uc15Conflict is not null) return uc15Conflict;
        var error = await ValidateHallScopeAsync(loaded.Booking!.HallSchedule.HallId, cancellationToken);
        if (error is not null) return error;
        var linkedContract = loaded.Booking!.Contract;
        if (linkedContract is not null && await HasActiveContractPaymentReservationAsync(id, cancellationToken))
            return Conflict(new { code = "ERR_PAYOS_PAYMENT_PENDING", message = "Hợp đồng đang có giao dịch PayOS chưa được xác nhận." });
        if (linkedContract is null)
        {
            error = await ValidateChangeEligibilityAsync(loaded.Booking, cancellationToken);
            if (error is not null) return error;
        }
        else if (!User.IsInRole(RoleNames.Manager)) return Forbid();
        if (request.ChangeMenu && !HasValidStructuredTableCounts(loaded.Booking!))
            return Conflict(new { code = "ERR_TABLE_COUNTS_UNKNOWN", message = "Cần có đủ số bàn tính tiền và số bàn dự phòng hợp lệ trước khi đổi thực đơn." });
        var destination = await ResolveDestinationAsync(loaded.Booking!, request.NewHallId!.Value, request.NewDate!.Value.Date, request.NewShift, cancellationToken);
        if (destination.Error is not null) return destination.Error;
        var menuChange = await ResolveMenuChangeAsync(loaded.Booking!, request, cancellationToken);
        if (menuChange.Error is not null) return menuChange.Error;
        if (request.ChangeMenu && menuChange.Selection!.HasExistingPricedMenu && !loaded.Booking.FinalMenuPrice.HasValue)
            return Conflict(new { code = "ERR_PRICE_SNAPSHOT_UNKNOWN", message = "Không có giá thực đơn đã lưu để so sánh; không thể đổi thực đơn an toàn." });
        var quote = BuildQuote(loaded.Booking!, destination.Hall!, destination.Date, destination.Shift, menuChange.Selection!);
        if (quote.RequiresPriceConfirmation && !quote.SavedHallPrice.HasValue)
            return Conflict(new { code = "ERR_PRICE_SNAPSHOT_UNKNOWN", message = "Không có giá sảnh đã lưu để so sánh; không thể thay đổi lịch hoặc sảnh an toàn." });
        if (linkedContract is not null)
        {
            var financial = await BuildLinkedFinancialQuoteAsync(loaded.Booking!, linkedContract, quote,
                destination.Date, menuChange.Selection!, cancellationToken);
            if (financial.Error is not null) return financial.Error;
            quote = financial.Quote!;
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(quote);
    }

    [HttpGet("{id:int}/reschedule-options")]
    public async Task<IActionResult> RescheduleOptions(int id, CancellationToken cancellationToken)
    {
        var current = await _db.WeddingBookings.AsNoTracking().Where(x => x.BookingId == id)
            .Select(x => new { x.HallSchedule.HallId }).SingleOrDefaultAsync(cancellationToken);
        if (current is null) return NotFound();
        var scopeError = await ValidateHallScopeAsync(current.HallId, cancellationToken);
        if (scopeError is not null) return scopeError;
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var halls = _db.Halls.AsNoTracking().Where(x => x.DataStatusId == existingId && x.Status.StatusCode == HallStatusCodes.Active);
        if (User.IsInRole(RoleNames.HallManager))
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var today = DateTime.Today;
            halls = halls.Where(h => _db.HallManagerAssignments.Any(a => a.HallId == h.HallId && a.HallManager.UserId == userId &&
                a.FromDate <= today && (!a.ToDate.HasValue || a.ToDate.Value >= today) && a.HallManager.DataStatusId == existingId &&
                a.HallManager.User.DataStatusId == existingId && a.HallManager.User.Status.StatusCode == BusinessStatusCodes.Active &&
                a.HallManager.User.Role.RoleName == RoleNames.HallManager));
        }
        return Ok(await halls.OrderBy(x => x.HallName).Select(x => new ManagerBookingHallOptionDto
        { HallId = x.HallId, HallName = x.HallName, RentalPrice = x.RentalPrice }).ToListAsync(cancellationToken));
    }

    [HttpPut("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, [FromBody] ManagerBookingRescheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var date = request.NewDate!.Value.Date;
        var shift = NormalizeShift(request.NewShift);
        if (shift is null) return BadRequest(new { code = "ERR_INVALID_SHIFT", message = "Ca tổ chức không hợp lệ." });

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await LockBookingAsync(id, cancellationToken);
            var loaded = await LoadBookingAsync(id, cancellationToken);
            if (loaded.Error is not null) return loaded.Error;
            var booking = loaded.Booking!;
            var uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
            if (uc15Conflict is not null) return uc15Conflict;
            var scopeError = await ValidateHallScopeAsync(booking.HallSchedule.HallId, cancellationToken);
            if (scopeError is not null) return scopeError;
            var linkedContract = booking.Contract;
            if (linkedContract is null)
            {
                var eligibilityError = await ValidateChangeEligibilityAsync(booking, cancellationToken);
                if (eligibilityError is not null) return eligibilityError;
            }
            else
            {
                if (!User.IsInRole(RoleNames.Manager)) return Forbid();
                if (!request.RequestId.HasValue || request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.QuoteToken))
                    return BadRequest(new { code = "ERR_FINANCIAL_RESCHEDULE_REQUEST_REQUIRED" });
                var contractId = linkedContract.ContractId;
                await LockContractAsync(contractId, cancellationToken);
                if (await HasActiveContractPaymentReservationAsync(id, cancellationToken))
                    return Conflict(new { code = "ERR_PAYOS_PAYMENT_PENDING", message = "Hợp đồng đang có giao dịch PayOS chưa được xác nhận." });
                linkedContract = await _db.Contracts.AsNoTracking().Include(x => x.Status)
                    .SingleOrDefaultAsync(x => x.ContractId == contractId && x.BookingId == id, cancellationToken);
                if (linkedContract is null) return Conflict(new { code = "ERR_CONTRACT_BOOKING_MISMATCH" });
                var prior = await _db.Set<Uc18BookingOperation>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestId == request.RequestId.Value, cancellationToken);
                if (prior is not null)
                {
                    var replayHash = ComputeReschedulePayloadHash(id, request);
                    if (prior.ActorUserId != actorId || prior.BookingId != id || prior.ContractId != linkedContract.ContractId ||
                        prior.Kind is not (Uc18BookingOperation.Reschedule or Uc18BookingOperation.MenuChange) || prior.PayloadHash != replayHash)
                        return Conflict(new { code = "ERR_REQUEST_ID_REUSED" });
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Ok(new ManagerBookingRescheduleResultDto(prior.RequestId, id, prior.ContractId!.Value,
                        prior.Kind, prior.SignedPriceDelta, prior.FeeAmount, prior.EffectiveTotal!.Value,
                        prior.PrincipalRefundDue, prior.DepositPrincipalPortion, prior.PolicyVersion, true));
                }
                if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                    return Conflict(new { code = "ERR_BOOKING_TERMINAL" });
                if (booking.Status.StatusCode == BookingStatusCodes.Pending &&
                    booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
                    return Conflict(new { code = "ERR_BOOKING_HOLD_EXPIRED" });
            }

            var destination = await ResolveDestinationAsync(booking, request.NewHallId!.Value, date, shift, cancellationToken);
            if (destination.Error is not null) return destination.Error;
            var hall = destination.Hall!;
            if (request.ChangeMenu && !HasValidStructuredTableCounts(booking))
                return Conflict(new { code = "ERR_TABLE_COUNTS_UNKNOWN", message = "Cần có đủ số bàn tính tiền và số bàn dự phòng hợp lệ trước khi đổi thực đơn." });
            var menuChange = await ResolveMenuChangeAsync(booking, request, cancellationToken);
            if (menuChange.Error is not null) return menuChange.Error;
            var selection = menuChange.Selection!;
            var hallChanged = hall.HallId != booking.HallSchedule.HallId;
            var slotChanged = date != booking.HallSchedule.Date.Date || shift != NormalizeShift(booking.HallSchedule.Shift) || hallChanged;
            var quote = BuildQuote(booking, hall, date, shift, selection);
            if (quote.RequiresPriceConfirmation && !quote.SavedHallPrice.HasValue)
                return Conflict(new { code = "ERR_PRICE_SNAPSHOT_UNKNOWN", message = "Không có giá sảnh đã lưu để so sánh; không thể đổi sảnh an toàn." });
            if (quote.RequiresPriceConfirmation && (!request.PriceDifferenceConfirmed || request.ConfirmedPriceDifference != quote.PriceDifference))
                return Conflict(new { code = "ERR_PRICE_CONFIRMATION_REQUIRED", message = "Giá sảnh đã thay đổi hoặc chưa được xác nhận. Hãy tải lại chênh lệch giá và xác nhận lại.", quote });
            if (request.ChangeMenu && selection.HasExistingPricedMenu && !quote.SavedMenuPrice.HasValue)
                return Conflict(new { code = "ERR_PRICE_SNAPSHOT_UNKNOWN", message = "Không có giá thực đơn đã lưu để so sánh; không thể đổi thực đơn an toàn." });
            if (request.ChangeMenu && (!request.MenuPriceDifferenceConfirmed || request.ConfirmedMenuPriceDifference != quote.MenuPriceDifference))
                return Conflict(new { code = "ERR_MENU_PRICE_CONFIRMATION_REQUIRED", message = "Giá thực đơn đã thay đổi hoặc chưa được xác nhận. Hãy tải lại chênh lệch giá và xác nhận lại.", quote });

            if (!slotChanged && !request.ChangeMenu)
                return BadRequest(new { code = "ERR_SAME_SLOT", message = "Lịch mới trùng với lịch hiện tại." });

            if (hallChanged && quote.CapacityCheck == "UNKNOWN")
                return Conflict(new { code = "ERR_CAPACITY_UNKNOWN", message = "Không có số bàn dự phòng trong trường dữ liệu có cấu trúc đáng tin cậy; giai đoạn này chỉ hỗ trợ đổi lịch trong cùng sảnh." });
            if (hallChanged && quote.CapacityCheck != "PASS")
                return Conflict(new { code = "ERR_HALL_CAPACITY", message = "Số bàn booking không phù hợp sức chứa sảnh mới." });

            LinkedFinancialQuote? linkedFinancial = null;
            if (linkedContract is not null)
            {
                var financialResult = await BuildLinkedFinancialQuoteAsync(booking, linkedContract, quote, date, selection, cancellationToken);
                if (financialResult.Error is not null) return financialResult.Error;
                linkedFinancial = financialResult.Financial;
                if (request.PolicyVersion != Uc18ReschedulePolicy.Version ||
                    string.IsNullOrWhiteSpace(request.CustomerAgreementReference) ||
                    string.IsNullOrWhiteSpace(request.ManagerAgreementReference) ||
                    request.CustomerAgreementReference.Trim().Length > 300 || request.ManagerAgreementReference.Trim().Length > 300 ||
                    $"CUSTOMER:{request.CustomerAgreementReference.Trim()}|MANAGER:{request.ManagerAgreementReference.Trim()}".Length > 300 ||
                    string.IsNullOrWhiteSpace(request.Reason))
                    return Conflict(new { code = "ERR_RESCHEDULE_AGREEMENT_REQUIRED" });
                if (!string.Equals(request.QuoteToken, linkedFinancial!.Quote.QuoteToken, StringComparison.Ordinal))
                    return Conflict(new { code = "ERR_RESCHEDULE_QUOTE_STALE", quote = linkedFinancial.Quote });
            }

            var priceDelta = linkedFinancial?.SignedPriceDelta ??
                (hallChanged ? quote.PriceDifference : 0m) + (request.ChangeMenu ? quote.MenuPriceDifference * booking.BilledTableCount!.Value : 0m);
            var feeAmount = linkedFinancial?.FeeAmount ?? 0m;
            var updatedTotal = booking.EstimatedTotal.GetValueOrDefault() + priceDelta;
            if (linkedContract is null && (hallChanged || request.ChangeMenu) &&
                (!booking.EstimatedTotal.HasValue || booking.EstimatedTotal.Value < 0m || updatedTotal < 0m || updatedTotal > MaxStoredMoney))
                return Conflict(new { code = "ERR_ESTIMATED_TOTAL_INVALID", message = "Không thể áp dụng thay đổi vì tổng tiền dự kiến không hợp lệ hoặc âm." });

            var oldSchedule = booking.HallSchedule;
            var old = new { booking.HallScheduleId, HallId = oldSchedule.HallId, Date = oldSchedule.Date, oldSchedule.Shift, booking.FinalHallPrice, booking.MenuId, booking.FinalMenuPrice, booking.CustomMenuSnapshotJson, booking.EstimatedTotal };
            HallSchedule? targetSchedule = null;
            if (slotChanged)
            {
                var target = await ResolveAndLockScheduleAsync(hall.HallId, date, shift, cancellationToken);
                if (target.Error is not null) return target.Error;
                targetSchedule = target.Schedule!;
                if (targetSchedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
                    return Conflict(new { code = "ERR_SCHEDULE_LOCKED", message = "Khung lịch mới đang LOCKED và không thể sử dụng." });
                if (targetSchedule.Status.StatusCode is not (HallScheduleStatusCodes.Available or HallScheduleStatusCodes.Booked))
                    return Conflict(new { code = "ERR_SCHEDULE_UNAVAILABLE", message = "Khung lịch mới không khả dụng." });

                var conflicting = await _db.WeddingBookings.AnyAsync(x => x.BookingId != id && x.HallScheduleId == targetSchedule.HallScheduleId &&
                    x.Status.StatusGroup == StatusGroups.Booking && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken);
                if (conflicting) return Conflict(new { code = "ERR_SCHEDULE_CONFLICT", message = "Đã có booking khác chiếm khung lịch mới." });

                targetSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked, cancellationToken);
                booking.HallScheduleId = targetSchedule.HallScheduleId;
                booking.HallSchedule = targetSchedule;
                if (oldSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked && !await HasOtherOccupantAsync(id, oldSchedule.HallScheduleId, cancellationToken))
                    oldSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);
            }

            if (hallChanged)
                booking.FinalHallPrice = hall.RentalPrice;
            if (request.ChangeMenu)
            {
                booking.MenuId = selection.MenuId;
                booking.FinalMenuPrice = selection.PricePerTable;
                booking.CustomMenuSnapshotJson = selection.SnapshotJson;
            }
            booking.EstimatedTotal = linkedFinancial?.EffectiveTotal ?? booking.EstimatedTotal + priceDelta;

            var now = DateTime.Now;
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = slotChanged ? "BOOKING_RESCHEDULED_BY_STAFF" : "BOOKING_MENU_CHANGED_BY_STAFF", EntityName = "DatTiec", EntityId = id,
                OldData = JsonSerializer.Serialize(old),
                NewData = JsonSerializer.Serialize(new { HallId = hall.HallId, hall.HallName, Date = date, Shift = shift, quote.PriceDifference, quote.MenuPriceDifference, quote.CapacityCheck, MenuId = selection.MenuId, CustomMenuSnapshotJson = selection.SnapshotJson, EstimatedTotal = booking.EstimatedTotal, Reason = request.Reason }),
                Timestamp = now, Notes = slotChanged ? "UC18: nhân viên đổi lịch booking." : "UC18: nhân viên cập nhật thực đơn booking."
            });
            var previousEvent = await BuildEventSnapshotAsync(booking, cancellationToken, oldSchedule,
                old.MenuId, old.CustomMenuSnapshotJson, useMenuOverride: true);
            var currentEvent = await BuildEventSnapshotAsync(booking, cancellationToken);
            Uc18ContractEventSnapshot? originalEvent = null;
            if (linkedContract is not null)
            {
                var priorSnapshots = await _db.Set<Uc18BookingOperation>().Where(x => x.ContractId == linkedContract.ContractId &&
                    (x.Kind == Uc18BookingOperation.Reschedule || x.Kind == Uc18BookingOperation.MenuChange))
                    .OrderBy(x => x.RecordedAtUtc).Select(x => x.SnapshotJson).ToListAsync(cancellationToken);
                foreach (var priorSnapshot in priorSnapshots)
                {
                    if (string.IsNullOrWhiteSpace(priorSnapshot)) continue;
                    try { originalEvent = JsonSerializer.Deserialize<Uc18ContractAmendmentSnapshot>(priorSnapshot)?.OriginalEvent; }
                    catch (JsonException) { }
                    if (IsVerifiedEventSnapshot(originalEvent)) break;
                    originalEvent = null;
                }
            }
            originalEvent ??= previousEvent;
            _db.Set<Uc18BookingOperation>().Add(new Uc18BookingOperation
            {
                RequestId = request.RequestId ?? Guid.NewGuid(), BookingId = id, ContractId = linkedContract?.ContractId, ActorUserId = actorId,
                Kind = slotChanged ? Uc18BookingOperation.Reschedule : Uc18BookingOperation.MenuChange,
                PayloadHash = ComputeReschedulePayloadHash(id, request), RecordedAtUtc = DateTime.UtcNow,
                PolicyVersion = linkedContract is null ? null : Uc18ReschedulePolicy.Version,
                ManagerAgreementReference = linkedContract is null ? null : $"CUSTOMER:{request.CustomerAgreementReference!.Trim()}|MANAGER:{request.ManagerAgreementReference!.Trim()}",
                SnapshotJson = JsonSerializer.Serialize(new Uc18ContractAmendmentSnapshot
                { OriginalEvent = originalEvent, PreviousEvent = previousEvent, CurrentEvent = currentEvent, Reason = Normalize(request.Reason) }),
                SignedPriceDelta = linkedFinancial?.SignedPriceDelta ?? 0m, FeeAmount = feeAmount,
                EffectiveTotal = linkedFinancial?.EffectiveTotal, PrincipalRefundDue = linkedFinancial?.RefundDue ?? 0m,
                DepositPrincipalPortion = linkedFinancial?.DepositPrincipalPortion ?? 0m
            });
            await NotifyAssignedCoordinatorsAsync(booking, actorId,
                slotChanged ? "BOOKING_RESCHEDULED" : "BOOKING_MENU_CHANGED", cancellationToken);
            uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
            if (uc15Conflict is not null) return uc15Conflict;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(linkedContract is null
                ? new { success = true, bookingId = id, quote }
                : (object)new ManagerBookingRescheduleResultDto(request.RequestId!.Value, id, linkedContract.ContractId,
                    slotChanged ? Uc18BookingOperation.Reschedule : Uc18BookingOperation.MenuChange,
                    linkedFinancial!.SignedPriceDelta, linkedFinancial.FeeAmount, linkedFinancial.EffectiveTotal,
                    linkedFinancial.RefundDue, linkedFinancial.DepositPrincipalPortion, Uc18ReschedulePolicy.Version, false));
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } || ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_BOOKING_STATE_CONFLICT", message = "Booking hoặc lịch vừa được thay đổi đồng thời. Hãy tải lại dữ liệu." });
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] ManagerBookingCancelRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { code = "ERR_INVALID_REASON", message = "Cần nhập lý do hủy (tối đa 500 ký tự)." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await LockBookingAsync(id, cancellationToken);
            var loaded = await LoadBookingAsync(id, cancellationToken);
            if (loaded.Error is not null) return loaded.Error;
            var booking = loaded.Booking!;
            var uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
            if (uc15Conflict is not null) return uc15Conflict;
            var scopeError = await ValidateHallScopeAsync(booking.HallSchedule.HallId, cancellationToken);
            if (scopeError is not null) return scopeError;
            if (booking.Contract is not null)
            {
                if (!User.IsInRole(RoleNames.Manager)) return Forbid();
                return await CancelFinancialBookingAsync(id, booking, request, actorId, transaction, cancellationToken);
            }
            if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                return Conflict(new { code = "ERR_BOOKING_TERMINAL", message = "Booking ở trạng thái kết thúc hoặc không thể hủy." });
            if (await HasContractOrPaymentAsync(id, cancellationToken))
                return Conflict(new { code = "ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT", message = "Booking có hợp đồng hoặc bản ghi thanh toán; thao tác bị từ chối an toàn." });

            var old = new { booking.Status.StatusCode, booking.HallScheduleId, booking.CancellationReason };
            booking.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled, cancellationToken);
            booking.CancellationReason = request.Reason.Trim();
            if (booking.HallSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked && !await HasOtherOccupantAsync(id, booking.HallScheduleId, cancellationToken))
                booking.HallSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = "BOOKING_CANCELLED_BY_STAFF", EntityName = "DatTiec", EntityId = id,
                OldData = JsonSerializer.Serialize(old), NewData = JsonSerializer.Serialize(new { Status = BookingStatusCodes.Cancelled, Reason = booking.CancellationReason }),
                Timestamp = DateTime.Now, Notes = "UC18: nhân viên hủy booking không có hợp đồng/thanh toán."
            });
            await NotifyAssignedCoordinatorsAsync(booking, actorId, "BOOKING_CANCELLED", cancellationToken);
            uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
            if (uc15Conflict is not null) return uc15Conflict;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new { success = true, bookingId = id, statusCode = BookingStatusCodes.Cancelled });
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } || ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_BOOKING_STATE_CONFLICT", message = "Booking vừa được thay đổi đồng thời. Hãy tải lại dữ liệu." });
        }
    }

    [HttpPost("{id:int}/cancel-quote")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> CancelQuote(int id, [FromBody] ManagerBookingCancelQuoteRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        await LockBookingAsync(id, cancellationToken);
        var loaded = await LoadBookingAsync(id, cancellationToken);
        if (loaded.Error is not null) return loaded.Error;
        if (loaded.Booking!.Contract is null)
            return Conflict(new { code = "ERR_FINANCIAL_CANCELLATION_REQUIRES_CONTRACT" });
        var contract = await _db.Contracts.Include(x => x.Status)
            .SingleOrDefaultAsync(x => x.ContractId == loaded.Booking.Contract.ContractId, cancellationToken);
        if (contract is null) return Conflict(new { code = "ERR_CONTRACT_NOT_FOUND" });
        var uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
        if (uc15Conflict is not null) return uc15Conflict;
        if (await HasActiveContractPaymentReservationAsync(id, cancellationToken))
            return Conflict(new { code = "ERR_PAYOS_PAYMENT_PENDING", message = "Hợp đồng đang có giao dịch PayOS chưa được xác nhận." });
        var quote = await BuildFinancialCancellationQuoteAsync(loaded.Booking, contract, request, cancellationToken);
        if (quote.Error is not null) return quote.Error;
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(quote.Quote);
    }

    private async Task<IActionResult> CancelFinancialBookingAsync(int id, WeddingBooking booking,
        ManagerBookingCancelRequestDto request, int actorId, IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        if ((!request.RequestId.HasValue || request.RequestId == Guid.Empty) &&
            (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode)))
            return Conflict(new { code = "ERR_BOOKING_TERMINAL" });
        var payloadHash = ComputeFinancialCancelPayloadHash(id, request);
        var contractId = booking.Contract!.ContractId;
        await LockContractAsync(contractId, cancellationToken);
        if (await HasActiveContractPaymentReservationAsync(id, cancellationToken))
            return Conflict(new { code = "ERR_PAYOS_PAYMENT_PENDING", message = "Hợp đồng đang có giao dịch PayOS chưa được xác nhận." });
        var contract = await _db.Contracts.Include(x => x.Status)
            .SingleOrDefaultAsync(x => x.ContractId == contractId, cancellationToken);
        if (contract is null || contract.BookingId != id)
            return Conflict(new { code = "ERR_CONTRACT_BOOKING_MISMATCH" });

        var previous = await _db.Set<Uc18BookingOperation>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestId == request.RequestId.Value, cancellationToken);
        if (previous is not null)
        {
            if (previous.ActorUserId != actorId || previous.BookingId != id || previous.ContractId != contractId ||
                previous.Kind != Uc18BookingOperation.Cancel || previous.PayloadHash != payloadHash)
                return Conflict(new { code = "ERR_REQUEST_ID_REUSED" });
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new ManagerBookingCancelResultDto(previous.RequestId, id, previous.ContractId,
                previous.RetainedDeposit, previous.PrincipalRefundDue, previous.DepositPrincipalPortion,
                previous.CompensationDue, true));
        }

        if (!request.RequestId.HasValue || request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.QuoteToken))
            return BadRequest(new { code = "ERR_FINANCIAL_CANCEL_REQUEST_REQUIRED" });

        var quoteRequest = new ManagerBookingCancelQuoteRequestDto
        {
            Origin = request.Origin ?? string.Empty, PolicyVersion = request.PolicyVersion,
            AgreementReference = request.AgreementReference, AgreedLowerRetention = request.AgreedLowerRetention,
            DepositSanctionsApply = request.DepositSanctionsApply
        };
        var calculated = await BuildFinancialCancellationQuoteAsync(booking, contract, quoteRequest, cancellationToken);
        if (calculated.Error is not null) return calculated.Error;
        if (!string.Equals(calculated.Quote!.QuoteToken, request.QuoteToken, StringComparison.Ordinal))
            return Conflict(new { code = "ERR_FINANCIAL_CANCEL_QUOTE_STALE" });
        if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            return Conflict(new { code = "ERR_BOOKING_TERMINAL" });

        var old = new { BookingStatus = booking.Status.StatusCode, ContractStatus = contract.Status.StatusCode,
            booking.HallScheduleId, booking.CancellationReason };
        var result = new ManagerBookingCancelResultDto(request.RequestId.Value, id, contract.ContractId,
            calculated.Quote.RetainedDeposit, calculated.Quote.PrincipalRefundDue,
            calculated.Quote.DepositPrincipalPortion, calculated.Quote.CompensationDue, false);
        booking.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled, cancellationToken);
        booking.CancellationReason = request.Reason.Trim();
        contract.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Cancelled, cancellationToken);
        if (booking.HallSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked &&
            !await HasOtherOccupantAsync(id, booking.HallScheduleId, cancellationToken))
            booking.HallSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);

        var operation = new Uc18BookingOperation
        {
            RequestId = request.RequestId.Value, BookingId = id, ContractId = contract.ContractId, ActorUserId = actorId,
            Kind = Uc18BookingOperation.Cancel, PayloadHash = payloadHash, RecordedAtUtc = DateTime.UtcNow,
            PolicyVersion = calculated.RequiresPolicy ? request.PolicyVersion : null,
            ManagerAgreementReference = calculated.RequiresPolicy ? request.AgreementReference!.Trim() : null,
            SnapshotJson = JsonSerializer.Serialize(new { Quote = calculated.Quote, Result = result, Request = request }),
            EffectiveTotal = calculated.EffectiveTotal, RetainedDeposit = result.RetainedDeposit,
            PrincipalRefundDue = result.PrincipalRefundDue, DepositPrincipalPortion = result.DepositPrincipalPortion,
            CompensationDue = result.CompensationDue
        };
        _db.Set<Uc18BookingOperation>().Add(operation);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorId, Action = "BOOKING_CANCELLED_FINANCIALLY", EntityName = "DatTiec", EntityId = id,
            OldData = JsonSerializer.Serialize(old), NewData = JsonSerializer.Serialize(new { result, request.Origin,
                request.PolicyVersion, request.AgreementReference, Reason = request.Reason.Trim() }),
            Timestamp = DateTime.Now, Notes = "UC18: hủy booking/hợp đồng và ghi nghĩa vụ hoàn tiền; không gọi cổng thanh toán."
        });
        await NotifyAssignedCoordinatorsAsync(booking, actorId, "BOOKING_CANCELLED", cancellationToken);
        var uc15Conflict = await ValidateNoUc15ConflictAsync(id, cancellationToken);
        if (uc15Conflict is not null) return uc15Conflict;
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Ok(result);
    }

    private async Task<(ManagerBookingCancelQuoteDto? Quote, IActionResult? Error, bool RequiresPolicy, decimal EffectiveTotal)> BuildFinancialCancellationQuoteAsync(
        WeddingBooking booking, Contract contract, ManagerBookingCancelQuoteRequestDto request, CancellationToken cancellationToken)
    {
        if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            return (null, Conflict(new { code = "ERR_BOOKING_TERMINAL" }), false, 0m);
        if (contract.BookingId != booking.BookingId || contract.Status.StatusGroup != StatusGroups.Contract ||
            contract.Status.StatusCode is not (ContractStatusCodes.Draft or ContractStatusCodes.Active))
            return (null, Conflict(new { code = "ERR_CONTRACT_NOT_CANCELLABLE" }), false, 0m);
        if (request.Origin is not ("CUSTOMER" or "RIVERSIDE"))
            return (null, BadRequest(new { code = "ERR_CANCELLATION_ORIGIN_REQUIRED" }), false, 0m);
        if (request.AgreementReference?.Trim().Length > 300 || request.PolicyVersion?.Length > 50)
            return (null, BadRequest(new { code = "ERR_CANCELLATION_POLICY_REFERENCE_INVALID" }), false, 0m);
        if ((request.Origin == "RIVERSIDE" && (!request.DepositSanctionsApply.HasValue || request.AgreedLowerRetention.HasValue)) ||
            (request.Origin == "CUSTOMER" && request.DepositSanctionsApply.HasValue))
            return (null, BadRequest(new { code = "ERR_INVALID_CANCELLATION_OPTIONS" }), false, 0m);

        Uc18FinancialLedgerSnapshot ledger;
        try { ledger = await new Uc18FinancialLedger(_db).ReadAsync(contract, cancellationToken); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { return (null, Conflict(new { code = "ERR_FINANCIAL_LEDGER_INVALID" }), false, 0m); }
        if (ledger.HasPendingReceipts)
            return (null, Conflict(new { code = "ERR_PENDING_RECEIPTS" }), false, ledger.EffectiveTotal);
        if (await HasUnresolvedRefundDueAsync(contract.ContractId, cancellationToken))
            return (null, Conflict(new { code = "ERR_PRIOR_REFUND_DUE_UNRESOLVED" }), false, ledger.EffectiveTotal);
        if (booking.HallSchedule.Date.Date < DateTime.Today)
            return (null, Conflict(new { code = "ERR_EVENT_DATE_PASSED" }), false, ledger.EffectiveTotal);

        var hasPaidReceipts = ledger.Receipts.GrossCollected > 0m;
        var requiresPolicy = contract.Status.StatusCode == ContractStatusCodes.Active || hasPaidReceipts;
        if (requiresPolicy && (request.PolicyVersion != Uc18CancellationPolicy.Version ||
            string.IsNullOrWhiteSpace(request.AgreementReference)))
            return (null, Conflict(new { code = "ERR_CANCELLATION_POLICY_APPROVAL_REQUIRED" }), true, ledger.EffectiveTotal);
        if (!requiresPolicy && ledger.Receipts.GrossCollected == 0m && ledger.Receipts.GrossDeposit == 0m)
        {
            if (request.AgreedLowerRetention.HasValue)
                return (null, BadRequest(new { code = "ERR_INVALID_CANCELLATION_OPTIONS" }), false, ledger.EffectiveTotal);
            var empty = new ManagerBookingCancelQuoteDto(booking.BookingId, contract.ContractId, 0m, 0m, 0m, 0m,
                ComputeCancellationQuoteToken(booking, contract, ledger, request, 0m, 0m, 0m, 0m));
            return (empty, null, false, ledger.EffectiveTotal);
        }

        try
        {
            int noticeDays = (booking.HallSchedule.Date.Date - DateTime.Today).Days;
            decimal retained, principal, depositPortion, compensation;
            if (request.Origin == "CUSTOMER")
            {
                var customer = Uc18FinancialRules.CustomerCancellation(ledger.Receipts, noticeDays, request.AgreedLowerRetention);
                retained = customer.RetainedDeposit;
                principal = customer.PrincipalRefund;
                depositPortion = Math.Min(principal, Math.Max(0m,
                    ledger.Receipts.GrossDeposit - ledger.Receipts.DepositPrincipalReturned - retained));
                compensation = 0m;
            }
            else
            {
                var venue = Uc18FinancialRules.VenueCancellation(ledger.Receipts, request.DepositSanctionsApply.Value);
                retained = 0m;
                principal = venue.PrincipalRefund;
                depositPortion = Math.Min(principal, ledger.Receipts.GrossDeposit - ledger.Receipts.DepositPrincipalReturned);
                compensation = venue.DepositCompensation;
            }
            if (!Uc18FinancialLedger.IsValidRefundMoney(retained) || !Uc18FinancialLedger.IsValidRefundMoney(principal) ||
                !Uc18FinancialLedger.IsValidRefundMoney(depositPortion) || !Uc18FinancialLedger.IsValidRefundMoney(compensation))
                return (null, Conflict(new { code = "ERR_FINANCIAL_AMOUNT_INVALID" }), requiresPolicy, ledger.EffectiveTotal);
            var token = ComputeCancellationQuoteToken(booking, contract, ledger, request, retained, principal, depositPortion, compensation);
            return (new ManagerBookingCancelQuoteDto(booking.BookingId, contract.ContractId, retained, principal, depositPortion, compensation, token),
                null, requiresPolicy, ledger.EffectiveTotal);
        }
        catch (ArgumentException)
        {
            return (null, BadRequest(new { code = "ERR_INVALID_CANCELLATION_OPTIONS" }), requiresPolicy, ledger.EffectiveTotal);
        }
    }

    private async Task<bool> HasUnresolvedRefundDueAsync(int contractId, CancellationToken cancellationToken)
    {
        var operations = await _db.Set<Uc18BookingOperation>().Where(x => x.ContractId == contractId).ToListAsync(cancellationToken);
        var operationIds = operations.Select(x => x.RequestId).ToArray();
        var refunds = await _db.Set<Uc18RefundConfirmation>().Where(x => operationIds.Contains(x.OperationRequestId))
            .GroupBy(x => x.OperationRequestId).Select(g => new
            {
                OperationId = g.Key,
                Principal = g.Sum(x => (decimal?)x.PrincipalPaid) ?? 0m,
                Compensation = g.Sum(x => (decimal?)x.CompensationPaid) ?? 0m
            }).ToListAsync(cancellationToken);
        var paidByOperation = refunds.ToDictionary(x => x.OperationId);
        return operations.Any(operation =>
        {
            paidByOperation.TryGetValue(operation.RequestId, out var paid);
            return operation.PrincipalRefundDue > (paid?.Principal ?? 0m) ||
                operation.CompensationDue > (paid?.Compensation ?? 0m);
        });
    }

    private async Task LockContractAsync(int contractId, CancellationToken cancellationToken)
    {
        if (_db.Database.IsSqlServer())
            await _db.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {contractId}")
                .Select(x => x.ContractId).SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> HasActiveContractPaymentReservationAsync(int bookingId, CancellationToken cancellationToken) =>
        _db.PaymentTransactions.AnyAsync(item => item.DatTiecId == bookingId &&
            (item.TransactionType == PaymentTransactionTypes.Installment || item.TransactionType == PaymentTransactionTypes.Settlement) &&
            (item.TrangThai == PaymentTransactionStatusCodes.Creating || item.TrangThai == PaymentTransactionStatusCodes.Pending ||
             item.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired),
            cancellationToken);

    private static string ComputeCancellationQuoteToken(WeddingBooking booking, Contract contract,
        Uc18FinancialLedgerSnapshot ledger, ManagerBookingCancelQuoteRequestDto request,
        decimal retained, decimal principal, decimal depositPortion, decimal compensation)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            booking.BookingId, BookingStatus = booking.Status.StatusCode, booking.HallScheduleId,
            HallId = booking.HallSchedule.HallId, EventDate = booking.HallSchedule.Date.ToString("yyyy-MM-dd"),
            booking.HallSchedule.Shift, contract.ContractId, contract.Status.StatusCode, TotalValue = CanonicalDecimal(contract.TotalValue),
            GrossCollected = CanonicalDecimal(ledger.Receipts.GrossCollected), GrossDeposit = CanonicalDecimal(ledger.Receipts.GrossDeposit),
            TotalPrincipalReturned = CanonicalDecimal(ledger.Receipts.TotalPrincipalReturned),
            DepositPrincipalReturned = CanonicalDecimal(ledger.Receipts.DepositPrincipalReturned),
            EffectiveTotal = CanonicalDecimal(ledger.EffectiveTotal), request.Origin, request.PolicyVersion,
            AgreementReference = request.AgreementReference?.Trim(),
            AgreedLowerRetention = request.AgreedLowerRetention.HasValue ? CanonicalDecimal(request.AgreedLowerRetention.Value) : null,
            request.DepositSanctionsApply, RetainedDeposit = CanonicalDecimal(retained), Principal = CanonicalDecimal(principal),
            DepositPortion = CanonicalDecimal(depositPortion), Compensation = CanonicalDecimal(compensation)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ComputeFinancialCancelPayloadHash(int bookingId, ManagerBookingCancelRequestDto request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            bookingId, request.QuoteToken, Origin = request.Origin?.Trim(), PolicyVersion = request.PolicyVersion?.Trim(),
            AgreementReference = request.AgreementReference?.Trim(),
            AgreedLowerRetention = request.AgreedLowerRetention.HasValue ? CanonicalDecimal(request.AgreedLowerRetention.Value) : null,
            request.DepositSanctionsApply, Reason = request.Reason.Trim()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CanonicalDecimal(decimal value) => value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);

    private static class Uc18CancellationPolicy
    {
        public const string Version = "UC18-CANCEL-V1";
    }

    [HttpPost("{id:int}/table-count-attestation")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> AttestTableCounts(int id, [FromBody] AttestBookingTableCountsRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.BilledTableCount <= 0 || !request.SpareTableCount.HasValue || request.SpareTableCount.Value < 0 || request.SpareTableCount.Value >= request.BilledTableCount ||
            string.IsNullOrWhiteSpace(request.SourceReference) || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { code = "ERR_INVALID_TABLE_COUNTS", message = "Số bàn, nguồn đối chiếu và lý do không hợp lệ." });
        var spareTableCount = request.SpareTableCount.Value;
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await LockBookingAsync(id, cancellationToken);
            var booking = await _db.WeddingBookings.Include(x => x.Status).Include(x => x.HallSchedule).ThenInclude(x => x.Hall)
                .SingleOrDefaultAsync(x => x.BookingId == id, cancellationToken);
            if (booking is null) return NotFound(new { code = "ERR_BOOKING_NOT_FOUND", message = "Không tìm thấy booking." });
            if (booking.BilledTableCount.HasValue || booking.SpareTableCount.HasValue)
                return Conflict(new { code = "ERR_TABLE_COUNTS_ALREADY_ATTESTED", message = "Booking đã có số bàn cấu trúc; không thể ghi đè hoặc attestation lần nữa." });
            if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                return Conflict(new { code = "ERR_BOOKING_TERMINAL", message = "Chỉ booking đang hoạt động mới được attestation." });
            if (booking.Status.StatusCode == BookingStatusCodes.Pending &&
                booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
                return Conflict(new { code = "ERR_BOOKING_HOLD_EXPIRED", message = "Booking đang giữ chỗ đã hết hạn và không thể attestation." });

            var hall = booking.HallSchedule.Hall;
            var minimumTables = (int)Math.Ceiling(booking.GuestCount / 10m);
            if ((hall.MinimumCapacity.HasValue && booking.GuestCount < hall.MinimumCapacity.Value) || booking.GuestCount > hall.MaximumCapacity ||
                request.BilledTableCount - spareTableCount < minimumTables || (long)request.BilledTableCount * 10 > hall.MaximumCapacity)
                return Conflict(new { code = "ERR_TABLE_COUNTS_CAPACITY", message = "Số bàn không phù hợp số khách hoặc sức chứa sảnh hiện tại." });

            var oldValues = new { booking.BilledTableCount, booking.SpareTableCount };
            booking.BilledTableCount = request.BilledTableCount;
            booking.SpareTableCount = spareTableCount;
            var now = DateTime.Now;
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = "BOOKING_TABLE_COUNTS_ATTESTED", EntityName = "DatTiec", EntityId = id,
                OldData = JsonSerializer.Serialize(oldValues),
                NewData = JsonSerializer.Serialize(new { request.BilledTableCount, SpareTableCount = spareTableCount, SourceReference = request.SourceReference.Trim(), Reason = request.Reason.Trim() }),
                Timestamp = now, Notes = "UC18: manager attestation số bàn có nguồn đối chiếu."
            });
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new { success = true, bookingId = id, request.BilledTableCount, SpareTableCount = spareTableCount });
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } || ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_BOOKING_STATE_CONFLICT", message = "Booking vừa được thay đổi đồng thời. Hãy tải lại dữ liệu." });
        }
    }

    private async Task<(WeddingBooking? Booking, IActionResult? Error)> LoadBookingAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await _db.WeddingBookings.Include(x => x.Status).Include(x => x.Contract).ThenInclude(x => x!.Status)
            .Include(x => x.Customer).Include(x => x.HallSchedule).ThenInclude(x => x.Status)
            .Include(x => x.HallSchedule).ThenInclude(x => x.Hall).ThenInclude(x => x.Status)
            .SingleOrDefaultAsync(x => x.BookingId == id, cancellationToken);
        return booking is null
            ? (null, NotFound(new { code = "ERR_BOOKING_NOT_FOUND", message = "Không tìm thấy booking." }))
            : (booking, null);
    }

    private async Task<IActionResult?> ValidateChangeEligibilityAsync(WeddingBooking booking, CancellationToken cancellationToken)
    {
        if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            return Conflict(new { code = "ERR_BOOKING_TERMINAL", message = "Booking ở trạng thái kết thúc hoặc không thể thay đổi." });
        if (booking.Status.StatusCode == BookingStatusCodes.Pending &&
            booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
            return Conflict(new { code = "ERR_BOOKING_HOLD_EXPIRED", message = "Thời hạn giữ chỗ 48 giờ đã hết; booking không thể được xác nhận hoặc đổi lịch." });
        if (await HasContractOrPaymentAsync(booking.BookingId, cancellationToken))
            return Conflict(new { code = "ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT", message = "Booking có hợp đồng hoặc bản ghi thanh toán; UC18 stage A không sửa dữ liệu tài chính." });
        return null;
    }

    private async Task<IActionResult?> ValidateNoUc15ConflictAsync(int bookingId, CancellationToken cancellationToken)
    {
        var hasOpenRequest = await _db.WeddingScheduleChanges.AnyAsync(change => change.BookingId == bookingId &&
            ScheduleChangeStatusCodes.Open.Contains(change.Status.StatusCode), cancellationToken);
        var hasReceivedLegacyPayment = await _db.PaymentTransactions.AnyAsync(payment => payment.DatTiecId == bookingId &&
            payment.TransactionType == PaymentTransactionTypes.ScheduleChange &&
            (payment.TrangThai == PaymentTransactionStatusCodes.Paid ||
             payment.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired), cancellationToken);
        return hasOpenRequest || hasReceivedLegacyPayment
            ? Conflict(new { code = "ERR_UC15_SCHEDULE_CHANGE_OPEN", message = "Booking có yêu cầu đổi lịch UC15 đang mở hoặc giao dịch UC15 đã nhận tiền cần đối soát. Hãy hoàn tất/đối soát yêu cầu với Manager trước khi thay đổi booking." })
            : null;
    }

    private async Task<bool> HasContractOrPaymentAsync(int id, CancellationToken cancellationToken) =>
        await _db.Contracts.AnyAsync(x => x.BookingId == id, cancellationToken) ||
        await _db.Payments.AnyAsync(x => x.Contract.BookingId == id, cancellationToken);

    private async Task<IActionResult?> ValidateHallScopeAsync(int hallId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Manager)) return null;
        if (!User.IsInRole(RoleNames.HallManager) || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var today = DateTime.Today;
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var allowed = await _db.HallManagerAssignments.AnyAsync(x => x.HallId == hallId && x.HallManager.UserId == userId &&
            x.FromDate <= today && (!x.ToDate.HasValue || x.ToDate.Value >= today) &&
            x.HallManager.DataStatusId == existingId && x.HallManager.User.DataStatusId == existingId &&
            x.HallManager.User.Status.StatusCode == BusinessStatusCodes.Active &&
            x.HallManager.User.Role.RoleName == RoleNames.HallManager, cancellationToken);
        return allowed ? null : Forbid();
    }

    private async Task<(Hall? Hall, DateTime Date, string Shift, IActionResult? Error)> ResolveDestinationAsync(
        WeddingBooking booking, int hallId, DateTime date, string shiftInput, CancellationToken cancellationToken)
    {
        var shift = NormalizeShift(shiftInput);
        if (shift is null || date < DateTime.Today) return (null, date, "", BadRequest(new { code = "ERR_INVALID_SLOT", message = "Ngày/ca mới không hợp lệ." }));
        var scope = await ValidateHallScopeAsync(hallId, cancellationToken);
        if (scope is not null) return (null, date, shift, scope);
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var hall = await _db.Halls.Include(x => x.Status).SingleOrDefaultAsync(x => x.HallId == hallId && x.DataStatusId == existingId &&
            x.Status.StatusGroup == StatusGroups.Hall && x.Status.StatusCode == HallStatusCodes.Active, cancellationToken);
        if (hall is null) return (null, date, shift, Conflict(new { code = "ERR_HALL_UNAVAILABLE", message = "Sảnh mới không tồn tại hoặc không hoạt động." }));
        if (booking.GuestCount > hall.MaximumCapacity || (hall.MinimumCapacity.HasValue && booking.GuestCount < hall.MinimumCapacity.Value))
            return (null, date, shift, Conflict(new { code = "ERR_HALL_CAPACITY", message = "Số khách booking không phù hợp sức chứa sảnh mới." }));
        var hasActiveHold = await _db.ScheduleChangeHolds.AnyAsync(hold => hold.HallSchedule.HallId == hallId &&
            hold.HallSchedule.Date == date && hold.HallSchedule.Shift == shift &&
            hold.Status == ScheduleChangeHoldStatusCodes.Active && hold.ExpiresAt > RiversideBusinessClock.Now, cancellationToken);
        if (hasActiveHold)
            return (null, date, shift, Conflict(new { code = "ERR_UC15_SCHEDULE_HOLD_ACTIVE", message = "Khung lịch đang được một yêu cầu đổi lịch UC15 giữ tạm. Hãy xử lý yêu cầu hoặc chờ thời hạn giữ lịch kết thúc rồi thử lại." }));
        return (hall, date, shift, null);
    }

    private async Task<(MenuChangeSelection? Selection, IActionResult? Error)> ResolveMenuChangeAsync(
        WeddingBooking booking, ManagerBookingRescheduleQuoteRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.ChangeMenu) return (new MenuChangeSelection(false, booking.MenuId, booking.FinalMenuPrice, booking.CustomMenuSnapshotJson,
            booking.MenuId.HasValue || booking.CustomMenuSnapshotJson is not null), null);

        var hasCustomSelection = request.CustomDishIds is { Count: > 0 };
        if (request.MenuId.HasValue == hasCustomSelection || (request.CustomDishIds is { Count: 0 }))
            return (null, BadRequest(new { code = "ERR_INVALID_MENU_SELECTION", message = "Chọn đúng một thực đơn có sẵn hoặc danh sách món tùy chỉnh." }));

        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        if (request.MenuId.HasValue)
        {
            var menu = await _db.Menus.Include(x => x.Status).SingleOrDefaultAsync(x => x.MenuId == request.MenuId.Value &&
                x.DataStatusId == existingId && x.Status.StatusGroup == StatusGroups.Menu && x.Status.StatusCode == MenuStatusCodes.Active, cancellationToken);
            if (menu is null)
                return (null, Conflict(new { code = "ERR_MENU_UNAVAILABLE", message = "Thực đơn không tồn tại hoặc đã ngưng hoạt động." }));
            return (new MenuChangeSelection(true, menu.MenuId, menu.PricePerTable, null,
                booking.MenuId.HasValue || booking.CustomMenuSnapshotJson is not null), null);
        }

        var dishIds = request.CustomDishIds!;
        if (dishIds.Count > 100 || dishIds.Any(x => x <= 0) || dishIds.Distinct().Count() != dishIds.Count)
            return (null, BadRequest(new { code = "ERR_INVALID_MENU_SELECTION", message = "Danh sách món tùy chỉnh không hợp lệ hoặc bị trùng." }));
        var dishes = await _db.Dishes.Include(x => x.Status).Where(x => dishIds.Contains(x.DishId) && x.DataStatusId == existingId &&
            x.Status.StatusGroup == StatusGroups.Dish && x.Status.StatusCode == DishStatusCodes.Active).ToListAsync(cancellationToken);
        if (dishes.Count != dishIds.Count)
            return (null, Conflict(new { code = "ERR_MENU_UNAVAILABLE", message = "Một hoặc nhiều món không tồn tại hoặc đã ngưng phục vụ." }));
        var byId = dishes.ToDictionary(x => x.DishId);
        var ordered = dishIds.OrderBy(id => id).Select(id => byId[id]).ToList();
        var price = ordered.Sum(x => x.Price);
        if (price <= 0m || price > MaxStoredMoney)
            return (null, BadRequest(new { code = "ERR_INVALID_MENU_PRICE", message = "Thực đơn tùy chỉnh chưa có giá hợp lệ." }));
        var snapshot = JsonSerializer.Serialize(ordered.Select(x => new CustomMenuDishSnapshot(x.DishId, x.DishName, x.Price)));
        return (new MenuChangeSelection(true, null, price, snapshot,
            booking.MenuId.HasValue || booking.CustomMenuSnapshotJson is not null), null);
    }

    private ManagerBookingRescheduleQuoteDto BuildQuote(WeddingBooking booking, Hall hall, DateTime date, string shift, MenuChangeSelection menuChange)
    {
        var sameHall = hall.HallId == booking.HallSchedule.HallId;
        var hasBothCounts = booking.BilledTableCount.HasValue && booking.SpareTableCount.HasValue;
        var countsValid = HasValidStructuredTableCounts(booking);
        var capacityPass = countsValid && (long)booking.BilledTableCount!.Value * 10 <= hall.MaximumCapacity;
        var capacityCheck = !hasBothCounts ? "UNKNOWN" : capacityPass ? "PASS" : "FAIL";
        var hallDifference = hall.RentalPrice - (booking.FinalHallPrice ?? 0m);
        var menuDifference = menuChange.Changed ? (menuChange.PricePerTable ?? 0m) - (booking.FinalMenuPrice ?? 0m) : 0m;
        var totalDelta = (sameHall ? 0m : hallDifference) + (menuChange.Changed ? menuDifference * booking.BilledTableCount!.Value : 0m);
        var requiresPriceConfirmation = !sameHall || date != booking.HallSchedule.Date.Date || shift != NormalizeShift(booking.HallSchedule.Shift);
        return new ManagerBookingRescheduleQuoteDto
        {
            BookingId = booking.BookingId, NewHallId = hall.HallId, NewDate = date, NewShift = shift,
            SavedHallPrice = booking.FinalHallPrice, CurrentHallPrice = hall.RentalPrice,
            PriceDifference = hallDifference, RequiresPriceConfirmation = requiresPriceConfirmation,
            CapacityCheck = capacityCheck,
            CapacityMessage = !hasBothCounts
                ? sameHall ? "Không có đủ số bàn cấu trúc; giữ nguyên sảnh nên không thay đổi sức chứa." : "Không có đủ số bàn cấu trúc; đổi sảnh bị từ chối an toàn."
                : capacityPass ? "Số bàn cấu trúc phù hợp sức chứa sảnh mới." : "Số bàn cấu trúc không phù hợp sức chứa sảnh mới.",
            SpareTableCount = hasBothCounts ? booking.SpareTableCount : null,
            SavedMenuPrice = booking.FinalMenuPrice, CurrentMenuPrice = menuChange.Changed ? menuChange.PricePerTable : booking.FinalMenuPrice,
            MenuPriceDifference = menuDifference, RequiresMenuPriceConfirmation = menuChange.Changed,
            EstimatedTotalAfterChange = booking.EstimatedTotal.HasValue ? booking.EstimatedTotal.Value + totalDelta : null
        };
    }

    private async Task<(ManagerBookingRescheduleQuoteDto? Quote, LinkedFinancialQuote? Financial, IActionResult? Error)> BuildLinkedFinancialQuoteAsync(
        WeddingBooking booking, Contract linkedContract, ManagerBookingRescheduleQuoteDto quote, DateTime date,
        MenuChangeSelection menuChange, CancellationToken cancellationToken)
    {
        if (linkedContract.Status is null || linkedContract.BookingId != booking.BookingId ||
            linkedContract.Status.StatusGroup != StatusGroups.Contract ||
            linkedContract.Status.StatusCode is not (ContractStatusCodes.Active or ContractStatusCodes.Draft))
            return (null, null, Conflict(new { code = "ERR_CONTRACT_NOT_AMENDABLE" }));
        if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
            return (null, null, Conflict(new { code = "ERR_BOOKING_TERMINAL" }));
        if (booking.Status.StatusCode == BookingStatusCodes.Pending &&
            booking.BookedAt.AddHours(BookingHoldExpirationService.HoldHours) <= DateTime.Now)
            return (null, null, Conflict(new { code = "ERR_BOOKING_HOLD_EXPIRED" }));
        if (booking.HallSchedule.Date.Date < DateTime.Today)
            return (null, null, Conflict(new { code = "ERR_EVENT_DATE_PASSED" }));

        Uc18FinancialLedgerSnapshot ledger;
        try { ledger = await new Uc18FinancialLedger(_db).ReadAsync(linkedContract, cancellationToken); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { return (null, null, Conflict(new { code = "ERR_FINANCIAL_LEDGER_INVALID" })); }
        if (ledger.HasPendingReceipts) return (null, null, Conflict(new { code = "ERR_PENDING_RECEIPTS" }));
        if (await HasUnresolvedRefundDueAsync(linkedContract.ContractId, cancellationToken))
            return (null, null, Conflict(new { code = "ERR_PRIOR_REFUND_DUE_UNRESOLVED" }));
        if (linkedContract.Status.StatusCode == ContractStatusCodes.Draft && ledger.Receipts.GrossCollected == 0m)
            return (null, null, Conflict(new { code = "ERR_DRAFT_CONTRACT_REQUIRES_FINALIZATION", message = "Hợp đồng nháp chưa có khoản thu; Manager cần hoàn tất phê duyệt hợp đồng trước khi sửa đổi." }));

        var realSlotMove = quote.NewHallId != booking.HallSchedule.HallId || date.Date != booking.HallSchedule.Date.Date ||
            quote.NewShift != NormalizeShift(booking.HallSchedule.Shift);
        var hallDelta = quote.NewHallId == booking.HallSchedule.HallId ? 0m : quote.PriceDifference;
        var menuDelta = menuChange.Changed ? quote.MenuPriceDifference * (booking.BilledTableCount ?? 0) : 0m;
        var signedDelta = hallDelta + menuDelta;
        var noticeDays = (booking.HallSchedule.Date.Date - DateTime.Today).Days;
        decimal fee;
        try
        {
            fee = Uc18FinancialRules.RescheduleFee(
                ledger.Receipts.GrossDeposit - ledger.Receipts.DepositPrincipalReturned,
                noticeDays, realSlotMove, ledger.PriorRealRescheduleCount > 0);
        }
        catch (ArgumentException)
        { return (null, null, Conflict(new { code = "ERR_FINANCIAL_AMOUNT_INVALID" })); }

        var effectiveTotal = ledger.EffectiveTotal + signedDelta + fee;
        if (effectiveTotal < 0m || effectiveTotal > MaxStoredMoney || decimal.Round(effectiveTotal, 2) != effectiveTotal)
            return (null, null, Conflict(new { code = "ERR_EFFECTIVE_TOTAL_INVALID" }));
        var netCollected = ledger.Receipts.GrossCollected - ledger.Receipts.TotalPrincipalReturned;
        var refundDue = Math.Max(netCollected - effectiveTotal, 0m);
        var netNonDeposit = (ledger.Receipts.GrossCollected - ledger.Receipts.GrossDeposit) -
            (ledger.Receipts.TotalPrincipalReturned - ledger.Receipts.DepositPrincipalReturned);
        var depositPrincipalPortion = Math.Max(0m, refundDue - netNonDeposit);
        quote.IsFinancialAmendment = true;
        quote.PolicyVersion = Uc18ReschedulePolicy.Version;
        quote.SignedPriceDelta = signedDelta;
        quote.FeeAmount = fee;
        quote.EffectiveTotalAfterChange = effectiveTotal;
        quote.RefundDue = refundDue;
        quote.DepositPrincipalRefundDue = depositPrincipalPortion;
        quote.EstimatedTotalAfterChange = effectiveTotal;
        quote.MenuId = menuChange.MenuId;
        quote.CustomMenuSnapshotJson = menuChange.SnapshotJson;
        if (!Uc18FinancialLedger.IsValidRefundMoney(refundDue) || !Uc18FinancialLedger.IsValidRefundMoney(depositPrincipalPortion))
            return (null, null, Conflict(new { code = "ERR_FINANCIAL_AMOUNT_INVALID" }));
        quote.QuoteToken = ComputeRescheduleQuoteToken(booking, linkedContract, ledger, quote, fee, refundDue, depositPrincipalPortion);
        return (quote, new LinkedFinancialQuote(quote, ledger, signedDelta, fee, effectiveTotal, refundDue, depositPrincipalPortion), null);
    }

    private static string ComputeRescheduleQuoteToken(WeddingBooking booking, Contract contract, Uc18FinancialLedgerSnapshot ledger,
        ManagerBookingRescheduleQuoteDto quote, decimal fee, decimal refundDue, decimal depositPrincipalPortion)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            booking.BookingId, BookingStatus = booking.Status.StatusCode, booking.HallScheduleId,
            CurrentHallId = booking.HallSchedule.HallId, CurrentDate = booking.HallSchedule.Date.ToString("yyyy-MM-dd"),
            CurrentShift = booking.HallSchedule.Shift, contract.ContractId, ContractStatus = contract.Status.StatusCode,
            booking.GuestCount, booking.BilledTableCount, booking.SpareTableCount,
            ContractTotal = CanonicalDecimal(contract.TotalValue), LedgerTotal = CanonicalDecimal(ledger.EffectiveTotal),
            GrossCollected = CanonicalDecimal(ledger.Receipts.GrossCollected), GrossDeposit = CanonicalDecimal(ledger.Receipts.GrossDeposit),
            TotalReturned = CanonicalDecimal(ledger.Receipts.TotalPrincipalReturned), DepositReturned = CanonicalDecimal(ledger.Receipts.DepositPrincipalReturned),
            ledger.PriorRealRescheduleCount, quote.NewHallId, NewDate = quote.NewDate.ToString("yyyy-MM-dd"), quote.NewShift,
            HallDelta = CanonicalDecimal(quote.NewHallId == booking.HallSchedule.HallId ? 0m : quote.PriceDifference),
            MenuDelta = CanonicalDecimal(quote.MenuPriceDifference * (quote.RequiresMenuPriceConfirmation ? booking.BilledTableCount.GetValueOrDefault() : 0)),
            quote.MenuId, quote.CustomMenuSnapshotJson,
            CurrentMenuId = booking.MenuId, CurrentCustomMenuSnapshotJson = booking.CustomMenuSnapshotJson,
            Fee = CanonicalDecimal(fee), EffectiveTotal = CanonicalDecimal(quote.EffectiveTotalAfterChange!.Value),
            RefundDue = CanonicalDecimal(refundDue), DepositPrincipalPortion = CanonicalDecimal(depositPrincipalPortion),
            quote.CapacityCheck, quote.SavedHallPrice, quote.CurrentHallPrice, quote.SavedMenuPrice, quote.CurrentMenuPrice
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ComputeReschedulePayloadHash(int bookingId, ManagerBookingRescheduleRequestDto request)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            bookingId, request.QuoteToken, request.NewHallId, Date = request.NewDate?.Date.ToString("yyyy-MM-dd"),
            request.NewShift, request.ChangeMenu, request.MenuId, CustomDishIds = request.CustomDishIds?.OrderBy(x => x).ToArray(),
            request.PriceDifferenceConfirmed, PriceDifference = request.ConfirmedPriceDifference.HasValue ? CanonicalDecimal(request.ConfirmedPriceDifference.Value) : null,
            request.MenuPriceDifferenceConfirmed, MenuPriceDifference = request.ConfirmedMenuPriceDifference.HasValue ? CanonicalDecimal(request.ConfirmedMenuPriceDifference.Value) : null,
            request.PolicyVersion, CustomerAgreementReference = request.CustomerAgreementReference?.Trim(),
            ManagerAgreementReference = request.ManagerAgreementReference?.Trim(), Reason = request.Reason?.Trim()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private async Task<Uc18ContractEventSnapshot> BuildEventSnapshotAsync(WeddingBooking booking, CancellationToken cancellationToken,
        HallSchedule? scheduleOverride = null, int? menuIdOverride = null, string? customMenuSnapshotOverride = null, bool useMenuOverride = false)
    {
        var schedule = scheduleOverride ?? booking.HallSchedule;
        var hallName = await _db.Halls.Where(x => x.HallId == schedule.HallId).Select(x => x.HallName).SingleAsync(cancellationToken);
        var menuId = useMenuOverride ? menuIdOverride : booking.MenuId;
        var customSnapshot = useMenuOverride ? customMenuSnapshotOverride : booking.CustomMenuSnapshotJson;
        string? menuName = menuId.HasValue
            ? await _db.Menus.Where(x => x.MenuId == menuId.Value).Select(x => x.MenuName).SingleOrDefaultAsync(cancellationToken)
            : null;
        if (!string.IsNullOrWhiteSpace(customSnapshot))
        {
            try
            {
                using var document = JsonDocument.Parse(customSnapshot);
                var names = document.RootElement.EnumerateArray().Select(item => item.TryGetProperty("DishName", out var name) ? name.GetString() : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name));
                menuName = string.Join(", ", names);
            }
            catch (JsonException) { menuName = "Thực đơn tùy chỉnh"; }
        }
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var services = await _db.WeddingBookingServices.Where(x => x.BookingId == booking.BookingId && x.DataStatusId == existingId)
            .Select(x => x.ServiceItem.TenDichVu).ToListAsync(cancellationToken);
        var decorationName = booking.DecorationPackageId.HasValue
            ? await _db.DecorPackages.Where(x => x.GoiTrangTriID == booking.DecorationPackageId.Value)
                .Select(x => x.TenGoi).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new Uc18ContractEventSnapshot
        {
            Date = schedule.Date, Shift = schedule.Shift, HallName = hallName, MenuName = menuName,
            Guests = booking.GuestCount, DecorationPackageName = decorationName,
            Services = services
        };
    }

    private static bool IsVerifiedEventSnapshot(Uc18ContractEventSnapshot? snapshot) => snapshot is not null &&
        snapshot.Date != default && !string.IsNullOrWhiteSpace(snapshot.Shift) &&
        !string.IsNullOrWhiteSpace(snapshot.HallName) && snapshot.Guests > 0 && snapshot.Services is not null;

    private sealed record LinkedFinancialQuote(ManagerBookingRescheduleQuoteDto Quote, Uc18FinancialLedgerSnapshot Ledger,
        decimal SignedPriceDelta, decimal FeeAmount, decimal EffectiveTotal, decimal RefundDue, decimal DepositPrincipalPortion);

    private static class Uc18ReschedulePolicy
    {
        public const string Version = "UC18-RESCHEDULE-V1";
    }

    private static bool HasValidStructuredTableCounts(WeddingBooking booking)
    {
        if (!booking.BilledTableCount.HasValue || !booking.SpareTableCount.HasValue) return false;
        var billed = booking.BilledTableCount.Value;
        var spare = booking.SpareTableCount.Value;
        return billed is > 0 and <= 500 && spare >= 0 && spare < billed &&
            billed - spare >= (int)Math.Ceiling(booking.GuestCount / 10m);
    }

    private sealed record MenuChangeSelection(bool Changed, int? MenuId, decimal? PricePerTable, string? SnapshotJson, bool HasExistingPricedMenu);
    private sealed record CustomMenuDishSnapshot(int DishId, string DishName, decimal Price);

    private async Task<(HallSchedule? Schedule, IActionResult? Error)> ResolveAndLockScheduleAsync(int hallId, DateTime date, string shift, CancellationToken cancellationToken)
    {
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var schedule = await _db.HallSchedules.Where(x => x.HallId == hallId && x.Date == date && x.Shift == shift && x.DataStatusId == existingId)
            .Select(x => (int?)x.HallScheduleId).SingleOrDefaultAsync(cancellationToken);
        if (schedule.HasValue && _db.Database.IsSqlServer())
            await _db.HallSchedules.FromSqlInterpolated($"SELECT * FROM [LichSanh] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [LichSanhID] = {schedule.Value}").Select(x => x.HallScheduleId).SingleAsync(cancellationToken);
        if (!schedule.HasValue)
        {
            var availableId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);
            var created = new HallSchedule { HallId = hallId, Date = date, Shift = shift, StatusId = availableId, DataStatusId = existingId };
            _db.HallSchedules.Add(created);
            await _db.SaveChangesAsync(cancellationToken);
            schedule = created.HallScheduleId;
        }
        var item = await _db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == schedule.Value, cancellationToken);
        if (await _db.ScheduleChangeHolds.AnyAsync(hold => hold.HallScheduleId == item.HallScheduleId &&
                hold.Status == ScheduleChangeHoldStatusCodes.Active && hold.ExpiresAt > RiversideBusinessClock.Now, cancellationToken))
            return (null, Conflict(new { code = "ERR_UC15_SCHEDULE_HOLD_ACTIVE", message = "Khung lịch đang được một yêu cầu đổi lịch UC15 giữ tạm. Hãy xử lý yêu cầu hoặc chờ thời hạn giữ lịch kết thúc rồi thử lại." }));
        return (item, null);
    }

    private async Task LockBookingAsync(int id, CancellationToken cancellationToken)
    {
        if (_db.Database.IsSqlServer())
            await _db.WeddingBookings.FromSqlInterpolated($"SELECT * FROM [DatTiec] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [DatTiecID] = {id}").Select(x => x.BookingId).SingleOrDefaultAsync(cancellationToken);
    }

    private Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken) =>
        _db.Database.IsRelational()
            ? BeginTransactionAsync(cancellationToken)
            : Task.FromResult<IDbContextTransaction?>(null);

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private async Task<bool> HasOtherOccupantAsync(int bookingId, int scheduleId, CancellationToken cancellationToken) =>
        await _db.WeddingBookings.AnyAsync(x => x.BookingId != bookingId && x.HallScheduleId == scheduleId &&
            x.Status.StatusGroup == StatusGroups.Booking && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken);

    private async Task NotifyAssignedCoordinatorsAsync(WeddingBooking booking, int actorId, string notificationType,
        CancellationToken cancellationToken)
    {
        var recipients = await _db.CoordinationAssignments.AsNoTracking()
            .Where(assignment => assignment.BookingId == booking.BookingId &&
                assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
                assignment.Coordinator.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                assignment.Coordinator.User.Role.RoleName == RoleNames.Coordinator &&
                assignment.Coordinator.User.Status.StatusCode == AccountStatusCodes.Active &&
                assignment.Coordinator.User.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(assignment => assignment.Coordinator.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var notificationService = new NotificationService(_db);
        foreach (var recipientId in recipients)
            await notificationService.AddAsync(recipientId, actorId, notificationType,
                "Thông tin booking được cập nhật", $"Booking {booking.BookingCode} đã được cập nhật bởi nhân viên.",
                AuditEntityNames.Booking, booking.BookingId, cancellationToken);
    }

    private Task<byte> GetExistingDataStatusIdAsync(CancellationToken cancellationToken) => _db.DataStatuses
        .Where(x => x.DataStatusCode == DataStatusCodes.Existing).Select(x => x.DataStatusId).SingleAsync(cancellationToken);

    private static string? NormalizeShift(string? shift) => shift?.Trim() switch { "Ca trưa" => "Ca trưa", "Ca tối" => "Ca tối", _ => null };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}

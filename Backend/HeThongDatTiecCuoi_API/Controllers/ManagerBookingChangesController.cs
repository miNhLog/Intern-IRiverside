using System.Data;
using System.Security.Claims;
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
public sealed class ManagerBookingChangesController : ControllerBase
{
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
            MenuId = x.MenuId, MenuName = x.Menu != null ? x.Menu.MenuName : null,
            DecorationPackageId = x.DecorationPackageId, DecorationPackageName = x.DecorationPackage != null ? x.DecorationPackage.TenGoi : null,
            GuestCount = x.GuestCount, SpecialRequests = x.SpecialRequests, FinalMenuPrice = x.FinalMenuPrice,
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
        var loaded = await LoadBookingAsync(id, cancellationToken);
        if (loaded.Error is not null) return loaded.Error;
        var error = await ValidateChangeEligibilityAsync(loaded.Booking!, cancellationToken);
        if (error is not null) return error;
        error = await ValidateHallScopeAsync(loaded.Booking!.HallSchedule.HallId, cancellationToken);
        if (error is not null) return error;
        var destination = await ResolveDestinationAsync(loaded.Booking!, request.NewHallId!.Value, request.NewDate!.Value.Date, request.NewShift, cancellationToken);
        if (destination.Error is not null) return destination.Error;
        var quote = BuildQuote(loaded.Booking!, destination.Hall!, destination.Date, destination.Shift);
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
            var eligibilityError = await ValidateChangeEligibilityAsync(booking, cancellationToken);
            if (eligibilityError is not null) return eligibilityError;
            var scopeError = await ValidateHallScopeAsync(booking.HallSchedule.HallId, cancellationToken);
            if (scopeError is not null) return scopeError;

            var destination = await ResolveDestinationAsync(booking, request.NewHallId!.Value, date, shift, cancellationToken);
            if (destination.Error is not null) return destination.Error;
            var hall = destination.Hall!;
            var quote = BuildQuote(booking, hall, date, shift);
            if (quote.RequiresPriceConfirmation && !quote.SavedHallPrice.HasValue)
                return Conflict(new { code = "ERR_PRICE_SNAPSHOT_UNKNOWN", message = "Không có giá sảnh đã lưu để so sánh; không thể đổi sảnh an toàn." });
            if (quote.RequiresPriceConfirmation && (!request.PriceDifferenceConfirmed || request.ConfirmedPriceDifference != quote.PriceDifference))
                return Conflict(new { code = "ERR_PRICE_CONFIRMATION_REQUIRED", message = "Giá sảnh đã thay đổi hoặc chưa được xác nhận. Hãy tải lại chênh lệch giá và xác nhận lại.", quote });

            if (date == booking.HallSchedule.Date.Date && shift == NormalizeShift(booking.HallSchedule.Shift) && hall.HallId == booking.HallSchedule.HallId)
                return BadRequest(new { code = "ERR_SAME_SLOT", message = "Lịch mới trùng với lịch hiện tại." });

            if (hall.HallId != booking.HallSchedule.HallId && quote.CapacityCheck == "UNKNOWN")
                return Conflict(new { code = "ERR_CAPACITY_UNKNOWN", message = "Không có số bàn dự phòng trong trường dữ liệu có cấu trúc đáng tin cậy; giai đoạn này chỉ hỗ trợ đổi lịch trong cùng sảnh." });

            var target = await ResolveAndLockScheduleAsync(hall.HallId, date, shift, cancellationToken);
            if (target.Error is not null) return target.Error;
            var targetSchedule = target.Schedule!;
            if (targetSchedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
                return Conflict(new { code = "ERR_SCHEDULE_LOCKED", message = "Khung lịch mới đang LOCKED và không thể sử dụng." });
            if (targetSchedule.Status.StatusCode is not (HallScheduleStatusCodes.Available or HallScheduleStatusCodes.Booked))
                return Conflict(new { code = "ERR_SCHEDULE_UNAVAILABLE", message = "Khung lịch mới không khả dụng." });

            var conflicting = await _db.WeddingBookings.AnyAsync(x => x.BookingId != id && x.HallScheduleId == targetSchedule.HallScheduleId &&
                x.Status.StatusGroup == StatusGroups.Booking && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken);
            if (conflicting) return Conflict(new { code = "ERR_SCHEDULE_CONFLICT", message = "Đã có booking khác chiếm khung lịch mới." });

            var oldSchedule = booking.HallSchedule;
            var old = new { booking.HallScheduleId, HallId = oldSchedule.HallId, Date = oldSchedule.Date, oldSchedule.Shift, booking.FinalHallPrice };
            targetSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked, cancellationToken);
            booking.HallScheduleId = targetSchedule.HallScheduleId;
            booking.HallSchedule = targetSchedule;
            if (oldSchedule.Status.StatusCode == HallScheduleStatusCodes.Booked && !await HasOtherOccupantAsync(id, oldSchedule.HallScheduleId, cancellationToken))
                oldSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);

            var now = DateTime.Now;
            _db.WeddingScheduleChanges.Add(new WeddingScheduleChange
            {
                BookingId = id, OldHallScheduleId = oldSchedule.HallScheduleId, NewHallScheduleId = targetSchedule.HallScheduleId,
                Reason = Normalize(request.Reason), StatusId = await _statuses.GetStatusIdAsync(StatusGroups.ScheduleChange, BusinessStatusCodes.Approved, cancellationToken),
                RequestedAt = now, ProcessedAt = now
            });
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = "BOOKING_RESCHEDULED_BY_STAFF", EntityName = "DatTiec", EntityId = id,
                OldData = JsonSerializer.Serialize(old),
                NewData = JsonSerializer.Serialize(new { HallId = hall.HallId, hall.HallName, Date = date, Shift = shift, quote.PriceDifference, quote.CapacityCheck, Reason = request.Reason }),
                Timestamp = now, Notes = "UC18: nhân viên đổi lịch booking."
            });
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new { success = true, bookingId = id, quote });
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
            if (booking.Status.StatusGroup != StatusGroups.Booking || !BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode))
                return Conflict(new { code = "ERR_BOOKING_TERMINAL", message = "Booking ở trạng thái kết thúc hoặc không thể hủy." });
            if (await HasContractOrPaymentAsync(id, cancellationToken))
                return Conflict(new { code = "ERR_BOOKING_HAS_CONTRACT_OR_PAYMENT", message = "Booking có hợp đồng hoặc bản ghi thanh toán; thao tác bị từ chối an toàn." });
            var scopeError = await ValidateHallScopeAsync(booking.HallSchedule.HallId, cancellationToken);
            if (scopeError is not null) return scopeError;

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
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Ok(new { success = true, bookingId = id, statusCode = BookingStatusCodes.Cancelled });
        }
        catch (Exception ex) when (ex is SqlException { Number: 1205 or 1222 or 2601 or 2627 } || ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 or 2601 or 2627 } })
        {
            return Conflict(new { code = "ERR_BOOKING_STATE_CONFLICT", message = "Booking vừa được thay đổi đồng thời. Hãy tải lại dữ liệu." });
        }
    }

    private async Task<(WeddingBooking? Booking, IActionResult? Error)> LoadBookingAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await _db.WeddingBookings.Include(x => x.Status).Include(x => x.Contract)
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
        var existingId = await GetExistingDataStatusIdAsync(cancellationToken);
        var hall = await _db.Halls.Include(x => x.Status).SingleOrDefaultAsync(x => x.HallId == hallId && x.DataStatusId == existingId &&
            x.Status.StatusGroup == StatusGroups.Hall && x.Status.StatusCode == HallStatusCodes.Active, cancellationToken);
        if (hall is null) return (null, date, shift, Conflict(new { code = "ERR_HALL_UNAVAILABLE", message = "Sảnh mới không tồn tại hoặc không hoạt động." }));
        var scope = await ValidateHallScopeAsync(hallId, cancellationToken);
        if (scope is not null) return (null, date, shift, scope);
        if (booking.GuestCount > hall.MaximumCapacity || (hall.MinimumCapacity.HasValue && booking.GuestCount < hall.MinimumCapacity.Value))
            return (null, date, shift, Conflict(new { code = "ERR_HALL_CAPACITY", message = "Số khách booking không phù hợp sức chứa sảnh mới." }));
        return (hall, date, shift, null);
    }

    private ManagerBookingRescheduleQuoteDto BuildQuote(WeddingBooking booking, Hall hall, DateTime date, string shift)
    {
        var sameHall = hall.HallId == booking.HallSchedule.HallId;
        var savedHallPrice = booking.FinalHallPrice;
        return new ManagerBookingRescheduleQuoteDto
        {
            BookingId = booking.BookingId, NewHallId = hall.HallId, NewDate = date, NewShift = shift,
            SavedHallPrice = savedHallPrice, CurrentHallPrice = hall.RentalPrice,
            PriceDifference = hall.RentalPrice - (savedHallPrice ?? 0m), RequiresPriceConfirmation = true,
            CapacityCheck = "UNKNOWN",
            CapacityMessage = sameHall
                ? "Không có số bàn dự phòng xác thực; giữ nguyên sảnh nên không thay đổi sức chứa."
                : "Không có số bàn dự phòng xác thực; đổi sảnh bị từ chối. Giai đoạn này chỉ hỗ trợ đổi ngày/ca trong cùng sảnh.",
            SpareTableCount = null
        };
    }

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

    private Task<byte> GetExistingDataStatusIdAsync(CancellationToken cancellationToken) => _db.DataStatuses
        .Where(x => x.DataStatusCode == DataStatusCodes.Existing).Select(x => x.DataStatusId).SingleAsync(cancellationToken);

    private static string? NormalizeShift(string? shift) => shift?.Trim() switch { "Ca trưa" => "Ca trưa", "Ca tối" => "Ca tối", _ => null };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}

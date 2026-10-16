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

    public PublicBookingController(
        ApplicationDbContext context,
        IStatusService statusService,
        IMemoryCache cache,
        ILogger<PublicBookingController> logger)
    {
        _context = context;
        _statusService = statusService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>UC14: POST /api/v1/public/bookings/lookup — tra cứu bằng mã + SĐT.</summary>
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
            var phone = request.PhoneNumber.Trim();
            var existingId = await _context.DataStatuses
                .Where(x => x.DataStatusCode == DataStatusCodes.Existing)
                .Select(x => x.DataStatusId).SingleAsync();

            var booking = await _context.WeddingBookings.AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.Status)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Status)
                .Include(b => b.Menu!).ThenInclude(m => m!.MenuDishes).ThenInclude(md => md.Dish)
                .Include(b => b.DecorationPackage)
                .Include(b => b.BookingServices).ThenInclude(bs => bs.ServiceItem)
                .Include(b => b.Contract!).ThenInclude(c => c!.Payments).ThenInclude(p => p.Status)
                .Include(b => b.CoordinationAssignments).ThenInclude(a => a.Coordinator)
                .SingleOrDefaultAsync(b =>
                    b.BookingCode == code &&
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
            {
                var next = fails + 1;
                _cache.Set(rateKey, next, FailedWindow);
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });
            }

            // Tra cứu đúng -> reset đếm sai của IP này.
            _cache.Remove(rateKey);

            var detail = BuildDetailDto(booking);
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
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (request is null)
                return BadRequest(new { success = false, message = "Thiếu dữ liệu hủy giữ chỗ." });
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);
            if (request.Reason?.Trim().Length > 500)
                return BadRequest(new { success = false, message = "Lý do hủy không được vượt quá 500 ký tự." });

            var code = request.BookingCode.Trim().ToUpperInvariant();
            var phone = request.PhoneNumber.Trim();
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
                    b.Customer.PhoneNumber == phone &&
                    b.Customer.DataStatusId == existingId);

            if (booking is null)
                return NotFound(new
                {
                    success = false,
                    message = "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác."
                });

            if (booking.Status.StatusGroup != StatusGroups.Booking ||
                booking.Status.StatusCode != BookingStatusCodes.Pending)
                return BadRequest(new
                {
                    success = false,
                    code = "ERR_BOOKING_CANNOT_CANCEL",
                    message = "Chỉ có thể hủy online khi tiệc đang ở trạng thái Chờ xác nhận. Tiệc đã xác nhận/cọc vui lòng liên hệ nhân viên."
                });

            var oldStatusName = booking.Status.StatusName;
            booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Cancelled);
            booking.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách hủy giữ chỗ online"
                : request.Reason.Trim();

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
            if (!hasOtherActiveBooking && booking.HallSchedule is not null)
                booking.HallSchedule.StatusId =
                    await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "CANCEL",
                EntityName = "DatTiec",
                EntityId = booking.BookingId,
                OldData = $"MaDatTiec={booking.BookingCode};TrangThai={oldStatusName}",
                NewData = $"MaDatTiec={booking.BookingCode};TrangThai=Đã hủy;LyDoHuy={booking.CancellationReason}",
                Timestamp = DateTime.Now,
                Notes = "Khách hủy giữ chỗ online (public cancel-hold)"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
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

    private static PublicBookingDetailDto BuildDetailDto(WeddingBooking booking)
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
        var deposit = booking.Contract?.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == "SUCCESS")
            .Sum(p => p.Amount) ?? 0m;
        var remaining = Math.Max(0m, total - deposit);

        var consultant = booking.CoordinationAssignments
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
            StatusCode = booking.Status.StatusCode,
            StatusName = booking.Status.StatusName,
            CanCancelOnline = booking.Status.StatusGroup == StatusGroups.Booking
                && booking.Status.StatusCode == BookingStatusCodes.Pending,
            HoldExpiresAt = booking.BookedAt.AddHours(48),
            BookedAt = booking.BookedAt,
            CancellationReason = booking.CancellationReason,
            SpecialRequests = booking.SpecialRequests,
            ConsultantName = consultant?.Coordinator.FullName,
            ConsultantPhone = consultant?.Coordinator.PhoneNumber
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
}

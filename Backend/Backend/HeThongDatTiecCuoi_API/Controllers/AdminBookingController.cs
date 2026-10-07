using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/bookings")]
[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class AdminBookingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public AdminBookingController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] BookingFilterRequestDto filter)
    {
        var query = _context.WeddingBookings.AsNoTracking()
            .Where(b => b.Status.StatusGroup == StatusGroups.Booking);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(b => b.BookingCode.Contains(keyword) ||
                b.Customer.FullName.Contains(keyword) || b.Customer.PhoneNumber.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(filter.StatusCode) && !filter.StatusCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var statusCode = filter.StatusCode.Trim().ToUpperInvariant();
            query = query.Where(b => b.Status.StatusCode == statusCode);
        }

        if (filter.HallId is > 0)
            query = query.Where(b => b.HallSchedule.HallId == filter.HallId);
        if (filter.FromDate.HasValue)
            query = query.Where(b => b.HallSchedule.Date >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(b => b.HallSchedule.Date <= filter.ToDate.Value.Date);

        var statusCounts = await _context.WeddingBookings.AsNoTracking()
            .Where(b => b.Status.StatusGroup == StatusGroups.Booking)
            .GroupBy(b => b.Status.StatusCode)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Code, item => item.Count);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.BookedAt)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(b => new BookingSummaryDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer.FullName,
                CustomerPhone = b.Customer.PhoneNumber,
                HallName = b.HallSchedule.Hall.HallName,
                EventDate = b.HallSchedule.Date,
                Shift = b.HallSchedule.Shift,
                GuestCount = b.GuestCount,
                EstimatedTotal = b.EstimatedTotal,
                StatusCode = b.Status.StatusCode,
                StatusName = b.Status.StatusName,
                BookedAt = b.BookedAt
            }).ToListAsync();

        int Count(string code) => statusCounts.GetValueOrDefault(code);
        return Ok(new BookingListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize,
            PendingCount = Count(BookingStatusCodes.Pending),
            ConfirmedCount = Count(BookingStatusCodes.Confirmed),
            PreparingCount = Count(BookingStatusCodes.Preparing),
            CompletedCount = Count(BookingStatusCodes.Completed),
            CancelledCount = Count(BookingStatusCodes.Cancelled)
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBookingDetail(int id)
    {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var detail = await _context.WeddingBookings.AsNoTracking()
            .Where(b => b.BookingId == id)
            .Select(b => new BookingDetailDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerId = b.CustomerId,
                CustomerName = b.Customer.FullName,
                CustomerPhone = b.Customer.PhoneNumber,
                CustomerEmail = b.Customer.Email,
                HallScheduleId = b.HallScheduleId,
                HallId = b.HallSchedule.HallId,
                HallName = b.HallSchedule.Hall.HallName,
                HallCode = b.HallSchedule.Hall.HallCode,
                EventDate = b.HallSchedule.Date,
                Shift = b.HallSchedule.Shift,
                MenuId = b.MenuId,
                MenuName = b.Menu != null ? b.Menu.MenuName : null,
                DecorationPackageId = b.DecorationPackageId,
                DecorationPackageName = b.DecorationPackage != null ? b.DecorationPackage.TenGoi : null,
                GuestCount = b.GuestCount,
                SpecialRequests = b.SpecialRequests,
                FinalMenuPrice = b.FinalMenuPrice,
                FinalDecorationPrice = b.FinalDecorationPrice,
                FinalHallPrice = b.FinalHallPrice,
                EstimatedTotal = b.EstimatedTotal,
                StatusCode = b.Status.StatusCode,
                StatusName = b.Status.StatusName,
                CancellationReason = b.CancellationReason,
                BookedAt = b.BookedAt,
                Services = b.BookingServices
                    .Where(item => item.DataStatusId == existingDataStatusId)
                    .Select(item => new BookingServiceDto
                    {
                        ServiceId = item.ServiceId,
                        ServiceCode = item.ServiceItem.MaDichVu,
                        ServiceName = item.ServiceItem.TenDichVu,
                        Quantity = item.Quantity,
                        FinalUnitPrice = item.FinalUnitPrice
                    }).ToList()
            }).SingleOrDefaultAsync();

        return detail == null ? NotFound(new { message = "Không tìm thấy đơn tiệc." }) : Ok(detail);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto request)
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var customerExists = await _context.Customers.AnyAsync(c =>
            c.CustomerId == request.CustomerId && c.DataStatusId == existingId);
        if (!customerExists)
            return BadRequest(new { message = "Khách hàng không tồn tại." });

        var schedule = await _context.HallSchedules
            .Include(item => item.Hall)
            .Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallScheduleId == request.HallScheduleId &&
                item.DataStatusId == existingId && item.Hall.DataStatusId == existingId);
        if (schedule == null)
            return BadRequest(new { message = "Lịch sảnh không tồn tại." });
        if (schedule.Status.StatusGroup != StatusGroups.HallSchedule || schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
            return Conflict(new { message = "Lịch sảnh không còn ở trạng thái AVAILABLE." });
        if (request.GuestCount > schedule.Hall.MaximumCapacity ||
            (schedule.Hall.MinimumCapacity.HasValue && request.GuestCount < schedule.Hall.MinimumCapacity.Value))
            return BadRequest(new { message = "Số lượng khách không phù hợp sức chứa của sảnh." });

        Menu? menu = null;
        if (request.MenuId.HasValue)
        {
            menu = await _context.Menus.SingleOrDefaultAsync(item => item.MenuId == request.MenuId && item.DataStatusId == existingId);
            if (menu == null) return BadRequest(new { message = "Thực đơn không tồn tại." });
        }

        DecorPackage? decor = null;
        if (request.DecorationPackageId.HasValue)
        {
            decor = await _context.DecorPackages.SingleOrDefaultAsync(item => item.GoiTrangTriID == request.DecorationPackageId && item.DataStatusId == existingId);
            if (decor == null) return BadRequest(new { message = "Gói trang trí không tồn tại." });
        }

        var pendingId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Pending);
        var bookedId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
        var tableCount = EstimateTableCount(request.GuestCount);
        var menuPrice = menu?.PricePerTable;
        var hallPrice = schedule.Hall.RentalPrice;
        var decorPrice = decor?.Gia;

        var booking = new WeddingBooking
        {
            BookingCode = await GenerateBookingCodeAsync(schedule.Date),
            CustomerId = request.CustomerId,
            HallScheduleId = schedule.HallScheduleId,
            MenuId = menu?.MenuId,
            DecorationPackageId = decor?.GoiTrangTriID,
            GuestCount = request.GuestCount,
            FinalMenuPrice = menuPrice,
            FinalDecorationPrice = decorPrice,
            FinalHallPrice = hallPrice,
            EstimatedTotal = hallPrice + (menuPrice ?? 0m) * tableCount + (decorPrice ?? 0m),
            SpecialRequests = NormalizeOptional(request.SpecialRequests),
            StatusId = pendingId,
            BookedAt = DateTime.Now
        };

        schedule.StatusId = bookedId;
        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, bookingId = booking.BookingId, bookingCode = booking.BookingCode });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequestDto request)
    {
        var code = request.StatusCode.Trim().ToUpperInvariant();
        if (!BookingStatusCodes.All.Contains(code))
            return BadRequest(new { message = "Mã trạng thái booking không hợp lệ." });

        var booking = await _context.WeddingBookings.Include(b => b.HallSchedule)
            .SingleOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound(new { message = "Không tìm thấy đơn tiệc." });

        booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, code);
        booking.CancellationReason = code == BookingStatusCodes.Cancelled
            ? NormalizeOptional(request.CancellationReason) ?? "Hủy theo yêu cầu khách hàng"
            : null;

        if (code == BookingStatusCodes.Cancelled)
            await ReleaseScheduleIfUnusedAsync(booking);
        else if (BookingStatusCodes.OccupyingSchedule.Contains(code))
            booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);

        await _context.SaveChangesAsync();
        return Ok(new { success = true, statusCode = code });
    }

    private async Task ReleaseScheduleIfUnusedAsync(WeddingBooking booking)
    {
        var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
            item.BookingId != booking.BookingId && item.HallScheduleId == booking.HallScheduleId &&
            BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode));
        if (!hasOtherActiveBooking)
            booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available);
    }

    private async Task<string> GenerateBookingCodeAsync(DateTime eventDate)
    {
        string code;
        do code = $"DT-{eventDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.WeddingBookings.AnyAsync(item => item.BookingCode == code));
        return code;
    }

    private async Task<byte> GetDataStatusIdAsync(string code) => await _context.DataStatuses
        .Where(item => item.DataStatusCode == code).Select(item => item.DataStatusId).SingleAsync();
    private static int EstimateTableCount(int guestCount) => (int)Math.Ceiling(guestCount / 10m);
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

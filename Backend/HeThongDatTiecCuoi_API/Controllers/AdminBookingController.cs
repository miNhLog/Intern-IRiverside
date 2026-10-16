using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
                BookedAt = b.BookedAt,
                HasContract = b.Contract != null,
                ContractId = b.Contract != null ? b.Contract.ContractId : null,
                ContractCode = b.Contract != null ? b.Contract.ContractCode : null,
                ContractStatusCode = b.Contract != null ? b.Contract.Status.StatusCode : null
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
                HasContract = b.Contract != null,
                ContractId = b.Contract != null ? b.Contract.ContractId : null,
                ContractCode = b.Contract != null ? b.Contract.ContractCode : null,
                ContractStatusCode = b.Contract != null ? b.Contract.Status.StatusCode : null,
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

    [HttpGet("create-options")]
    public async Task<ActionResult<BookingCreateOptionsDto>> GetCreateOptions([FromQuery] DateTime? eventDate)
    {
        var date = (eventDate ?? DateTime.Today.AddMonths(1)).Date;
        if (date < DateTime.Today.AddMonths(1))
            return BadRequest(new { message = "Ngày tổ chức phải cách ngày tạo booking ít nhất 1 tháng." });
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var halls = await _context.Halls.AsNoTracking()
            .Where(x => x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active)
            .OrderBy(x => x.HallName)
            .Select(x => new BookingHallOptionDto
            {
                HallId = x.HallId, HallName = x.HallName,
                MinimumCapacity = x.MinimumCapacity ?? 0, MaximumCapacity = x.MaximumCapacity,
                RentalPrice = x.RentalPrice
            }).ToListAsync();

        var schedules = await _context.HallSchedules.AsNoTracking()
            .Where(x => x.Date == date && x.DataStatusId == existingId)
            .Select(x => new { x.HallId, x.Shift, StatusCode = x.Status.StatusCode, x.HallScheduleId })
            .ToListAsync();
        var occupiedIds = await _context.WeddingBookings.AsNoTracking()
            .Where(x => x.HallSchedule.Date == date && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode))
            .Select(x => x.HallScheduleId).ToListAsync();
        foreach (var hall in halls)
        {
            bool Available(string shift)
            {
                var schedule = schedules.FirstOrDefault(x => x.HallId == hall.HallId && x.Shift == shift);
                return schedule == null || (schedule.StatusCode == HallScheduleStatusCodes.Available && !occupiedIds.Contains(schedule.HallScheduleId));
            }
            hall.IsNoonAvailable = Available("Ca trưa");
            hall.IsEveningAvailable = Available("Ca tối");
        }

        return Ok(new BookingCreateOptionsDto
        {
            QueryDate = date.ToString("yyyy-MM-dd"), Halls = halls,
            Menus = await _context.Menus.AsNoTracking()
                .Where(x => x.DataStatusId == existingId && x.Status.StatusCode == MenuStatusCodes.Active)
                .OrderBy(x => x.PricePerTable).Select(x => new BookingMenuOptionDto { MenuId = x.MenuId, MenuName = x.MenuName, PricePerTable = x.PricePerTable }).ToListAsync(),
            Decorations = await _context.DecorPackages.AsNoTracking()
                .Where(x => x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active)
                .OrderBy(x => x.Gia).Select(x => new BookingDecorOptionDto { DecorationPackageId = x.GoiTrangTriID, PackageName = x.TenGoi, Style = x.PhongCach, Price = x.Gia }).ToListAsync(),
            Services = await _context.ServiceItems.AsNoTracking()
                .Where(x => x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active)
                .OrderBy(x => x.LoaiDichVu).ThenBy(x => x.Gia)
                .Select(x => new BookingServiceOptionDto { ServiceId = x.DichVuID, ServiceName = x.TenDichVu, ServiceType = x.LoaiDichVu, Price = x.Gia }).ToListAsync()
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto request)
    {
        if (request.EventDate.Date < DateTime.Today.AddMonths(1))
            return BadRequest(new { message = "Ngày tổ chức phải cách ngày tạo booking ít nhất 1 tháng." });
        var shift = request.Shift.Trim();
        if (shift is not ("Ca trưa" or "Ca tối"))
            return BadRequest(new { message = "Ca tổ chức không hợp lệ." });
        if (request.Services.GroupBy(x => x.ServiceId).Any(x => x.Count() > 1))
            return BadRequest(new { message = "Danh sách dịch vụ bị trùng." });

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var phone = request.PhoneNumber.Trim();
        var customer = await _context.Customers.OrderBy(c => c.CustomerId)
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone && c.DataStatusId == existingId);
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = await GenerateCustomerCodeAsync(), FullName = request.CustomerName.Trim(),
                PhoneNumber = phone, Email = NormalizeOptional(request.Email), CreatedAt = DateTime.Now, DataStatusId = existingId
            };
            _context.Customers.Add(customer);
        }
        else
        {
            customer.FullName = request.CustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(request.Email)) customer.Email = request.Email.Trim();
        }

        var hall = await _context.Halls.SingleOrDefaultAsync(x => x.HallId == request.HallId &&
            x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active);
        if (hall == null) return BadRequest(new { message = "Sảnh tiệc không tồn tại hoặc đang ngưng hoạt động." });
        var guestCount = request.TableCount * 10;
        var occupiedCapacity = (request.TableCount + request.SpareTableCount) * 10;
        if (occupiedCapacity > hall.MaximumCapacity || (hall.MinimumCapacity.HasValue && guestCount < hall.MinimumCapacity))
            return BadRequest(new { message = "Tổng số bàn không phù hợp sức chứa của sảnh." });

        var schedule = await _context.HallSchedules
            .Include(item => item.Hall)
            .Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallId == request.HallId && item.Date == request.EventDate.Date && item.Shift == shift && item.DataStatusId == existingId);
        var createdSchedule = schedule == null;
        if (createdSchedule)
        {
            schedule = new HallSchedule { HallId = hall.HallId, Hall = hall, Date = request.EventDate.Date, Shift = shift,
                StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available), DataStatusId = existingId };
            _context.HallSchedules.Add(schedule);
        }
        if (!createdSchedule && (schedule!.Status.StatusGroup != StatusGroups.HallSchedule || schedule.Status.StatusCode != HallScheduleStatusCodes.Available))
            return Conflict(new { message = "Lịch sảnh không còn ở trạng thái AVAILABLE." });
        var resolvedSchedule = schedule!;

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

        // Booking do quản trị viên lập trực tiếp đã được tư vấn/chốt cấu hình,
        // xác nhận ngay để đủ điều kiện chuyển sang bước tạo hợp đồng.
        var confirmedId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Confirmed);
        var bookedId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
        var tableCount = request.TableCount + request.SpareTableCount;
        var menuPrice = menu?.PricePerTable;
        var hallPrice = hall.RentalPrice;
        var decorPrice = decor?.Gia;

        var serviceIds = request.Services.Select(x => x.ServiceId).ToList();
        var services = await _context.ServiceItems.Where(x => serviceIds.Contains(x.DichVuID) && x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active).ToListAsync();
        if (services.Count != serviceIds.Count) return BadRequest(new { message = "Có dịch vụ không tồn tại hoặc đã ngưng cung cấp." });
        var serviceTotal = request.Services.Sum(x => services.Single(s => s.DichVuID == x.ServiceId).Gia * x.Quantity);
        var notes = new List<string> { $"Bàn chính: {request.TableCount}; bàn dự phòng: {request.SpareTableCount}" };
        if (!string.IsNullOrWhiteSpace(request.DesiredStyle)) notes.Add($"Phong cách mong muốn: {request.DesiredStyle.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.SpecialRequests)) notes.Add(request.SpecialRequests.Trim());

        var booking = new WeddingBooking
        {
            BookingCode = await GenerateBookingCodeAsync(resolvedSchedule.Date),
            Customer = customer,
            HallSchedule = resolvedSchedule,
            MenuId = menu?.MenuId,
            DecorationPackageId = decor?.GoiTrangTriID,
            GuestCount = guestCount,
            FinalMenuPrice = menuPrice,
            FinalDecorationPrice = decorPrice,
            FinalHallPrice = hallPrice,
            EstimatedTotal = hallPrice + (menuPrice ?? 0m) * tableCount + (decorPrice ?? 0m) + serviceTotal,
            SpecialRequests = string.Join("; ", notes),
            StatusId = confirmedId,
            BookedAt = DateTime.Now
        };

        foreach (var selected in request.Services)
        {
            var service = services.Single(x => x.DichVuID == selected.ServiceId);
            booking.BookingServices.Add(new WeddingBookingService { ServiceId = service.DichVuID, Quantity = selected.Quantity, FinalUnitPrice = service.Gia, DataStatusId = existingId });
        }

        resolvedSchedule.StatusId = bookedId;
        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, bookingId = booking.BookingId, bookingCode = booking.BookingCode });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequestDto request)
    {
        var code = request.StatusCode.Trim().ToUpperInvariant();
        if (!BookingStatusCodes.All.Contains(code))
            return BadRequest(new { message = "Mã trạng thái booking không hợp lệ." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
        var booking = await _context.WeddingBookings.Include(b => b.HallSchedule)
            .SingleOrDefaultAsync(b => b.BookingId == id);
        if (booking == null) return NotFound(new { message = "Không tìm thấy đơn tiệc." });

        booking.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, code);
        booking.CancellationReason = code == BookingStatusCodes.Cancelled
            ? NormalizeOptional(request.CancellationReason) ?? "Hủy theo yêu cầu khách hàng"
            : null;

        // BR03 trong cùng transaction: hủy -> giải phóng LichSanh về AVAILABLE (bỏ qua nếu schedule null);
        // chuyển sang trạng thái chiếm giữ -> LichSanh BOOKED.
        if (code == BookingStatusCodes.Cancelled)
            await ReleaseScheduleIfUnusedAsync(booking);
        else if (booking.HallSchedule is not null && BookingStatusCodes.OccupyingSchedule.Contains(code))
            booking.HallSchedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, statusCode = code });
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    private async Task ReleaseScheduleIfUnusedAsync(WeddingBooking booking)
    {
        if (booking.HallSchedule is null) return; // default-available: không có dòng vẫn coi trống.
        var hasOtherActiveBooking = await _context.WeddingBookings.AnyAsync(item =>
            item.BookingId != booking.BookingId && item.HallScheduleId == booking.HallScheduleId &&
            item.Status != null &&
            item.Status.StatusGroup == StatusGroups.Booking &&
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
    private async Task<string> GenerateCustomerCodeAsync()
    {
        string code;
        do code = $"KH-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.Customers.AnyAsync(x => x.CustomerCode == code));
        return code;
    }

    private async Task<byte> GetDataStatusIdAsync(string code) => await _context.DataStatuses
        .Where(item => item.DataStatusCode == code).Select(item => item.DataStatusId).SingleAsync();
    private static int EstimateTableCount(int guestCount) => (int)Math.Ceiling(guestCount / 10m);
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

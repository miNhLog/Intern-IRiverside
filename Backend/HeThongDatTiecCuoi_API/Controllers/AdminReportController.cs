using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Report;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/reports")]
[ApiController]
[Authorize(Roles = RoleNames.Manager)]
public class AdminReportController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("revenue-bi")]
    public async Task<IActionResult> GetRevenueBiReport(
        [FromQuery] int year = 2026,
        [FromQuery] int quarter = 0,
        [FromQuery] int month = 0,
        [FromQuery] int? hallId = null,
        [FromQuery] string? shift = null)
    {
        // 1. Kiểm tra bộ lọc và xác định phạm vi thời gian (Niên độ / Quý / Tháng)
        if (quarter < 0 || quarter > 4)
        {
            return BadRequest(new { message = "Quý không hợp lệ. Chỉ chấp nhận 0 (cả năm) hoặc 1-4." });
        }

        string? shiftFilter = string.IsNullOrWhiteSpace(shift) ? null : shift.Trim();
        if (shiftFilter != null &&
            shiftFilter != HallShiftNames.Lunch &&
            shiftFilter != HallShiftNames.Dinner)
        {
            return BadRequest(new { message = "Ca tiệc không hợp lệ. Chỉ chấp nhận 'Ca trưa' hoặc 'Ca tối'." });
        }

        if (month != 0)
        {
            if (month < 1 || month > 12)
            {
                return BadRequest(new { message = "Tháng không hợp lệ. Chỉ chấp nhận 1-12." });
            }

            if (quarter >= 1 && quarter <= 4)
            {
                int quarterStartMonth = (quarter - 1) * 3 + 1;
                if (month < quarterStartMonth || month > quarterStartMonth + 2)
                {
                    return BadRequest(new { message = $"Tháng {month} không thuộc Quý {quarter}." });
                }
            }
        }

        DateTime rangeStart;
        DateTime rangeEndExclusive;
        int daysInScope;

        if (month != 0)
        {
            rangeStart = new DateTime(year, month, 1);
            rangeEndExclusive = rangeStart.AddMonths(1);
            daysInScope = (int)(rangeEndExclusive - rangeStart).TotalDays;
        }
        else if (quarter >= 1 && quarter <= 4)
        {
            int startMonth = (quarter - 1) * 3 + 1;
            rangeStart = new DateTime(year, startMonth, 1);
            rangeEndExclusive = rangeStart.AddMonths(3);
            daysInScope = (int)(rangeEndExclusive - rangeStart).TotalDays;
        }
        else
        {
            rangeStart = new DateTime(year, 1, 1);
            rangeEndExclusive = new DateTime(year + 1, 1, 1);
            daysInScope = DateTime.IsLeapYear(year) ? 366 : 365;
        }

        // 2. Danh sách sảnh đang hoạt động (dùng cho bộ lọc và mẫu số công suất)
        var activeHalls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(hall => hall.HallId)
            .ToListAsync();

        if (hallId.HasValue && activeHalls.All(hall => hall.HallId != hallId.Value))
        {
            return BadRequest(new { message = "Sảnh không tồn tại hoặc đã ngừng hoạt động." });
        }

        var halls = hallId.HasValue
            ? activeHalls.Where(hall => hall.HallId == hallId.Value).ToList()
            : activeHalls;

        // 3. Đơn đặt tiệc tiếp nhận trong kỳ (mốc BookedAt) - phục vụ KPI tiếp nhận
        var bookings = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
                    .ThenInclude(h => h.DataStatus)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.DataStatus)
            .Include(b => b.Status)
            .AsNoTracking()
            .Where(b => b.BookedAt >= rangeStart && b.BookedAt < rangeEndExclusive &&
                        b.Status.StatusGroup == StatusGroups.Booking &&
                        b.Status.StatusCode != BookingStatusCodes.Cancelled &&
                        b.HallSchedule.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        b.HallSchedule.Hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        (hallId == null || b.HallSchedule.HallId == hallId) &&
                        (shiftFilter == null || b.HallSchedule.Shift == shiftFilter))
            .ToListAsync();

        // 4. Sự kiện theo ngày tổ chức (mốc HallSchedule.Date) - phục vụ đếm sự kiện/công suất
        var eventBookings = await _context.WeddingBookings
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Status)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.DataStatus)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
                    .ThenInclude(h => h.DataStatus)
            .Include(b => b.Status)
            .AsNoTracking()
            .Where(b => b.HallSchedule.Date >= rangeStart && b.HallSchedule.Date < rangeEndExclusive &&
                        b.Status.StatusGroup == StatusGroups.Booking &&
                        b.HallSchedule.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        b.HallSchedule.Hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        (hallId == null || b.HallSchedule.HallId == hallId) &&
                        (shiftFilter == null || b.HallSchedule.Shift == shiftFilter))
            .ToListAsync();

        // 5. Hợp đồng chính thức trong kỳ (bảng HopDong)
        var contracts = await _context.Contracts
            .Include(c => c.Status)
            .Include(c => c.Booking)
                .ThenInclude(b => b.Status)
            .Include(c => c.Booking)
                .ThenInclude(b => b.HallSchedule)
            .Include(c => c.Payments)
                .ThenInclude(p => p.Status)
            .AsNoTracking()
            .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEndExclusive &&
                        c.Status.StatusGroup == StatusGroups.Contract &&
                        c.Status.StatusCode != ContractStatusCodes.Cancelled &&
                        c.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
                        (hallId == null || c.Booking.HallSchedule.HallId == hallId) &&
                        (shiftFilter == null || c.Booking.HallSchedule.Shift == shiftFilter))
            .ToListAsync();

        // 6. Doanh thu thực thu: phiếu thu thành công theo PaymentDate của hợp đồng đã hoàn tất
        var payments = await _context.Payments
            .Include(p => p.Status)
            .Include(p => p.Contract)
                .ThenInclude(c => c.Status)
            .Include(p => p.Contract)
                .ThenInclude(c => c.Booking)
                    .ThenInclude(b => b.Status)
            .Include(p => p.Contract)
                .ThenInclude(c => c.Booking)
                    .ThenInclude(b => b.HallSchedule)
            .AsNoTracking()
            .Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEndExclusive &&
                        p.Status.StatusGroup == StatusGroups.Payment &&
                        p.Status.StatusCode == PaymentStatusCodes.Success &&
                        p.Contract.Status.StatusGroup == StatusGroups.Contract &&
                        p.Contract.Status.StatusCode == ContractStatusCodes.Completed &&
                        p.Contract.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
                        (hallId == null || p.Contract.Booking.HallSchedule.HallId == hallId) &&
                        (shiftFilter == null || p.Contract.Booking.HallSchedule.Shift == shiftFilter))
            .ToListAsync();

        // 7. Đánh giá chất lượng QR theo ngày tổ chức, không bịa điểm vệ sinh
        var reviews = await _context.WeddingReviews
            .Include(r => r.ReviewQrCode)
                .ThenInclude(q => q.Booking)
                    .ThenInclude(b => b.Status)
            .Include(r => r.ReviewQrCode)
                .ThenInclude(q => q.Booking)
                    .ThenInclude(b => b.HallSchedule)
                        .ThenInclude(hs => hs.DataStatus)
            .AsNoTracking()
            .Where(r => r.ReviewQrCode.Booking.HallSchedule.Date >= rangeStart &&
                        r.ReviewQrCode.Booking.HallSchedule.Date < rangeEndExclusive &&
                        r.ReviewQrCode.Booking.Status.StatusGroup == StatusGroups.Booking &&
                        r.ReviewQrCode.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
                        r.ReviewQrCode.Booking.HallSchedule.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        (hallId == null || r.ReviewQrCode.Booking.HallSchedule.HallId == hallId) &&
                        (shiftFilter == null || r.ReviewQrCode.Booking.HallSchedule.Shift == shiftFilter))
            .ToListAsync();

        // 8. Công suất: mẫu số là số ca khả dụng của (các) ca đang chọn, trừ ca đã khóa theo lịch.
        var shiftNames = shiftFilter != null
            ? new[] { shiftFilter }
            : new[] { HallShiftNames.Lunch, HallShiftNames.Dinner };

        var lockedSchedules = await _context.HallSchedules
            .AsNoTracking()
            .Where(s => s.Date >= rangeStart && s.Date < rangeEndExclusive &&
                        s.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        s.Hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                        (hallId == null || s.HallId == hallId) &&
                        (shiftFilter == null || s.Shift == shiftFilter) &&
                        (s.Shift == HallShiftNames.Lunch || s.Shift == HallShiftNames.Dinner) &&
                        s.Status.StatusGroup == StatusGroups.HallSchedule &&
                        s.Status.StatusCode == HallScheduleStatusCodes.Locked)
            .Select(s => new { s.HallScheduleId, s.HallId, s.Shift })
            .ToListAsync();

        // Ca đã chiếm: chỉ tính tiệc đã xác nhận/đang chuẩn bị/hoàn tất, ca hợp lệ và không bị khóa.
        List<int> OccupiedScheduleIds(IEnumerable<WeddingBooking> source) => source
            .Where(b => b.Status.StatusCode == BookingStatusCodes.Confirmed ||
                        b.Status.StatusCode == BookingStatusCodes.Preparing ||
                        b.Status.StatusCode == BookingStatusCodes.Completed)
            .Where(b => b.HallSchedule.Status.StatusCode != HallScheduleStatusCodes.Locked)
            .Where(b => b.HallSchedule.Shift == HallShiftNames.Lunch ||
                        b.HallSchedule.Shift == HallShiftNames.Dinner)
            .Select(b => b.HallScheduleId)
            .Distinct()
            .ToList();

        // 9. Tính toán tổng hợp các chỉ số KPI
        var totalActualPaid = payments.Sum(p => p.Amount);
        var totalEstimated = bookings.Sum(b => b.EstimatedTotal ?? 0m);
        var totalContractVal = contracts.Sum(c => c.TotalValue);
        var totalBookingsCount = bookings.Count;
        var confirmedBookingsCount = bookings.Count(b =>
            b.Status.StatusCode == BookingStatusCodes.Confirmed ||
            b.Status.StatusCode == BookingStatusCodes.Preparing ||
            b.Status.StatusCode == BookingStatusCodes.Completed);
        var eventsHeld = eventBookings.Count(b =>
            b.Status.StatusCode == BookingStatusCodes.Completed);
        var eventsCancelled = eventBookings.Count(b =>
            b.Status.StatusCode == BookingStatusCodes.Cancelled);
        var totalContractsCount = contracts.Count;
        var outstandingReceivables = contracts.Sum(contract => Math.Max(
            0m,
            contract.TotalValue - contract.Payments
                .Where(payment =>
                    payment.Status.StatusGroup == StatusGroups.Payment &&
                    payment.Status.StatusCode == PaymentStatusCodes.Success)
                .Sum(payment => payment.Amount)));
        var totalTables = bookings.Sum(b => (int)Math.Ceiling(b.GuestCount / 10m));

        // ARPT: Doanh thu trọn gói bình quân / bàn (giá trị dự kiến, tách khỏi thực thu)
        var avgRevPerTable = totalTables > 0 ? Math.Round(totalEstimated / totalTables, 0) : 0m;

        // Đơn giá thực đơn bình quân / bàn
        var bookingsWithMenu = bookings.Where(b => (b.FinalMenuPrice ?? 0m) > 0).ToList();
        var avgMenuPrice = bookingsWithMenu.Count > 0 ? Math.Round(bookingsWithMenu.Average(b => b.FinalMenuPrice!.Value), 0) : 0m;

        // Công suất khai thác: ca thực tế đã chiếm / số ca khả dụng của (các) ca đang chọn.
        var occupiedScheduleIds = OccupiedScheduleIds(eventBookings);
        int totalSlots = halls.Count * daysInScope * shiftNames.Length;
        int maxCapacityShifts = Math.Max(1, totalSlots - lockedSchedules.Count);
        double occupancy = Math.Round((double)occupiedScheduleIds.Count / maxCapacityShifts * 100.0, 1);

        var kpi = new KpiSummaryDto
        {
            TotalRevenue = totalActualPaid,
            EstimatedTotalRevenue = totalEstimated,
            TotalBookings = totalBookingsCount,
            ConfirmedBookings = confirmedBookingsCount,
            EventsHeld = eventsHeld,
            EventsCancelled = eventsCancelled,
            TotalContracts = totalContractsCount,
            TotalContractValue = totalContractVal,
            OutstandingReceivables = outstandingReceivables,
            TotalTables = totalTables,
            AverageRevenuePerTable = avgRevPerTable,
            AverageMenuPricePerTable = avgMenuPrice,
            AverageOccupancyRate = Math.Min(occupancy, 100.0),
            // UC18 chưa có sổ theo dõi tiền cọc bị mất trong schema hiện tại.
            ForfeitedDepositAmount = null,
            ForfeitedDepositAvailable = false
        };

        // 9. Doanh thu theo tháng (tách biệt thực thu từ ThanhToan và dự kiến từ DatTiec)
        var monthlyList = new List<MonthlyRevenueDto>();
        int minM;
        int maxM;

        if (month != 0)
        {
            minM = month;
            maxM = month;
        }
        else if (quarter >= 1 && quarter <= 4)
        {
            minM = (quarter - 1) * 3 + 1;
            maxM = minM + 2;
        }
        else
        {
            minM = 1;
            maxM = 12;
        }

        for (int m = minM; m <= maxM; m++)
        {
            var bksInMonth = bookings.Where(b => b.BookedAt.Month == m).ToList();
            var paysInMonth = payments.Where(p => p.PaymentDate.Month == m).ToList();
            var contractsInMonth = contracts.Where(c => c.CreatedDate.Month == m).ToList();

            monthlyList.Add(new MonthlyRevenueDto
            {
                Month = m,
                MonthName = $"Tháng {m}",
                TotalRevenue = paysInMonth.Sum(p => p.Amount),
                EstimatedRevenue = bksInMonth.Sum(b => b.EstimatedTotal ?? 0m),
                BookingCount = bksInMonth.Count,
                ContractCount = contractsInMonth.Count
            });
        }

        // 10. Cơ cấu doanh thu dự kiến (Ẩm thực, Sảnh, Decor)
        var totalFnb = bookings.Sum(b => (b.FinalMenuPrice ?? 0m) * (int)Math.Ceiling(b.GuestCount / 10m));
        var totalHall = bookings.Sum(b => b.FinalHallPrice ?? 0m);
        var totalDecor = bookings.Sum(b => b.FinalDecorationPrice ?? 0m);
        var grandTotal = totalFnb + totalHall + totalDecor;
        if (grandTotal == 0) grandTotal = 1;

        var structure = new List<RevenueStructureDto>
        {
            new() {
                CategoryName = "Ẩm thực & Mâm cỗ tiệc",
                Amount = totalFnb,
                Percentage = Math.Round((double)totalFnb / (double)grandTotal * 100, 1),
                ColorHex = "#B89758" // Gold
            },
            new() {
                CategoryName = "Mặt bằng Đại sảnh",
                Amount = totalHall,
                Percentage = Math.Round((double)totalHall / (double)grandTotal * 100, 1),
                ColorHex = "#1E293B" // Dark Navy
            },
            new() {
                CategoryName = "Gói Decor & Dịch vụ đi kèm",
                Amount = totalDecor,
                Percentage = Math.Round((double)totalDecor / (double)grandTotal * 100, 1),
                ColorHex = "#D97706" // Amber
            }
        };

        // 11. Hiệu suất khai thác theo từng đại sảnh (không dùng hệ số ảo)
        var hallPerformances = halls.Select(h => {
            var bks = bookings.Where(b => b.HallSchedule.HallId == h.HallId).ToList();
            var actualRevenue = payments
                .Where(p => p.Contract.Booking.HallSchedule.HallId == h.HallId)
                .Sum(p => p.Amount);
            var estimatedRevenue = bks.Sum(b => b.EstimatedTotal ?? 0m);
            var occupiedSlotCount = OccupiedScheduleIds(
                    eventBookings.Where(b => b.HallSchedule.HallId == h.HallId))
                .Count;
            int hallLockedSlots = lockedSchedules.Count(s => s.HallId == h.HallId);
            int hallMaxShifts = Math.Max(1, daysInScope * shiftNames.Length - hallLockedSlots);
            var occ = Math.Round((double)occupiedSlotCount / hallMaxShifts * 100.0, 1);
            return new HallPerformanceDto
            {
                HallId = h.HallId,
                HallCode = h.HallCode,
                HallName = h.HallName,
                BookingCount = bks.Count,
                TotalRevenue = actualRevenue,
                EstimatedRevenue = estimatedRevenue,
                OccupancyRate = Math.Min(occ, 100.0)
            };
        }).OrderByDescending(h => h.TotalRevenue).ToList();

        // 12. Công suất theo từng ca (không nhân hệ số ảo)
        var occupancyByShift = shiftNames.Select(shiftName =>
        {
            var occupied = OccupiedScheduleIds(
                    eventBookings.Where(b => b.HallSchedule.Shift == shiftName))
                .Count;
            int lockedSlots = lockedSchedules.Count(s => s.Shift == shiftName);
            int maxSlots = Math.Max(1, halls.Count * daysInScope - lockedSlots);
            return new ShiftOccupancyDto
            {
                Shift = shiftName,
                ShiftName = shiftName,
                OccupiedSlots = occupied,
                MaxSlots = maxSlots,
                OccupancyRate = Math.Min(Math.Round((double)occupied / maxSlots * 100.0, 1), 100.0)
            };
        }).ToList();

        // 13. Chất lượng đánh giá QR (chỉ các điểm có trong schema hiện tại)
        double? AverageOrNull(IEnumerable<int?> values)
        {
            var present = values.Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
            return present.Count > 0 ? Math.Round(present.Average(), 2) : null;
        }

        var quality = new QualitySummaryDto
        {
            ReviewCount = reviews.Count,
            AverageOverallScore = reviews.Count > 0
                ? Math.Round(reviews.Average(r => (double)r.OverallScore), 2)
                : null,
            AverageHallScore = AverageOrNull(reviews.Select(r => r.HallScore)),
            AverageFoodScore = AverageOrNull(reviews.Select(r => r.FoodScore)),
            AverageServiceScore = AverageOrNull(reviews.Select(r => r.ServiceScore)),
            AverageSoundScore = AverageOrNull(reviews.Select(r => r.SoundScore)),
            AverageLightingScore = AverageOrNull(reviews.Select(r => r.LightingScore)),
            AverageHygieneScore = null,
            HygieneScoreAvailable = false
        };

        // 14. Danh sách đơn đặt tiệc cưới tiếp nhận gần nhất
        var recentBookings = bookings
            .OrderByDescending(b => b.BookedAt)
            .Take(8)
            .Select(b => new RecentBookingDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer != null ? b.Customer.FullName : $"Khách hàng #{b.CustomerId}",
                HallName = b.HallSchedule.Hall != null ? b.HallSchedule.Hall.HallName : "Sảnh tiệc",
                EventDate = b.HallSchedule.Date,
                TableCount = (int)Math.Ceiling(b.GuestCount / 10m),
                TotalAmount = b.EstimatedTotal ?? 0m,
                Status = b.Status.StatusName
            }).ToList();

        return Ok(new RevenueBiReportDto
        {
            Year = year,
            Quarter = quarter,
            Month = month,
            HallId = hallId,
            Shift = shiftFilter,
            Kpi = kpi,
            MonthlyRevenues = monthlyList,
            RevenueStructures = structure,
            HallPerformances = hallPerformances,
            RecentBookings = recentBookings,
            OccupancyByShift = occupancyByShift,
            Quality = quality,
            HallOptions = activeHalls.Select(hall => new ReportHallOptionDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName
            }).ToList()
        });
    }

    [HttpGet("hall-matrix")]
    public async Task<IActionResult> GetHallScheduleMatrix([FromQuery] DateTime? startDate)
    {
        // Xác định Thứ Hai đầu tuần: Mặc định là Thứ Hai của tuần hiện tại (DateTime.Today)
        DateTime today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        DateTime mondayThisWeek = today.AddDays(-1 * diff).Date;
        DateTime start = startDate?.Date ?? mondayThisWeek;
        DateTime end = start.AddDays(6).Date;

        // 1. Khởi tạo danh sách 7 ngày trong tuần
        var days = new List<MatrixDayHeaderDto>();
        string[] dayNames = { "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật" };
        for (int i = 0; i < 7; i++)
        {
            var d = start.AddDays(i);
            days.Add(new MatrixDayHeaderDto
            {
                Date = d,
                DayOfWeekName = dayNames[i],
                FormattedDate = d.ToString("dd/MM"),
                IsWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday
            });
        }

        // 2. Lấy 9 sảnh, lịch sảnh và đơn đặt tiệc trong tuần
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(h => h.HallId)
            .ToListAsync();

        var schedules = await _context.HallSchedules
            .Include(schedule => schedule.Status)
            .AsNoTracking()
            .Where(ls =>
                ls.Date >= start &&
                ls.Date <= end &&
                ls.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                ls.Hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .ToListAsync();

        var availableStatus = await _context.Statuses.AsNoTracking()
            .FirstOrDefaultAsync(status =>
                status.StatusGroup == StatusGroups.HallSchedule &&
                status.StatusCode == HallScheduleStatusCodes.Available)
            ?? new Status { StatusId = 301, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };

        var scheduleIds = schedules.Select(s => s.HallScheduleId).ToList();

        var bookings = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .AsNoTracking()
            .Where(b => scheduleIds.Contains(b.HallScheduleId))
            .ToListAsync();

        // 3. Ghép nối thành ma trận 9 sảnh x 14 ca tiệc
        var matrixRows = new List<HallMatrixRowDto>();
        string[] shifts = { HallShiftNames.Lunch, HallShiftNames.Dinner };

        foreach (var hall in halls)
        {
            var row = new HallMatrixRowDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                Capacity = hall.MaximumCapacity
            };

            foreach (var day in days)
            {
                foreach (var shift in shifts)
                {
                    var sch = schedules.FirstOrDefault(s => s.HallId == hall.HallId && s.Date.Date == day.Date.Date && s.Shift == shift);
                    var isBooked = sch != null && (sch.Status.StatusCode == HallScheduleStatusCodes.Booked || sch.StatusId == 302);
                    var isLocked = sch != null && (sch.Status.StatusCode == HallScheduleStatusCodes.Locked || sch.StatusId == 303);

                    var slot = new HallMatrixSlotDto
                    {
                        Date = day.Date,
                        Shift = shift,
                        StatusId = sch?.StatusId ?? availableStatus.StatusId,
                        StatusCode = isBooked ? HallScheduleStatusCodes.Booked : (isLocked ? HallScheduleStatusCodes.Locked : HallScheduleStatusCodes.Available),
                        StatusText = sch?.Status.StatusName ?? availableStatus.StatusName
                    };

                    if (isBooked)
                    {
                        var bk = bookings
                            .Where(b => b.HallScheduleId == sch!.HallScheduleId && b.Status.StatusCode != BookingStatusCodes.Cancelled)
                            .OrderByDescending(b => b.BookedAt)
                            .FirstOrDefault()
                            ?? bookings.FirstOrDefault(b => b.HallScheduleId == sch!.HallScheduleId);

                        if (bk != null)
                        {
                            slot.BookingCode = bk.BookingCode;
                            slot.CustomerName = bk.Customer != null ? bk.Customer.FullName : $"KH #{bk.CustomerId}";
                            slot.TableCount = (int)Math.Ceiling(bk.GuestCount / 10m);
                            slot.BookingStatus = bk.Status.StatusName;
                        }
                        else
                        {
                            slot.BookingCode = "ĐÃ ĐẶT";
                            slot.CustomerName = "Đã đặt tiệc";
                            slot.BookingStatus = "Đã đặt";
                        }
                    }

                    row.Slots.Add(slot);
                }
            }
            matrixRows.Add(row);
        }

        return Ok(new HallScheduleMatrixDto
        {
            StartDate = start,
            EndDate = end,
            Days = days,
            Halls = matrixRows
        });
    }
}

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
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class AdminReportController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("revenue-bi")]
    public async Task<IActionResult> GetRevenueBiReport([FromQuery] int year = 2026, [FromQuery] int quarter = 0)
    {
        // 1. Xác định phạm vi thời gian (Niên độ / Quý)
        DateTime rangeStart;
        DateTime rangeEndExclusive;
        int daysInScope;

        if (quarter >= 1 && quarter <= 4)
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

        // 2. Truy vấn các đơn đặt tiệc tiếp nhận (bảng DatTiec)
        var bookings = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
            .Include(b => b.Status)
            .AsNoTracking()
            .Where(b => b.BookedAt >= rangeStart && b.BookedAt < rangeEndExclusive &&
                        b.Status.StatusGroup == StatusGroups.Booking &&
                        b.Status.StatusCode != BookingStatusCodes.Cancelled)
            .ToListAsync();

        // 3. Truy vấn các hợp đồng chính thức (bảng HopDong)
        var contracts = await _context.Contracts
            .Include(c => c.Status)
            .Include(c => c.Payments)
                .ThenInclude(p => p.Status)
            .AsNoTracking()
            .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEndExclusive &&
                        c.Status.StatusGroup == StatusGroups.Contract &&
                        c.Status.StatusCode != ContractStatusCodes.Cancelled)
            .ToListAsync();

        // 4. Truy vấn các giao dịch thanh toán thành công (bảng ThanhToan)
        var payments = await _context.Payments
            .Include(p => p.Status)
            .Include(p => p.Contract)
            .AsNoTracking()
            .Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEndExclusive &&
                        p.Status.StatusGroup == StatusGroups.Payment &&
                        p.Status.StatusCode == PaymentStatusCodes.Success)
            .ToListAsync();

        // 5. Danh sách các đại sảnh đang hoạt động
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .ToListAsync();

        // 6. Tính toán tổng hợp các chỉ số KPI
        var totalActualPaid = payments.Sum(p => p.Amount);
        var totalEstimated = bookings.Sum(b => b.EstimatedTotal ?? 0m);
        var totalContractVal = contracts.Sum(c => c.TotalValue);
        var totalBookingsCount = bookings.Count;
        var confirmedBookingsCount = bookings.Count(b =>
            b.Status.StatusCode == BookingStatusCodes.Confirmed ||
            b.Status.StatusCode == BookingStatusCodes.Preparing ||
            b.Status.StatusCode == BookingStatusCodes.Completed);
        var totalContractsCount = contracts.Count;
        var outstandingReceivables = contracts.Sum(contract => Math.Max(
            0m,
            contract.TotalValue - contract.Payments
                .Where(payment =>
                    payment.Status.StatusGroup == StatusGroups.Payment &&
                    payment.Status.StatusCode == PaymentStatusCodes.Success)
                .Sum(payment => payment.Amount)));
        var totalTables = bookings.Sum(b => (int)Math.Ceiling(b.GuestCount / 10m));

        // ARPT: Doanh thu trọn gói bình quân / bàn (gồm Ẩm thực, Sảnh và Decor)
        var avgRevPerTable = totalTables > 0 ? Math.Round(totalEstimated / totalTables, 0) : 0m;

        // Đơn giá thực đơn bình quân / bàn
        var bookingsWithMenu = bookings.Where(b => (b.FinalMenuPrice ?? 0m) > 0).ToList();
        var avgMenuPrice = bookingsWithMenu.Count > 0 ? Math.Round(bookingsWithMenu.Average(b => b.FinalMenuPrice!.Value), 0) : 0m;

        // Công suất khai thác chuẩn: (Số ca tiệc thực tế / Tổng số ca tối đa của 9 sảnh trong kỳ) * 100%
        int maxCapacityShifts = Math.Max(1, halls.Count * 2 * daysInScope);
        double occupancy = Math.Round((double)totalBookingsCount / maxCapacityShifts * 100.0, 1);

        var kpi = new KpiSummaryDto
        {
            TotalRevenue = totalActualPaid,
            EstimatedTotalRevenue = totalEstimated,
            TotalBookings = totalBookingsCount,
            ConfirmedBookings = confirmedBookingsCount,
            TotalContracts = totalContractsCount,
            TotalContractValue = totalContractVal,
            OutstandingReceivables = outstandingReceivables,
            TotalTables = totalTables,
            AverageRevenuePerTable = avgRevPerTable,
            AverageMenuPricePerTable = avgMenuPrice,
            AverageOccupancyRate = Math.Min(occupancy, 100.0)
        };

        // 7. Doanh thu theo tháng (tách biệt thực thu từ ThanhToan và dự kiến từ DatTiec)
        var monthlyList = new List<MonthlyRevenueDto>();
        int minM = quarter == 0 ? 1 : (quarter - 1) * 3 + 1;
        int maxM = quarter == 0 ? 12 : minM + 2;

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

        // 8. Cơ cấu doanh thu (Ẩm thực, Sảnh, Decor)
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

        // 9. Hiệu suất khai thác theo 9 đại sảnh (Công thức chuẩn không dùng hệ số ảo)
        var hallPerformances = halls.Select(h => {
            var bks = bookings.Where(b => b.HallSchedule != null && b.HallSchedule.HallId == h.HallId).ToList();
            var rev = bks.Sum(b => b.EstimatedTotal ?? 0m);
            int hallMaxShifts = Math.Max(1, daysInScope * 2);
            var occ = Math.Round((double)bks.Count / hallMaxShifts * 100.0, 1);
            return new HallPerformanceDto
            {
                HallId = h.HallId,
                HallCode = h.HallCode,
                HallName = h.HallName,
                BookingCount = bks.Count,
                TotalRevenue = rev,
                OccupancyRate = Math.Min(occ, 100.0)
            };
        }).OrderByDescending(h => h.TotalRevenue).ToList();

        // 10. Danh sách đơn đặt tiệc cưới tiếp nhận gần nhất
        var recentBookings = bookings
            .OrderByDescending(b => b.BookedAt)
            .Take(8)
            .Select(b => new RecentBookingDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer != null ? b.Customer.FullName : $"Khách hàng #{b.CustomerId}",
                HallName = b.HallSchedule?.Hall != null ? b.HallSchedule.Hall.HallName : "Sảnh tiệc",
                EventDate = b.HallSchedule != null ? b.HallSchedule.Date : b.BookedAt,
                TableCount = (int)Math.Ceiling(b.GuestCount / 10m),
                TotalAmount = b.EstimatedTotal ?? 0m,
                Status = b.Status.StatusName
            }).ToList();

        return Ok(new RevenueBiReportDto
        {
            Year = year,
            Quarter = quarter,
            Kpi = kpi,
            MonthlyRevenues = monthlyList,
            RevenueStructures = structure,
            HallPerformances = hallPerformances,
            RecentBookings = recentBookings
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

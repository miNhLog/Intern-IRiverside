using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class DashboardService : IDashboardService
{
    private const string SuccessfulPaymentStatus = "Thành công";
    private const string EffectiveContractStatus = "Hiệu lực";
    private const string CompletedContractStatus = "Hoàn tất";
    private const string CancelledBookingStatus = "Đã hủy";
    private const string CompletedBookingStatus = "Hoàn tất";
    private const string BlockedScheduleStatus = "Tạm khóa";
    private const string LunchSession = "Ca trưa";
    private const string DinnerSession = "Ca tối";

    private static readonly BookingStatusDefinition[] BookingStatuses =
    [
        new("pending-confirmation", "Chờ xác nhận"),
        new("confirmed", "Đã xác nhận"),
        new("deposited", "Đã cọc"),
        new("preparing", "Đang chuẩn bị"),
        new("completed", "Hoàn tất"),
        new("cancelled", "Đã hủy")
    ];

    private static readonly string[] ContractedStatuses =
    [
        EffectiveContractStatus,
        CompletedContractStatus
    ];

    private static readonly string[] StandardSessions =
    [
        LunchSession,
        DinnerSession
    ];

    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardKpiResponseDto> GetKpisAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (from, toExclusive) = GetDateRange(period);

        var cashCollected = await _context.ThanhToan
            .AsNoTracking()
            .Where(x =>
                x.NgayThanhToan >= from &&
                x.NgayThanhToan < toExclusive &&
                x.TrangThai == SuccessfulPaymentStatus)
            .SumAsync(x => (decimal?)x.SoTien, cancellationToken) ?? 0m;

        var contractedValue = await _context.HopDong
            .AsNoTracking()
            .Where(x =>
                x.DatTiec.LichSanh.Ngay >= from &&
                x.DatTiec.LichSanh.Ngay < toExclusive &&
                ContractedStatuses.Contains(x.TrangThai))
            .SumAsync(x => (decimal?)x.TongGiaTri, cancellationToken) ?? 0m;

        var bookings = await GetBookingStatusDistributionAsync(period, cancellationToken);
        var halls = await GetHallUtilizationAsync(period, cancellationToken);
        var satisfaction = await GetSatisfactionAsync(from, toExclusive, cancellationToken);

        return new DashboardKpiResponseDto
        {
            Period = ToPeriodDto(period),
            CashCollected = new MoneyMetricDto
            {
                Amount = cashCollected,
                DateBasis = "payment-date"
            },
            ContractedValue = new MoneyMetricDto
            {
                Amount = contractedValue,
                DateBasis = "event-date"
            },
            TotalBookings = bookings.TotalBookings,
            CancelledBookings = bookings.Statuses
                .Single(x => x.StatusCode == "cancelled")
                .Count,
            UnmappedStatusCount = bookings.UnmappedStatusCount,
            CancellationRate = bookings.CancellationRate,
            OccupiedSlots = halls.OccupiedSlots,
            AvailableSlots = halls.AvailableSlots,
            BlockedSlots = halls.BlockedSlots,
            HallUtilizationRate = halls.UtilizationRate,
            AverageOverallScore = satisfaction.AverageOverallScore,
            ReviewCount = satisfaction.ReviewCount,
            RatedEventCount = satisfaction.RatedEventCount,
            StarDistribution = satisfaction.StarDistribution
        };
    }

    public async Task<RevenueTrendResponseDto> GetRevenueTrendAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (from, toExclusive) = GetDateRange(period);
        Dictionary<DateOnly, decimal> cashByPeriod;
        Dictionary<DateOnly, decimal> contractsByPeriod;

        if (period.Granularity == "day")
        {
            var cashRows = await _context.ThanhToan
                .AsNoTracking()
                .Where(x =>
                    x.NgayThanhToan >= from &&
                    x.NgayThanhToan < toExclusive &&
                    x.TrangThai == SuccessfulPaymentStatus)
                .GroupBy(x => new
                {
                    x.NgayThanhToan.Year,
                    x.NgayThanhToan.Month,
                    x.NgayThanhToan.Day
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Amount = group.Sum(x => x.SoTien)
                })
                .ToListAsync(cancellationToken);

            cashByPeriod = cashRows.ToDictionary(
                x => new DateOnly(x.Year, x.Month, x.Day),
                x => x.Amount);

            var contractRows = await _context.HopDong
                .AsNoTracking()
                .Where(x =>
                    x.DatTiec.LichSanh.Ngay >= from &&
                    x.DatTiec.LichSanh.Ngay < toExclusive &&
                    ContractedStatuses.Contains(x.TrangThai))
                .GroupBy(x => new
                {
                    x.DatTiec.LichSanh.Ngay.Year,
                    x.DatTiec.LichSanh.Ngay.Month,
                    x.DatTiec.LichSanh.Ngay.Day
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Amount = group.Sum(x => x.TongGiaTri)
                })
                .ToListAsync(cancellationToken);

            contractsByPeriod = contractRows.ToDictionary(
                x => new DateOnly(x.Year, x.Month, x.Day),
                x => x.Amount);
        }
        else
        {
            var cashRows = await _context.ThanhToan
                .AsNoTracking()
                .Where(x =>
                    x.NgayThanhToan >= from &&
                    x.NgayThanhToan < toExclusive &&
                    x.TrangThai == SuccessfulPaymentStatus)
                .GroupBy(x => new
                {
                    x.NgayThanhToan.Year,
                    x.NgayThanhToan.Month
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Amount = group.Sum(x => x.SoTien)
                })
                .ToListAsync(cancellationToken);

            cashByPeriod = cashRows.ToDictionary(
                x => new DateOnly(x.Year, x.Month, 1),
                x => x.Amount);

            var contractRows = await _context.HopDong
                .AsNoTracking()
                .Where(x =>
                    x.DatTiec.LichSanh.Ngay >= from &&
                    x.DatTiec.LichSanh.Ngay < toExclusive &&
                    ContractedStatuses.Contains(x.TrangThai))
                .GroupBy(x => new
                {
                    x.DatTiec.LichSanh.Ngay.Year,
                    x.DatTiec.LichSanh.Ngay.Month
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Amount = group.Sum(x => x.TongGiaTri)
                })
                .ToListAsync(cancellationToken);

            contractsByPeriod = contractRows.ToDictionary(
                x => new DateOnly(x.Year, x.Month, 1),
                x => x.Amount);
        }

        return new RevenueTrendResponseDto
        {
            Period = ToPeriodDto(period),
            Points = BuildTrendPoints(period, cashByPeriod, contractsByPeriod)
        };
    }

    public async Task<BookingStatusDistributionResponseDto> GetBookingStatusDistributionAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (from, toExclusive) = GetDateRange(period);
        var rows = await _context.DatTiec
            .AsNoTracking()
            .Where(x => x.LichSanh.Ngay >= from && x.LichSanh.Ngay < toExclusive)
            .GroupBy(x => x.TrangThai)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var normalizedCounts = rows
            .GroupBy(x => TextNormalization.Required(x.Status), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Count), StringComparer.Ordinal);
        var knownStatuses = BookingStatuses
            .Select(x => x.DisplayName)
            .ToHashSet(StringComparer.Ordinal);
        var totalBookings = rows.Sum(x => x.Count);
        var unmappedStatusCount = normalizedCounts
            .Where(x => !knownStatuses.Contains(x.Key))
            .Sum(x => x.Value);

        var statuses = BookingStatuses
            .Select(status =>
            {
                var count = normalizedCounts.GetValueOrDefault(status.DisplayName);
                return new BookingStatusMetricDto
                {
                    StatusCode = status.Code,
                    DisplayName = TextNormalization.Required(status.DisplayName),
                    Count = count,
                    Percentage = Percentage(count, totalBookings)
                };
            })
            .ToList();
        var cancelledBookings = statuses.Single(x => x.StatusCode == "cancelled").Count;

        return new BookingStatusDistributionResponseDto
        {
            Period = ToPeriodDto(period),
            TotalBookings = totalBookings,
            UnmappedStatusCount = unmappedStatusCount,
            CancellationRate = Percentage(cancelledBookings, totalBookings),
            Statuses = statuses
        };
    }

    public async Task<HallUtilizationResponseDto> GetHallUtilizationAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (from, toExclusive) = GetDateRange(period);
        var scheduleRows = await _context.LichSanh
            .AsNoTracking()
            .Where(x =>
                x.Ngay >= from &&
                x.Ngay < toExclusive &&
                StandardSessions.Contains(x.CaToChuc))
            .GroupBy(x => new
            {
                x.SanhTiecID,
                x.SanhTiec.MaSanh,
                x.SanhTiec.TenSanh
            })
            .Select(group => new
            {
                HallId = group.Key.SanhTiecID,
                HallCode = group.Key.MaSanh,
                HallName = group.Key.TenSanh,
                AvailableSlots = group.Count(x => x.TrangThai != BlockedScheduleStatus),
                BlockedSlots = group.Count(x => x.TrangThai == BlockedScheduleStatus)
            })
            .OrderBy(x => x.HallId)
            .ToListAsync(cancellationToken);

        var occupiedRows = await _context.LichSanh
            .AsNoTracking()
            .Where(schedule =>
                schedule.Ngay >= from &&
                schedule.Ngay < toExclusive &&
                StandardSessions.Contains(schedule.CaToChuc) &&
                schedule.TrangThai != BlockedScheduleStatus &&
                _context.DatTiec.Any(booking =>
                    booking.LichSanhID == schedule.LichSanhID &&
                    booking.TrangThai != CancelledBookingStatus))
            .GroupBy(x => x.SanhTiecID)
            .Select(group => new
            {
                HallId = group.Key,
                OccupiedSlots = group.Count()
            })
            .ToDictionaryAsync(x => x.HallId, x => x.OccupiedSlots, cancellationToken);

        var halls = scheduleRows.Select(x => new HallUtilizationItemDto
        {
            HallId = x.HallId,
            HallCode = TextNormalization.Required(x.HallCode),
            HallName = TextNormalization.Required(x.HallName),
            OccupiedSlots = occupiedRows.GetValueOrDefault(x.HallId),
            AvailableSlots = x.AvailableSlots,
            BlockedSlots = x.BlockedSlots,
            UtilizationRate = Percentage(
                occupiedRows.GetValueOrDefault(x.HallId),
                x.AvailableSlots)
        }).ToList();
        var occupiedSlots = halls.Sum(x => x.OccupiedSlots);
        var availableSlots = halls.Sum(x => x.AvailableSlots);

        return new HallUtilizationResponseDto
        {
            Period = ToPeriodDto(period),
            OccupiedSlots = occupiedSlots,
            AvailableSlots = availableSlots,
            BlockedSlots = halls.Sum(x => x.BlockedSlots),
            UtilizationRate = Percentage(occupiedSlots, availableSlots),
            Halls = halls
        };
    }

    public async Task<UpcomingEventsResponseDto> GetUpcomingEventsAsync(
        int daysAhead,
        int limit,
        CancellationToken cancellationToken)
    {
        var from = DateTime.Today;
        var toExclusive = from.AddDays(daysAhead);
        var rows = await _context.DatTiec
            .AsNoTracking()
            .Where(x =>
                x.LichSanh.Ngay >= from &&
                x.LichSanh.Ngay < toExclusive &&
                x.TrangThai != CancelledBookingStatus &&
                x.TrangThai != CompletedBookingStatus)
            .OrderBy(x => x.LichSanh.Ngay)
            .ThenBy(x => x.LichSanh.CaToChuc == LunchSession
                ? 0
                : x.LichSanh.CaToChuc == DinnerSession ? 1 : 2)
            .ThenBy(x => x.DatTiecID)
            .Select(x => new
            {
                BookingId = x.DatTiecID,
                BookingCode = x.MaDatTiec,
                EventDate = x.LichSanh.Ngay,
                EventSession = x.LichSanh.CaToChuc,
                BookingStatus = x.TrangThai,
                HallId = x.LichSanh.SanhTiecID,
                HallCode = x.LichSanh.SanhTiec.MaSanh,
                HallName = x.LichSanh.SanhTiec.TenSanh,
                CustomerId = x.KhachHangID,
                CustomerName = x.KhachHang.HoTen,
                GuestCount = x.SoLuongKhach,
                TableCount = x.SoBan
            })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new UpcomingEventsResponseDto
        {
            FromDate = DateOnly.FromDateTime(from),
            ToDate = DateOnly.FromDateTime(toExclusive.AddDays(-1)),
            Events = rows.Select(x => new UpcomingEventDto
            {
                BookingId = x.BookingId,
                BookingCode = TextNormalization.Required(x.BookingCode),
                EventDate = DateOnly.FromDateTime(x.EventDate),
                EventSession = TextNormalization.Required(x.EventSession),
                BookingStatus = TextNormalization.Required(x.BookingStatus),
                HallId = x.HallId,
                HallCode = TextNormalization.Required(x.HallCode),
                HallName = TextNormalization.Required(x.HallName),
                CustomerId = x.CustomerId,
                CustomerName = TextNormalization.Required(x.CustomerName),
                GuestCount = x.GuestCount,
                TableCount = x.TableCount
            }).ToList()
        };
    }

    private async Task<SatisfactionSummary> GetSatisfactionAsync(
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        var reviews = _context.DanhGia
            .AsNoTracking()
            .Where(x =>
                x.MaQRDanhGia.DatTiec.LichSanh.Ngay >= from &&
                x.MaQRDanhGia.DatTiec.LichSanh.Ngay < toExclusive &&
                x.DiemTongThe >= 1 &&
                x.DiemTongThe <= 5);

        var summary = await reviews
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ReviewCount = group.Count(),
                RatedEventCount = group.Select(x => x.MaQRDanhGia.DatTiecID).Distinct().Count(),
                AverageOverallScore = group.Average(x => (decimal)x.DiemTongThe)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var rows = await reviews
            .GroupBy(x => x.DiemTongThe)
            .Select(group => new
            {
                Stars = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return new SatisfactionSummary(
            summary is null ? null : decimal.Round(summary.AverageOverallScore, 2),
            summary?.ReviewCount ?? 0,
            summary?.RatedEventCount ?? 0,
            Enumerable.Range(1, 5)
                .Select(stars => new StarDistributionItemDto
                {
                    Stars = stars,
                    Count = rows.Where(x => x.Stars == stars).Sum(x => x.Count)
                })
                .ToList());
    }

    private static IReadOnlyList<RevenueTrendPointDto> BuildTrendPoints(
        DashboardPeriod period,
        IReadOnlyDictionary<DateOnly, decimal> cashByPeriod,
        IReadOnlyDictionary<DateOnly, decimal> contractsByPeriod)
    {
        var points = new List<RevenueTrendPointDto>();
        if (period.Granularity == "day")
        {
            for (var date = period.FromDate; date <= period.ToDate; date = date.AddDays(1))
            {
                points.Add(new RevenueTrendPointDto
                {
                    PeriodStart = date,
                    PeriodEnd = date,
                    CashCollected = cashByPeriod.GetValueOrDefault(date),
                    ContractedValue = contractsByPeriod.GetValueOrDefault(date)
                });
            }

            return points;
        }

        var month = new DateOnly(period.FromDate.Year, period.FromDate.Month, 1);
        while (month <= period.ToDate)
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            points.Add(new RevenueTrendPointDto
            {
                PeriodStart = month < period.FromDate ? period.FromDate : month,
                PeriodEnd = monthEnd > period.ToDate ? period.ToDate : monthEnd,
                CashCollected = cashByPeriod.GetValueOrDefault(month),
                ContractedValue = contractsByPeriod.GetValueOrDefault(month)
            });
            month = month.AddMonths(1);
        }

        return points;
    }

    private static (DateTime From, DateTime ToExclusive) GetDateRange(DashboardPeriod period) =>
        (
            period.FromDate.ToDateTime(TimeOnly.MinValue),
            period.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue)
        );

    private static DashboardPeriodDto ToPeriodDto(DashboardPeriod period) => new()
    {
        FromDate = period.FromDate,
        ToDate = period.ToDate,
        Granularity = period.Granularity
    };

    private static decimal Percentage(int numerator, int denominator) =>
        denominator == 0
            ? 0m
            : decimal.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    private sealed record BookingStatusDefinition(string Code, string DisplayName);

    private sealed record SatisfactionSummary(
        decimal? AverageOverallScore,
        int ReviewCount,
        int RatedEventCount,
        IReadOnlyList<StarDistributionItemDto> StarDistribution);
}

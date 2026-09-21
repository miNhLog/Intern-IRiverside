using HeThongDatTiecCuoi_API.DTOs.Dashboard;

namespace HeThongDatTiecCuoi_API.Services;

public sealed record DashboardPeriod(DateOnly FromDate, DateOnly ToDate, string Granularity);

public interface IDashboardService
{
    Task<DashboardKpiResponseDto> GetKpisAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken);

    Task<RevenueTrendResponseDto> GetRevenueTrendAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken);

    Task<BookingStatusDistributionResponseDto> GetBookingStatusDistributionAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken);

    Task<HallUtilizationResponseDto> GetHallUtilizationAsync(
        DashboardPeriod period,
        CancellationToken cancellationToken);

    Task<UpcomingEventsResponseDto> GetUpcomingEventsAsync(
        int daysAhead,
        int limit,
        CancellationToken cancellationToken);
}

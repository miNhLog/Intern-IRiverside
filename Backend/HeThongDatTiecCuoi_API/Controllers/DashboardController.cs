using System.Globalization;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.Dashboard;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class DashboardController : ControllerBase
{
    private const int MaximumReportDays = 366;
    private const int MaximumUpcomingDays = 14;
    private const int MaximumUpcomingEvents = 5;

    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("kpis")]
    [ProducesResponseType<DashboardKpiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DashboardPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePeriod(query, out var period, out var error))
        {
            return BadRequest(error);
        }

        return Ok(await _dashboardService.GetKpisAsync(period, cancellationToken));
    }

    [HttpGet("revenue-trend")]
    [ProducesResponseType<RevenueTrendResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRevenueTrend(
        [FromQuery] DashboardPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePeriod(query, out var period, out var error))
        {
            return BadRequest(error);
        }

        return Ok(await _dashboardService.GetRevenueTrendAsync(period, cancellationToken));
    }

    [HttpGet("booking-status-distribution")]
    [ProducesResponseType<BookingStatusDistributionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBookingStatusDistribution(
        [FromQuery] DashboardPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePeriod(query, out var period, out var error))
        {
            return BadRequest(error);
        }

        return Ok(await _dashboardService.GetBookingStatusDistributionAsync(period, cancellationToken));
    }

    [HttpGet("hall-utilization")]
    [ProducesResponseType<HallUtilizationResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHallUtilization(
        [FromQuery] DashboardPeriodQueryDto query,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePeriod(query, out var period, out var error))
        {
            return BadRequest(error);
        }

        return Ok(await _dashboardService.GetHallUtilizationAsync(period, cancellationToken));
    }

    [HttpGet("upcoming-events")]
    [ProducesResponseType<UpcomingEventsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUpcomingEvents(
        [FromQuery] int daysAhead = MaximumUpcomingDays,
        [FromQuery] int limit = MaximumUpcomingEvents,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (daysAhead is < 1 or > MaximumUpcomingDays)
        {
            errors["daysAhead"] = [$"daysAhead must be between 1 and {MaximumUpcomingDays}."];
        }

        if (limit is < 1 or > MaximumUpcomingEvents)
        {
            errors["limit"] = [$"limit must be between 1 and {MaximumUpcomingEvents}."];
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ApiErrorResponse("The dashboard filter is invalid.", errors));
        }

        return Ok(await _dashboardService.GetUpcomingEventsAsync(
            daysAhead,
            limit,
            cancellationToken));
    }

    private static bool TryResolvePeriod(
        DashboardPeriodQueryDto query,
        out DashboardPeriod period,
        out ApiErrorResponse? error)
    {
        var errors = new Dictionary<string, string[]>();
        var hasFromDate = query.FromDate is not null;
        var hasToDate = query.ToDate is not null;
        if (hasFromDate != hasToDate)
        {
            errors[hasFromDate ? "toDate" : "fromDate"] =
                ["fromDate and toDate must be provided together."];
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromDate = new DateOnly(today.Year, today.Month, 1);
        var toDate = new DateOnly(
            today.Year,
            today.Month,
            DateTime.DaysInMonth(today.Year, today.Month));

        if (hasFromDate && !DateOnly.TryParseExact(
                query.FromDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out fromDate))
        {
            errors["fromDate"] = ["fromDate must use the yyyy-MM-dd format."];
        }

        if (hasToDate && !DateOnly.TryParseExact(
                query.ToDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out toDate))
        {
            errors["toDate"] = ["toDate must use the yyyy-MM-dd format."];
        }

        if (!errors.ContainsKey("fromDate") &&
            !errors.ContainsKey("toDate") &&
            fromDate > toDate)
        {
            errors["fromDate"] = ["fromDate must not be later than toDate."];
        }
        else if (!errors.ContainsKey("toDate") &&
                 toDate.Year == DateOnly.MaxValue.Year &&
                 toDate.Month == DateOnly.MaxValue.Month)
        {
            errors["toDate"] = ["toDate is outside the supported range."];
        }
        else if (!errors.ContainsKey("fromDate") &&
                 !errors.ContainsKey("toDate") &&
                 toDate.DayNumber - fromDate.DayNumber + 1 > MaximumReportDays)
        {
            errors["toDate"] = [$"The reporting period must not exceed {MaximumReportDays} days."];
        }

        var granularity = string.IsNullOrWhiteSpace(query.Granularity)
            ? "month"
            : query.Granularity.Trim().ToLowerInvariant();
        if (granularity is not ("day" or "month"))
        {
            errors["granularity"] = ["granularity must be day or month."];
        }

        period = new DashboardPeriod(fromDate, toDate, granularity);
        error = errors.Count == 0
            ? null
            : new ApiErrorResponse("The dashboard filter is invalid.", errors);
        return error is null;
    }
}

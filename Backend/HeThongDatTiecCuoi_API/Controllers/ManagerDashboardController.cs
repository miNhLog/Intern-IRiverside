using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Dashboard;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/dashboard")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ManagerDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ManagerDashboardDto>> Get(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var upcomingEnd = today.AddDays(8);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var sevenDaysAgo = now.AddDays(-7);
        var activeBookingStatuses = BookingStatusCodes.OccupyingSchedule;

        var todayEventCount = await _context.WeddingBookings.AsNoTracking()
            .CountAsync(booking => booking.HallSchedule.Date >= today &&
                booking.HallSchedule.Date < tomorrow &&
                activeBookingStatuses.Contains(booking.Status.StatusCode), cancellationToken);

        var upcomingEventCount = await _context.WeddingBookings.AsNoTracking()
            .CountAsync(booking => booking.HallSchedule.Date >= tomorrow &&
                booking.HallSchedule.Date < upcomingEnd &&
                activeBookingStatuses.Contains(booking.Status.StatusCode), cancellationToken);

        var pendingBookingCount = await _context.WeddingBookings.AsNoTracking()
            .CountAsync(booking => booking.Status.StatusCode == BookingStatusCodes.Pending, cancellationToken);

        var newBookingCount = await _context.WeddingBookings.AsNoTracking()
            .CountAsync(booking => booking.BookedAt >= sevenDaysAgo, cancellationToken);

        var revenueThisMonth = await _context.Payments.AsNoTracking()
            .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success &&
                payment.PaymentDate >= monthStart && payment.PaymentDate < nextMonth)
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

        var contractBalances = await _context.Contracts.AsNoTracking()
            .Where(contract => contract.Status.StatusCode != ContractStatusCodes.Cancelled)
            .Select(contract => new
            {
                contract.TotalValue,
                Paid = contract.Payments
                    .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);
        var outstandingDebt = contractBalances.Sum(contract => Math.Max(0m, contract.TotalValue - contract.Paid));

        var openIncidentQuery = _context.Incidents.AsNoTracking()
            .Where(incident => incident.Status.StatusCode == BusinessStatusCodes.Pending ||
                incident.Status.StatusCode == BusinessStatusCodes.Processing);
        var openIncidentCount = await openIncidentQuery.CountAsync(cancellationToken);

        var upcomingEvents = await _context.WeddingBookings.AsNoTracking()
            .Where(booking => booking.HallSchedule.Date >= today &&
                booking.HallSchedule.Date < upcomingEnd &&
                activeBookingStatuses.Contains(booking.Status.StatusCode))
            .OrderBy(booking => booking.HallSchedule.Date)
            .ThenBy(booking => booking.HallSchedule.Shift)
            .Take(6)
            .Select(booking => new ManagerUpcomingEventDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                CustomerName = booking.Customer.FullName,
                HallName = booking.HallSchedule.Hall.HallName,
                EventDate = booking.HallSchedule.Date,
                Shift = booking.HallSchedule.Shift,
                GuestCount = booking.GuestCount,
                StatusCode = booking.Status.StatusCode,
                StatusName = booking.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        var recentBookings = await _context.WeddingBookings.AsNoTracking()
            .OrderByDescending(booking => booking.BookedAt)
            .Take(5)
            .Select(booking => new ManagerRecentBookingDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                CustomerName = booking.Customer.FullName,
                BookedAt = booking.BookedAt,
                EventDate = booking.HallSchedule.Date,
                EstimatedTotal = booking.EstimatedTotal ?? 0m,
                StatusCode = booking.Status.StatusCode,
                StatusName = booking.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        var openIncidents = await openIncidentQuery
            .OrderByDescending(incident => incident.OccurredAt)
            .Take(4)
            .Select(incident => new ManagerIncidentDto
            {
                IncidentId = incident.IncidentId,
                BookingCode = incident.Booking.BookingCode,
                IncidentType = incident.IncidentType,
                Severity = incident.Severity,
                StatusName = incident.Status.StatusName,
                OccurredAt = incident.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new ManagerDashboardDto
        {
            GeneratedAt = now,
            TodayEventCount = todayEventCount,
            UpcomingEventCount = upcomingEventCount,
            PendingBookingCount = pendingBookingCount,
            NewBookingCount = newBookingCount,
            RevenueThisMonth = revenueThisMonth,
            OutstandingDebt = outstandingDebt,
            OpenIncidentCount = openIncidentCount,
            UpcomingEvents = upcomingEvents,
            RecentBookings = recentBookings,
            OpenIncidents = openIncidents
        });
    }
}

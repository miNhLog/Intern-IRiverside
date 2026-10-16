using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/event-coordination")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerEventCoordinationController : ControllerBase
{
    private const string CoordinationAssigned = "ASSIGNED";
    private const string StateAssigned = "ASSIGNED";
    private const string StateUnassigned = "UNASSIGNED";
    private const string StateConflict = "CONFLICT";
    private const string StateNotApplicable = "NOT_APPLICABLE";
    private readonly ApplicationDbContext _context;

    public ManagerEventCoordinationController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<EventCoordinationListResponseDto>> GetList(
        string? search, string? bookingStatus, string? coordinationStatus,
        DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            return BadRequest(new { message = "Ngày bắt đầu không được sau ngày kết thúc." });

        var query = _context.WeddingBookings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.BookingCode.Contains(term) || b.Customer.FullName.Contains(term) ||
                b.Customer.PhoneNumber.Contains(term) || b.HallSchedule.Hall.HallName.Contains(term) ||
                b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssigned && a.Coordinator.FullName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(bookingStatus))
            query = query.Where(b => b.Status.StatusCode == bookingStatus);
        if (fromDate.HasValue) query = query.Where(b => b.HallSchedule.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(b => b.HallSchedule.Date <= toDate.Value.Date);

        var conflictBookingIds = ConflictBookingIds();
        if (string.Equals(coordinationStatus, StateAssigned, StringComparison.OrdinalIgnoreCase))
            query = query.Where(b => b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssigned) && !conflictBookingIds.Contains(b.BookingId));
        else if (string.Equals(coordinationStatus, StateUnassigned, StringComparison.OrdinalIgnoreCase))
            query = query.Where(b => (b.Status.StatusCode == BookingStatusCodes.Confirmed || b.Status.StatusCode == BookingStatusCodes.Preparing) &&
                !b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssigned));
        else if (string.Equals(coordinationStatus, StateConflict, StringComparison.OrdinalIgnoreCase))
            query = query.Where(b => conflictBookingIds.Contains(b.BookingId));

        var totalItems = await query.CountAsync(cancellationToken);
        var rawItems = await query
            .OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.HallSchedule.Shift).ThenBy(b => b.BookingCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.BookingId, b.BookingCode, EventDate = b.HallSchedule.Date, b.HallSchedule.Shift,
                b.HallSchedule.HallId, b.HallSchedule.Hall.HallName, CustomerName = b.Customer.FullName,
                BookingStatusCode = b.Status.StatusCode, BookingStatusName = b.Status.StatusName,
                HallManagerName = _context.HallManagerAssignments
                    .Where(a => a.HallId == b.HallSchedule.HallId && a.FromDate <= b.HallSchedule.Date &&
                        (!a.ToDate.HasValue || a.ToDate.Value >= b.HallSchedule.Date))
                    .OrderByDescending(a => a.FromDate).Select(a => a.HallManager.FullName).FirstOrDefault(),
                Coordinator = b.CoordinationAssignments.Where(a => a.Status.StatusCode == CoordinationAssigned)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new { Id = (int?)a.CoordinatorEmployeeId, Name = a.Coordinator.FullName }).FirstOrDefault(),
                HasConflict = conflictBookingIds.Contains(b.BookingId)
            }).ToListAsync(cancellationToken);

        var items = rawItems.Select(x => new EventCoordinationListItemDto
        {
            BookingId = x.BookingId, BookingCode = x.BookingCode, EventDate = x.EventDate, Shift = x.Shift,
            HallId = x.HallId, HallName = x.HallName, CustomerName = x.CustomerName,
            BookingStatusCode = x.BookingStatusCode, BookingStatusName = x.BookingStatusName,
            HallManagerName = x.HallManagerName, CoordinatorEmployeeId = x.Coordinator?.Id,
            CoordinatorName = x.Coordinator?.Name, HasConflict = x.HasConflict,
            CoordinationState = ResolveState(x.BookingStatusCode, x.Coordinator is not null, x.HasConflict)
        }).ToList();

        var today = DateTime.Today;
        var nextWeek = today.AddDays(7);
        var kpi = new EventCoordinationKpiDto
        {
            UpcomingCount = await _context.WeddingBookings.AsNoTracking().CountAsync(b =>
                b.HallSchedule.Date >= today && b.HallSchedule.Date <= nextWeek &&
                b.Status.StatusCode != BookingStatusCodes.Cancelled && b.Status.StatusCode != BookingStatusCodes.Completed, cancellationToken),
            UnassignedCount = await _context.WeddingBookings.AsNoTracking().CountAsync(b =>
                (b.Status.StatusCode == BookingStatusCodes.Confirmed || b.Status.StatusCode == BookingStatusCodes.Preparing) &&
                !b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssigned), cancellationToken),
            PreparingCount = await _context.WeddingBookings.AsNoTracking().CountAsync(b => b.Status.StatusCode == BookingStatusCodes.Preparing, cancellationToken),
            IncidentAttentionCount = await _context.WeddingBookings.AsNoTracking().CountAsync(b =>
                b.Incidents.Any(i => i.Status.StatusCode == BusinessStatusCodes.Pending || i.Status.StatusCode == BusinessStatusCodes.Processing), cancellationToken)
        };

        return Ok(new EventCoordinationListResponseDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize), Kpi = kpi,
            BookingStatuses = await _context.Statuses.AsNoTracking().Where(s => s.StatusGroup == StatusGroups.Booking)
                .OrderBy(s => s.StatusId).Select(s => new EventCoordinationOptionDto { Value = s.StatusCode, Label = s.StatusName }).ToListAsync(cancellationToken),
            Halls = await _context.Halls.AsNoTracking().Where(h => h.DataStatus.DataStatusCode == DataStatusCodes.Existing).OrderBy(h => h.HallName)
                .Select(h => new EventCoordinationOptionDto { Value = h.HallId.ToString(), Label = h.HallName }).ToListAsync(cancellationToken),
            Coordinators = await _context.Employees.AsNoTracking()
                .Where(e => e.User.Role.RoleName == RoleNames.Coordinator && e.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                .OrderBy(e => e.FullName).Select(e => new EventCoordinationOptionDto { Value = e.EmployeeId.ToString(), Label = e.FullName }).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("{bookingId:int}")]
    public async Task<ActionResult<EventCoordinationDetailDto>> GetDetail(int bookingId, CancellationToken cancellationToken)
    {
        var conflictIds = ConflictBookingIds();
        var data = await _context.WeddingBookings.AsNoTracking().Where(b => b.BookingId == bookingId)
            .Select(b => new
            {
                b.BookingId, b.BookingCode, EventDate = b.HallSchedule.Date, b.HallSchedule.Shift,
                HallName = b.HallSchedule.Hall.HallName, BookingStatusCode = b.Status.StatusCode,
                BookingStatusName = b.Status.StatusName, CustomerName = b.Customer.FullName,
                CustomerPhone = b.Customer.PhoneNumber, CustomerEmail = b.Customer.Email, b.GuestCount,
                HallManagerName = _context.HallManagerAssignments.Where(a => a.HallId == b.HallSchedule.HallId &&
                    a.FromDate <= b.HallSchedule.Date && (!a.ToDate.HasValue || a.ToDate.Value >= b.HallSchedule.Date))
                    .OrderByDescending(a => a.FromDate).Select(a => a.HallManager.FullName).FirstOrDefault(),
                ActiveAssignment = b.CoordinationAssignments.Where(a => a.Status.StatusCode == CoordinationAssigned)
                    .OrderByDescending(a => a.AssignedAt).Select(a => new { a.Coordinator.FullName, a.AssignedAt }).FirstOrDefault(),
                HasConflict = conflictIds.Contains(b.BookingId),
                OpenIncidentCount = b.Incidents.Count(i => i.Status.StatusCode == BusinessStatusCodes.Pending || i.Status.StatusCode == BusinessStatusCodes.Processing),
                ScheduleChangeCount = b.ScheduleChanges.Count(), MenuName = b.Menu != null ? b.Menu.MenuName : null,
                b.FinalMenuPrice, DecorationName = b.DecorationPackage != null ? b.DecorationPackage.TenGoi : null,
                b.FinalDecorationPrice, b.FinalHallPrice, b.EstimatedTotal, b.SpecialRequests
            }).SingleOrDefaultAsync(cancellationToken);
        if (data is null) return NotFound(new { message = "Không tìm thấy tiệc." });

        var result = new EventCoordinationDetailDto
        {
            BookingId = data.BookingId, BookingCode = data.BookingCode, EventDate = data.EventDate, Shift = data.Shift,
            HallName = data.HallName, BookingStatusCode = data.BookingStatusCode, BookingStatusName = data.BookingStatusName,
            CustomerName = data.CustomerName, CustomerPhone = data.CustomerPhone, CustomerEmail = data.CustomerEmail,
            GuestCount = data.GuestCount, HallManagerName = data.HallManagerName,
            CoordinatorName = data.ActiveAssignment?.FullName, AssignedAt = data.ActiveAssignment?.AssignedAt,
            HasConflict = data.HasConflict, CoordinationState = ResolveState(data.BookingStatusCode, data.ActiveAssignment is not null, data.HasConflict),
            OpenIncidentCount = data.OpenIncidentCount, ScheduleChangeCount = data.ScheduleChangeCount,
            MenuName = data.MenuName, FinalMenuPrice = data.FinalMenuPrice, DecorationName = data.DecorationName,
            FinalDecorationPrice = data.FinalDecorationPrice, FinalHallPrice = data.FinalHallPrice,
            EstimatedTotal = data.EstimatedTotal, SpecialRequests = data.SpecialRequests
        };

        result.Services = await _context.WeddingBookingServices.AsNoTracking().Where(s => s.BookingId == bookingId)
            .OrderBy(s => s.ServiceItem.TenDichVu).Select(s => new EventCoordinationServiceDto
            { ServiceName = s.ServiceItem.TenDichVu, Quantity = s.Quantity, FinalUnitPrice = s.FinalUnitPrice }).ToListAsync(cancellationToken);
        result.Assignments = await _context.CoordinationAssignments.AsNoTracking().Where(a => a.BookingId == bookingId)
            .OrderByDescending(a => a.AssignedAt).Select(a => new EventCoordinationAssignmentDto
            { CoordinatorName = a.Coordinator.FullName, CoordinatorCode = a.Coordinator.EmployeeCode,
                AssignedByName = a.AssignedByHallManager.FullName, StatusCode = a.Status.StatusCode,
                StatusName = a.Status.StatusName, AssignedAt = a.AssignedAt }).ToListAsync(cancellationToken);
        result.ScheduleChanges = await _context.WeddingScheduleChanges.AsNoTracking().Where(c => c.BookingId == bookingId)
            .OrderByDescending(c => c.RequestedAt).Select(c => new EventScheduleChangeDto
            { OldHallName = c.OldHallSchedule.Hall.HallName, OldDate = c.OldHallSchedule.Date, OldShift = c.OldHallSchedule.Shift,
                NewHallName = c.NewHallSchedule.Hall.HallName, NewDate = c.NewHallSchedule.Date, NewShift = c.NewHallSchedule.Shift,
                Reason = c.Reason, StatusName = c.Status.StatusName, RequestedAt = c.RequestedAt, ProcessedAt = c.ProcessedAt }).ToListAsync(cancellationToken);
        result.Incidents = await _context.Incidents.AsNoTracking().Where(i => i.BookingId == bookingId)
            .OrderByDescending(i => i.OccurredAt).Select(i => new EventIncidentDto
            { IncidentType = i.IncidentType, Description = i.Description, Severity = i.Severity,
                StatusCode = i.Status.StatusCode, StatusName = i.Status.StatusName,
                ReportedByName = i.ReportedByEmployee != null ? i.ReportedByEmployee.FullName : null,
                Resolution = i.Resolution, OccurredAt = i.OccurredAt, ResolvedAt = i.ResolvedAt }).ToListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<EventCoordinationCalendarDto>> GetCalendar(
        DateTime fromDate, DateTime toDate, int? hallId, int? coordinatorEmployeeId, bool conflictsOnly = false,
        CancellationToken cancellationToken = default)
    {
        fromDate = fromDate.Date; toDate = toDate.Date;
        if (toDate < fromDate || (toDate - fromDate).TotalDays > 14)
            return BadRequest(new { message = "Khoảng lịch phải từ 1 đến 14 ngày." });
        var conflicts = ConflictBookingIds();
        var query = _context.WeddingBookings.AsNoTracking().Where(b => b.HallSchedule.Date >= fromDate && b.HallSchedule.Date <= toDate);
        if (hallId.HasValue) query = query.Where(b => b.HallSchedule.HallId == hallId.Value);
        if (coordinatorEmployeeId.HasValue) query = query.Where(b => b.CoordinationAssignments.Any(a => a.Status.StatusCode == CoordinationAssigned && a.CoordinatorEmployeeId == coordinatorEmployeeId.Value));
        if (conflictsOnly) query = query.Where(b => conflicts.Contains(b.BookingId));

        var raw = await query.OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.HallSchedule.Shift)
            .Select(b => new
            {
                b.BookingId, b.BookingCode, EventDate = b.HallSchedule.Date, b.HallSchedule.Shift,
                b.HallSchedule.HallId, b.HallSchedule.Hall.HallName, CustomerName = b.Customer.FullName,
                BookingStatusCode = b.Status.StatusCode, BookingStatusName = b.Status.StatusName,
                Coordinator = b.CoordinationAssignments.Where(a => a.Status.StatusCode == CoordinationAssigned)
                    .OrderByDescending(a => a.AssignedAt).Select(a => new { Id = (int?)a.CoordinatorEmployeeId, Name = a.Coordinator.FullName }).FirstOrDefault(),
                HasConflict = conflicts.Contains(b.BookingId)
            }).ToListAsync(cancellationToken);
        return Ok(new EventCoordinationCalendarDto
        {
            FromDate = fromDate, ToDate = toDate,
            Items = raw.Select(x => new EventCoordinationCalendarItemDto
            {
                BookingId = x.BookingId, BookingCode = x.BookingCode, EventDate = x.EventDate, Shift = x.Shift,
                HallId = x.HallId, HallName = x.HallName, CustomerName = x.CustomerName,
                BookingStatusCode = x.BookingStatusCode, BookingStatusName = x.BookingStatusName,
                CoordinatorEmployeeId = x.Coordinator?.Id, CoordinatorName = x.Coordinator?.Name,
                HasConflict = x.HasConflict, CoordinationState = ResolveState(x.BookingStatusCode, x.Coordinator is not null, x.HasConflict)
            }).ToList()
        });
    }

    private IQueryable<int> ConflictBookingIds()
    {
        var active = _context.CoordinationAssignments.AsNoTracking().Where(a =>
            a.Status.StatusCode == CoordinationAssigned &&
            a.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
            a.Booking.Status.StatusCode != BookingStatusCodes.Completed);
        return active.Where(a => active.Any(other => other.CoordinationAssignmentId != a.CoordinationAssignmentId &&
            other.CoordinatorEmployeeId == a.CoordinatorEmployeeId &&
            other.Booking.HallSchedule.Date == a.Booking.HallSchedule.Date &&
            other.Booking.HallSchedule.Shift == a.Booking.HallSchedule.Shift)).Select(a => a.BookingId).Distinct();
    }

    private static string ResolveState(string bookingStatus, bool assigned, bool conflict)
    {
        if (conflict) return StateConflict;
        if (assigned) return StateAssigned;
        return bookingStatus is BookingStatusCodes.Cancelled or BookingStatusCodes.Completed ? StateNotApplicable : StateUnassigned;
    }
}

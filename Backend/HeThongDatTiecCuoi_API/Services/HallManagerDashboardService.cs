using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Dashboard;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class HallManagerDashboardService : IHallManagerDashboardService
{
    private const string Unassigned = "UNASSIGNED";
    private const string Conflict = "CONFLICT";
    private const string Assigned = "ASSIGNED";
    private const string NoSchedule = "NO_SCHEDULE";
    private const string Review = "REVIEW";
    private readonly ApplicationDbContext _context;

    public HallManagerDashboardService(ApplicationDbContext context) => _context = context;

    public async Task<ServiceResult<HallManagerDashboardDto>> GetAsync(int userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveHallManagerAsync(userId, cancellationToken))
            return ServiceResult<HallManagerDashboardDto>.Failure(
                "Không tìm thấy hồ sơ Quản lý sảnh hợp lệ.", StatusCodes.Status403Forbidden);

        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var endExclusive = today.AddDays(8);
        var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, today);
        var conflictIds = HallManagerQueryScope.ConflictBookingIds(_context);
        var countedStatuses = new[]
        {
            BookingStatusCodes.Confirmed, BookingStatusCodes.Preparing, BookingStatusCodes.Completed
        };

        var managedHalls = await hallIds.CountAsync(cancellationToken);
        var scopedBookings = _context.WeddingBookings.AsNoTracking()
            .Where(booking => hallIds.Contains(booking.HallSchedule.HallId));
        var eligibleCoordination = scopedBookings.Where(booking => booking.HallSchedule.Date >= today &&
            (booking.Status.StatusCode == BookingStatusCodes.Confirmed ||
             booking.Status.StatusCode == BookingStatusCodes.Preparing));

        var kpi = new HallManagerDashboardKpiDto
        {
            ManagedHalls = managedHalls,
            TodayBookings = await scopedBookings.CountAsync(booking =>
                booking.HallSchedule.Date == today && countedStatuses.Contains(booking.Status.StatusCode), cancellationToken),
            NextSevenDaysBookings = await scopedBookings.CountAsync(booking =>
                booking.HallSchedule.Date >= tomorrow && booking.HallSchedule.Date < endExclusive &&
                countedStatuses.Contains(booking.Status.StatusCode), cancellationToken),
            UnassignedBookings = await eligibleCoordination.CountAsync(booking =>
                !booking.CoordinationAssignments.Any(assignment =>
                    assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned), cancellationToken),
            CoordinationConflicts = await eligibleCoordination.CountAsync(booking =>
                conflictIds.Contains(booking.BookingId), cancellationToken),
            OpenIncidents = await _context.Incidents.AsNoTracking().CountAsync(incident =>
                hallIds.Contains(incident.Booking.HallSchedule.HallId) &&
                (incident.Status.StatusCode == BusinessStatusCodes.Pending ||
                 incident.Status.StatusCode == BusinessStatusCodes.Processing), cancellationToken)
        };

        var upcoming = await scopedBookings
            .Where(booking => booking.HallSchedule.Date >= today && booking.HallSchedule.Date < endExclusive &&
                (booking.Status.StatusCode == BookingStatusCodes.Confirmed ||
                 booking.Status.StatusCode == BookingStatusCodes.Preparing))
            .OrderBy(booking => booking.HallSchedule.Date)
            .ThenBy(booking => booking.HallSchedule.Shift == "Ca trưa" ? 0 : 1)
            .ThenBy(booking => booking.BookingCode)
            .Take(7)
            .Select(booking => new HallManagerUpcomingBookingDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                CustomerName = booking.Customer.FullName,
                HallName = booking.HallSchedule.Hall.HallName,
                EventDate = booking.HallSchedule.Date,
                Shift = booking.HallSchedule.Shift,
                TableCount = (int)Math.Ceiling(booking.GuestCount / 10m),
                BookingStatusCode = booking.Status.StatusCode,
                BookingStatusName = booking.Status.StatusName,
                CoordinatorName = booking.CoordinationAssignments
                    .Where(assignment => assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)
                    .OrderByDescending(assignment => assignment.AssignedAt)
                    .Select(assignment => assignment.Coordinator.FullName).FirstOrDefault(),
                HasConflict = conflictIds.Contains(booking.BookingId),
                CoordinationState = conflictIds.Contains(booking.BookingId) ? Conflict :
                    booking.CoordinationAssignments.Any(assignment =>
                        assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned) ? Assigned : Unassigned
            }).ToListAsync(cancellationToken);

        var tasks = await GetTasksAsync(hallIds, eligibleCoordination, conflictIds, cancellationToken);
        var scheduleResult = await BuildScheduleAsync(hallIds, today, cancellationToken);

        return ServiceResult<HallManagerDashboardDto>.Success(new HallManagerDashboardDto
        {
            GeneratedAt = now,
            Kpi = kpi,
            Schedule = scheduleResult,
            UpcomingBookings = upcoming,
            Tasks = tasks
        });
    }

    public async Task<ServiceResult<HallManagerScheduleDto>> GetScheduleAsync(int userId, DateTime date,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveHallManagerAsync(userId, cancellationToken))
            return ServiceResult<HallManagerScheduleDto>.Failure(
                "Không tìm thấy hồ sơ Quản lý sảnh hợp lệ.", StatusCodes.Status403Forbidden);

        var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, DateTime.Today);
        return ServiceResult<HallManagerScheduleDto>.Success(
            await BuildScheduleAsync(hallIds, date.Date, cancellationToken));
    }

    private async Task<HallManagerScheduleDto> BuildScheduleAsync(IQueryable<int> hallIds, DateTime date,
        CancellationToken cancellationToken)
    {
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hallIds.Contains(hall.HallId) &&
                hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(hall => hall.HallName)
            .Select(hall => new { hall.HallId, hall.HallName })
            .ToListAsync(cancellationToken);
        var ids = halls.Select(hall => hall.HallId).ToList();
        var schedules = await _context.HallSchedules.AsNoTracking()
            .Where(schedule => ids.Contains(schedule.HallId) && schedule.Date == date &&
                schedule.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(schedule => new
            {
                schedule.HallScheduleId, schedule.HallId, schedule.Shift,
                ScheduleStatus = schedule.Status.StatusCode
            }).ToListAsync(cancellationToken);
        var scheduleIds = schedules.Select(schedule => schedule.HallScheduleId).ToList();
        var bookings = await _context.WeddingBookings.AsNoTracking()
            .Where(booking => scheduleIds.Contains(booking.HallScheduleId) &&
                booking.Status.StatusCode != BookingStatusCodes.Cancelled)
            .Select(booking => new ScheduleBooking(
                booking.HallScheduleId, booking.BookingCode, booking.Customer.FullName,
                booking.Status.StatusCode, booking.Status.StatusName))
            .ToListAsync(cancellationToken);

        return new HallManagerScheduleDto
        {
            Date = date,
            Halls = halls.Select(hall => new HallManagerScheduleRowDto
            {
                HallId = hall.HallId,
                HallName = hall.HallName,
                Lunch = BuildSlot("Ca trưa", schedules.Where(item => item.HallId == hall.HallId && IsLunch(item.Shift))
                    .Select(item => (item.HallScheduleId, item.ScheduleStatus)).ToList(), bookings),
                Dinner = BuildSlot("Ca tối", schedules.Where(item => item.HallId == hall.HallId && IsDinner(item.Shift))
                    .Select(item => (item.HallScheduleId, item.ScheduleStatus)).ToList(), bookings)
            }).ToList()
        };
    }

    private static HallManagerScheduleSlotDto BuildSlot(string shift,
        List<(int HallScheduleId, string ScheduleStatus)> schedules,
        IEnumerable<ScheduleBooking> bookings)
    {
        if (schedules.Count == 0)
            return new HallManagerScheduleSlotDto { Shift = shift, StateCode = NoSchedule, StateName = "Chưa có lịch" };
        if (schedules.Count > 1)
            return new HallManagerScheduleSlotDto { Shift = shift, StateCode = Review, StateName = "Cần kiểm tra dữ liệu", RequiresReview = true };

        var schedule = schedules[0];
        var activeBookings = bookings.Where(booking => booking.HallScheduleId == schedule.HallScheduleId).ToList();
        if (activeBookings.Count > 1)
            return new HallManagerScheduleSlotDto { Shift = shift, StateCode = Review, StateName = "Cần kiểm tra dữ liệu", RequiresReview = true };
        if (activeBookings.Count == 1)
        {
            var booking = activeBookings[0];
            var inconsistent = schedule.ScheduleStatus != HallScheduleStatusCodes.Booked;
            return new HallManagerScheduleSlotDto
            {
                Shift = shift, StateCode = booking.BookingStatus, StateName = booking.BookingStatusName,
                BookingCode = booking.BookingCode, CustomerName = booking.CustomerName, RequiresReview = inconsistent
            };
        }

        return schedule.ScheduleStatus switch
        {
            HallScheduleStatusCodes.Available => new HallManagerScheduleSlotDto
                { Shift = shift, StateCode = HallScheduleStatusCodes.Available, StateName = "Còn trống" },
            HallScheduleStatusCodes.Locked => new HallManagerScheduleSlotDto
                { Shift = shift, StateCode = HallScheduleStatusCodes.Locked, StateName = "Đã khóa" },
            _ => new HallManagerScheduleSlotDto
                { Shift = shift, StateCode = Review, StateName = "Cần kiểm tra dữ liệu", RequiresReview = true }
        };
    }

    private async Task<List<HallManagerTaskDto>> GetTasksAsync(IQueryable<int> hallIds,
        IQueryable<WeddingBooking> eligibleCoordination, IQueryable<int> conflictIds,
        CancellationToken cancellationToken)
    {
        var incidents = await _context.Incidents.AsNoTracking().Where(incident =>
                hallIds.Contains(incident.Booking.HallSchedule.HallId) &&
                (incident.Status.StatusCode == BusinessStatusCodes.Pending ||
                 incident.Status.StatusCode == BusinessStatusCodes.Processing))
            .Select(incident => new HallManagerTaskDto
            {
                Type = "INCIDENT", Priority = incident.Severity == IncidentSeverityCodes.Critical ? "CRITICAL" : "NORMAL",
                ReferenceCode = "SC-" + incident.IncidentId, Title = incident.IncidentType,
                HallName = incident.Booking.HallSchedule.Hall.HallName, RelevantAt = incident.OccurredAt,
                BookingId = incident.BookingId, IncidentId = incident.IncidentId
            }).ToListAsync(cancellationToken);
        var conflicts = await eligibleCoordination.Where(booking => conflictIds.Contains(booking.BookingId))
            .Select(booking => new HallManagerTaskDto
            {
                Type = Conflict, Priority = "HIGH", ReferenceCode = booking.BookingCode,
                Title = "Xung đột phân công điều phối", HallName = booking.HallSchedule.Hall.HallName,
                RelevantAt = booking.HallSchedule.Date, BookingId = booking.BookingId
            }).ToListAsync(cancellationToken);
        var unassigned = await eligibleCoordination.Where(booking => !booking.CoordinationAssignments.Any(assignment =>
                assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned))
            .Select(booking => new HallManagerTaskDto
            {
                Type = Unassigned, Priority = "MEDIUM", ReferenceCode = booking.BookingCode,
                Title = "Tiệc chưa có điều phối viên", HallName = booking.HallSchedule.Hall.HallName,
                RelevantAt = booking.HallSchedule.Date, BookingId = booking.BookingId
            }).ToListAsync(cancellationToken);

        return incidents.Where(item => item.Priority == "CRITICAL").OrderByDescending(item => item.RelevantAt)
            .Concat(conflicts.OrderBy(item => item.RelevantAt))
            .Concat(unassigned.OrderBy(item => item.RelevantAt))
            .Concat(incidents.Where(item => item.Priority != "CRITICAL").OrderByDescending(item => item.RelevantAt))
            .GroupBy(item => new { item.Type, item.ReferenceCode }).Select(group => group.First()).Take(5).ToList();
    }

    private Task<bool> IsActiveHallManagerAsync(int userId, CancellationToken cancellationToken) =>
        _context.Employees.AsNoTracking().AnyAsync(employee => employee.UserId == userId &&
            employee.User.Role.RoleName == RoleNames.HallManager &&
            employee.User.Status.StatusCode == AccountStatusCodes.Active &&
            employee.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            employee.DataStatus.DataStatusCode == DataStatusCodes.Existing, cancellationToken);

    private static bool IsLunch(string shift) => shift.Contains("trưa", StringComparison.OrdinalIgnoreCase);
    private static bool IsDinner(string shift) => shift.Contains("tối", StringComparison.OrdinalIgnoreCase);

    private sealed record ScheduleBooking(int HallScheduleId, string BookingCode, string CustomerName,
        string BookingStatus, string BookingStatusName);
}

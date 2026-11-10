using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

internal static class HallManagerQueryScope
{
    public static IQueryable<int> CurrentHallIds(ApplicationDbContext context, int userId, DateTime today) =>
        context.HallManagerAssignments.Where(assignment =>
            assignment.HallManager.UserId == userId &&
            assignment.HallManager.User.Role.RoleName == RoleNames.HallManager &&
            assignment.HallManager.User.Status.StatusCode == AccountStatusCodes.Active &&
            assignment.HallManager.User.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            assignment.HallManager.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            assignment.Hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            assignment.FromDate <= today &&
            (!assignment.ToDate.HasValue || assignment.ToDate.Value >= today))
        .Select(assignment => assignment.HallId)
        .Distinct();

    public static IQueryable<int> ConflictBookingIds(ApplicationDbContext context)
    {
        var active = context.CoordinationAssignments.AsNoTracking().Where(assignment =>
            assignment.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
            assignment.Booking.Status.StatusCode != BookingStatusCodes.Cancelled &&
            assignment.Booking.Status.StatusCode != BookingStatusCodes.Completed);

        return active.Where(assignment => active.Any(other =>
                other.CoordinationAssignmentId != assignment.CoordinationAssignmentId &&
                other.CoordinatorEmployeeId == assignment.CoordinatorEmployeeId &&
                other.Booking.HallSchedule.Date == assignment.Booking.HallSchedule.Date &&
                other.Booking.HallSchedule.Shift == assignment.Booking.HallSchedule.Shift))
            .Select(assignment => assignment.BookingId)
            .Distinct();
    }
}

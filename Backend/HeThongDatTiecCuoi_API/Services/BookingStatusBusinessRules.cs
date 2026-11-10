using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public enum BookingStatusRuleFailureKind
{
    None,
    InvalidTransition,
    BusinessCondition
}

public sealed record BookingStatusRuleResult(
    bool Allowed,
    bool Unchanged,
    BookingStatusRuleFailureKind FailureKind,
    string? Code = null,
    string? Message = null)
{
    public static BookingStatusRuleResult Success() =>
        new(true, false, BookingStatusRuleFailureKind.None);

    public static BookingStatusRuleResult SameState() =>
        new(true, true, BookingStatusRuleFailureKind.None);

    public static BookingStatusRuleResult Invalid(string from, string to) =>
        new(false, false, BookingStatusRuleFailureKind.InvalidTransition,
            "ERR_BOOKING_STATUS_TRANSITION_INVALID",
            $"Không thể chuyển trạng thái booking từ {from} sang {to}.");

    public static BookingStatusRuleResult Condition(string code, string message) =>
        new(false, false, BookingStatusRuleFailureKind.BusinessCondition, code, message);
}

/// <summary>
/// Central booking state machine and the database-backed business conditions for
/// each allowed transition. This service only validates; callers remain responsible
/// for applying all accepted changes in their transaction.
/// </summary>
public sealed class BookingStatusBusinessRules(
    ApplicationDbContext context,
    TimeProvider? timeProvider = null)
{
    private static readonly IReadOnlySet<(string From, string To)> AllowedTransitions =
        new HashSet<(string From, string To)>
        {
            (BookingStatusCodes.Pending, BookingStatusCodes.Confirmed),
            (BookingStatusCodes.Pending, BookingStatusCodes.Cancelled),
            (BookingStatusCodes.Confirmed, BookingStatusCodes.Preparing),
            (BookingStatusCodes.Confirmed, BookingStatusCodes.Cancelled),
            (BookingStatusCodes.Preparing, BookingStatusCodes.Completed),
            (BookingStatusCodes.Preparing, BookingStatusCodes.Cancelled)
        };

    private DateTime Now => (timeProvider ?? TimeProvider.System)
        .GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime;

    public static BookingStatusRuleResult ValidateTransition(string from, string to)
    {
        if (from == to)
            return BookingStatusRuleResult.SameState();

        return AllowedTransitions.Contains((from, to))
            ? BookingStatusRuleResult.Success()
            : BookingStatusRuleResult.Invalid(from, to);
    }

    public async Task<BookingStatusRuleResult> ValidateAsync(
        WeddingBooking booking,
        string targetStatusCode,
        CancellationToken cancellationToken = default)
    {
        var transition = ValidateTransition(booking.Status.StatusCode, targetStatusCode);
        if (!transition.Allowed || transition.Unchanged)
            return transition;

        if (targetStatusCode == BookingStatusCodes.Cancelled)
            return await ValidateCancellationAsync(booking, cancellationToken);

        if (booking.Status.StatusCode == BookingStatusCodes.Pending &&
            targetStatusCode == BookingStatusCodes.Confirmed)
            return await ValidateScheduleAsync(booking, cancellationToken);

        if (booking.Status.StatusCode == BookingStatusCodes.Confirmed &&
            targetStatusCode == BookingStatusCodes.Preparing)
        {
            var schedule = await ValidateScheduleAsync(booking, cancellationToken);
            if (!schedule.Allowed) return schedule;

            if (booking.Contract?.Status.StatusCode == ContractStatusCodes.Cancelled)
                return BookingStatusRuleResult.Condition(
                    "ERR_BOOKING_CONTRACT_CANCELLED",
                    "Hợp đồng của booking đã bị hủy. Không thể tiếp tục chuyển booking sang chuẩn bị.");

            return await ValidateNoOpenScheduleChangeAsync(booking.BookingId, cancellationToken);
        }

        if (booking.Status.StatusCode == BookingStatusCodes.Preparing &&
            targetStatusCode == BookingStatusCodes.Completed)
            return await ValidateCompletionAsync(booking, cancellationToken);

        return transition;
    }

    private async Task<BookingStatusRuleResult> ValidateScheduleAsync(
        WeddingBooking booking,
        CancellationToken cancellationToken)
    {
        var schedule = booking.HallSchedule;
        if (schedule is null || schedule.Status is null ||
            schedule.Status.StatusGroup != StatusGroups.HallSchedule)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_INVALID",
                "Lịch sảnh của booking không hợp lệ.");

        if (schedule.DataStatus?.DataStatusCode != DataStatusCodes.Existing ||
            schedule.Hall is null ||
            schedule.Hall.DataStatus?.DataStatusCode != DataStatusCodes.Existing ||
            schedule.Hall.Status?.StatusGroup != StatusGroups.Hall ||
            schedule.Hall.Status.StatusCode != BusinessStatusCodes.Active)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_INVALID",
                "Lịch sảnh hoặc sảnh tiệc không còn hiệu lực.");

        if (schedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_LOCKED",
                "Lịch sảnh đang bị khóa bởi quản trị viên.");

        if (schedule.Status.StatusCode is not (HallScheduleStatusCodes.Available or HallScheduleStatusCodes.Booked))
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_INVALID",
                "Lịch sảnh không ở trạng thái cho phép tiếp tục xử lý booking.");

        var occupiedByAnotherBooking = await context.WeddingBookings.AsNoTracking().AnyAsync(item =>
            item.BookingId != booking.BookingId &&
            item.HallScheduleId == booking.HallScheduleId &&
            item.Status.StatusGroup == StatusGroups.Booking &&
            BookingStatusCodes.OccupyingSchedule.Contains(item.Status.StatusCode),
            cancellationToken);
        if (occupiedByAnotherBooking)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_CONFLICT",
                "Lịch sảnh đang được một booking khác sử dụng.");

        var heldByAnotherChange = await context.ScheduleChangeHolds.AsNoTracking().AnyAsync(hold =>
            hold.HallScheduleId == booking.HallScheduleId &&
            hold.ScheduleChange.BookingId != booking.BookingId &&
            hold.Status == ScheduleChangeHoldStatusCodes.Active &&
            hold.ExpiresAt > Now,
            cancellationToken);
        return heldByAnotherChange
            ? BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_HELD",
                "Lịch sảnh đang được giữ cho một yêu cầu đổi lịch khác.")
            : BookingStatusRuleResult.Success();
    }

    private async Task<BookingStatusRuleResult> ValidateCompletionAsync(
        WeddingBooking booking,
        CancellationToken cancellationToken)
    {
        var window = ChecklistTimingRules.Window(booking.HallSchedule);
        if (!window.HasValue)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_EVENT_WINDOW_INVALID",
                "Không xác định được giờ kết thúc ca của booking.");
        if (Now < window.Value.End)
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_EVENT_NOT_ENDED",
                $"Chỉ có thể hoàn tất booking sau {window.Value.End:HH:mm dd/MM/yyyy} (giờ Việt Nam).");

        var scheduleChange = await ValidateNoOpenScheduleChangeAsync(booking.BookingId, cancellationToken);
        if (!scheduleChange.Allowed) return scheduleChange;

        var checklist = await context.PartyChecklists.AsNoTracking()
            .Include(item => item.Tasks)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.BookingId == booking.BookingId, cancellationToken);
        if (checklist is null)
            return BookingStatusRuleResult.Success();

        if (checklist.State != "approved" ||
            checklist.Tasks.Any(task => task.Required && !ChecklistTimingRules.ValidCompletion(task, booking.HallSchedule, Now)) ||
            ChecklistTimingRules.NeedsRepair(checklist, booking.HallSchedule, Now))
            return BookingStatusRuleResult.Condition(
                "ERR_BOOKING_CHECKLIST_INCOMPLETE",
                "Checklist chưa được duyệt bàn giao hoặc còn công việc bắt buộc chưa hoàn thành hợp lệ.");

        return BookingStatusRuleResult.Success();
    }

    private async Task<BookingStatusRuleResult> ValidateCancellationAsync(
        WeddingBooking booking,
        CancellationToken cancellationToken)
    {
        if (await BookingPaymentConcurrency.HasRecordedDepositAsync(
                context, booking.BookingId, booking.PaidAmount, cancellationToken))
            return BookingStatusRuleResult.Condition(
                "ERR_PAID_BOOKING_REQUIRES_MANAGED_CANCELLATION",
                "Booking đã ghi nhận thanh toán. Hãy sử dụng quy trình hủy booking đã thanh toán và xử lý hoàn tiền/đối soát.");

        if (booking.Contract?.Status.StatusCode is ContractStatusCodes.Active or ContractStatusCodes.Completed)
            return BookingStatusRuleResult.Condition(
                "ERR_ACTIVE_CONTRACT_REQUIRES_MANAGED_CANCELLATION",
                "Booking có hợp đồng đang hiệu lực hoặc đã hoàn tất. Hãy xử lý hợp đồng và nghĩa vụ tài chính trước khi hủy booking.");

        return await ValidateNoOpenScheduleChangeAsync(booking.BookingId, cancellationToken);
    }

    private async Task<BookingStatusRuleResult> ValidateNoOpenScheduleChangeAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        var hasOpenChange = await context.WeddingScheduleChanges.AsNoTracking().AnyAsync(item =>
            item.BookingId == bookingId &&
            item.Status.StatusGroup == StatusGroups.ScheduleChange &&
            ScheduleChangeStatusCodes.Open.Contains(item.Status.StatusCode),
            cancellationToken);
        return hasOpenChange
            ? BookingStatusRuleResult.Condition(
                "ERR_BOOKING_SCHEDULE_CHANGE_OPEN",
                "Booking đang có yêu cầu đổi lịch chưa được giải quyết.")
            : BookingStatusRuleResult.Success();
    }
}

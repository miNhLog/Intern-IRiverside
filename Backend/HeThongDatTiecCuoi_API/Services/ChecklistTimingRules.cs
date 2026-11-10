using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public static class ChecklistTimingRules
{
    public static (DateTime Start, DateTime End)? Window(HallSchedule schedule)
    {
        var shift = schedule.Shift.Trim().ToLowerInvariant();
        return shift switch
        {
            "ca trưa" or "trưa" => (schedule.Date.Date.AddHours(10.5), schedule.Date.Date.AddHours(14)),
            "ca tối" or "tối" => (schedule.Date.Date.AddHours(17.5), schedule.Date.Date.AddHours(21.5)),
            _ => null
        };
    }

    // Confirmation work may be completed as soon as the coordinator is assigned.
    // Physical preparation and custom pre-event work require a check from the previous day.
    public static DateTime? OpensAt(PartyChecklistTask task, HallSchedule schedule) => task.Group switch
    {
        "Trước tiệc" when task.Name is "Xác nhận thực đơn và số bàn với bếp" or "Đối chiếu yêu cầu đặc biệt của khách" => DateTime.MinValue,
        "Trước tiệc" => schedule.Date.Date.AddDays(-1),
        "Trong tiệc" => Window(schedule)?.Start,
        "Sau tiệc" => Window(schedule)?.End,
        _ => null
    };

    public static bool ValidCompletion(PartyChecklistTask task, HallSchedule schedule, DateTime now) =>
        task.Status == "done" && task.CompletedAt is { } completed && OpensAt(task, schedule) is { } opens &&
        completed >= opens && completed <= now;

    public static bool NeedsRepair(PartyChecklist checklist, HallSchedule schedule, DateTime now) =>
        checklist.Tasks.Any(t => t.Status == "done" && !ValidCompletion(t, schedule, now)) ||
        (checklist.State is "review" or "approved" &&
            (Window(schedule) is not { } window || now < window.End ||
                checklist.History.Where(h => h.Action == (checklist.State == "approved" ? "APPROVE" : "SUBMIT"))
                    .OrderByDescending(h => h.Id).FirstOrDefault()?.Timestamp < window.End));
}

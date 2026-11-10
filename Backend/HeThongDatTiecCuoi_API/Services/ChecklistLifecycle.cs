using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

// Called inside the existing booking/assignment transaction; never commits separately.
public static class ChecklistLifecycle
{
    public static readonly (string Group, string Name, int Offset)[] Template =
    [
        ("Trước tiệc", "Kiểm tra bố trí bàn và sơ đồ sảnh", -1),
        ("Trước tiệc", "Xác nhận thực đơn và số bàn với bếp", -1),
        ("Trước tiệc", "Kiểm tra trang trí, âm thanh và ánh sáng", 0),
        ("Trước tiệc", "Đối chiếu yêu cầu đặc biệt của khách", 0),
        ("Trong tiệc", "Theo dõi đón khách và phục vụ món", 0),
        ("Trong tiệc", "Theo dõi dịch vụ và các phát sinh trong tiệc", 0),
        ("Sau tiệc", "Kiểm tra bàn giao sảnh và thiết bị", 0),
        ("Sau tiệc", "Ghi nhận tổng kết ca điều phối", 1)
    ];

    public static void AddHistory(PartyChecklist checklist, int? userId, string actor, string action, string text, DateTime? timestamp = null) =>
        checklist.History.Add(new PartyChecklistHistory
        {
            ActorUserId = userId, ActorName = actor, Action = action, Text = text,
            Timestamp = timestamp ?? RiversideBusinessClock.Now
        });

    public static async Task<PartyChecklist> EnsureAsync(ApplicationDbContext db, int bookingId,
        int? userId, string actor, CancellationToken ct)
    {
        var checklist = db.PartyChecklists.Local.FirstOrDefault(c => c.BookingId == bookingId)
            ?? await db.PartyChecklists.SingleOrDefaultAsync(c => c.BookingId == bookingId, ct);
        if (checklist is not null) return checklist;
        checklist = new PartyChecklist { BookingId = bookingId };
        foreach (var (group, name, offset) in Template)
            checklist.Tasks.Add(new PartyChecklistTask { Name = name, Group = group, DueOffsetDays = offset });
        AddHistory(checklist, userId, actor, "CREATED", "Tạo checklist điều phối tiêu chuẩn gồm 8 công việc.");
        db.PartyChecklists.Add(checklist);
        return checklist;
    }

    public static async Task RecordAsync(ApplicationDbContext db, int bookingId, int? userId,
        string actor, string action, string text, string? state, CancellationToken ct)
    {
        var checklist = db.PartyChecklists.Local.FirstOrDefault(c => c.BookingId == bookingId)
            ?? await db.PartyChecklists.SingleOrDefaultAsync(c => c.BookingId == bookingId, ct);
        if (checklist is null) return;
        if (state is not null) checklist.State = state;
        checklist.Version++;
        AddHistory(checklist, userId, actor, action, text);
    }
}

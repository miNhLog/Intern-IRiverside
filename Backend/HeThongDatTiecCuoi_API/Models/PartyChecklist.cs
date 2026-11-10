namespace HeThongDatTiecCuoi_API.Models;

public sealed class PartyChecklist
{
    public int BookingId { get; set; }
    public string State { get; set; } = "active";
    public int Version { get; set; } = 1;
    public string? ReturnReason { get; set; }
    public WeddingBooking Booking { get; set; } = null!;
    public ICollection<PartyChecklistTask> Tasks { get; set; } = [];
    public ICollection<PartyChecklistHistory> History { get; set; } = [];
}

public sealed class PartyChecklistTask
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string Name { get; set; } = "";
    public string Group { get; set; } = "Trước tiệc";
    public int DueOffsetDays { get; set; }
    public bool Required { get; set; } = true;
    public string Status { get; set; } = "todo";
    public string? Note { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // Derived from the existing audit history; never requires a new database column.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime? CompletedAt
    {
        get
        {
            if (Status != "done" || Checklist is null) return null;
            var uniqueName = Checklist.Tasks.Count(t => t.Name == Name) == 1;
            var prefix = $"Công việc “{Name}”: ";
            var latest = Checklist.History.Where(h =>
                    (h.Action == $"TASK:{Id}" || uniqueName && h.Action == "TASK" && h.Text.StartsWith(prefix, StringComparison.Ordinal)) &&
                    !h.Text.EndsWith(": Hoàn thành → Hoàn thành.", StringComparison.Ordinal))
                .OrderByDescending(h => h.Id).FirstOrDefault();
            return latest?.Text.EndsWith(" → Hoàn thành.", StringComparison.Ordinal) == true ? latest.Timestamp : null;
        }
    }
    public PartyChecklist Checklist { get; set; } = null!;
}

public sealed class PartyChecklistHistory
{
    public long Id { get; set; }
    public int BookingId { get; set; }
    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = "Hệ thống";
    public string Action { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public PartyChecklist Checklist { get; set; } = null!;
}

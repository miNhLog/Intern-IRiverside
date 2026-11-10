using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Checklist;

public sealed class ChecklistCommand
{
    [Range(0, int.MaxValue)] public int Version { get; set; }
    public int? TaskId { get; set; }
    [StringLength(180)] public string? Name { get; set; }
    [StringLength(30)] public string? Group { get; set; }
    public DateTime? DueDate { get; set; }
    public bool Required { get; set; } = true;
    [StringLength(20)] public string? Status { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}

public sealed class ChecklistWorkspaceDto
{
    public string Role { get; set; } = "coordinator";
    public string Today { get; set; } = "";
    public List<ChecklistPartyDto> Parties { get; set; } = [];
}

public sealed class ChecklistPartyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Date { get; set; } = "";
    public string Shift { get; set; } = "";
    public string Hall { get; set; } = "";
    public int Tables { get; set; }
    public string? Coordinator { get; set; }
    public string State { get; set; } = "unassigned";
    public string BookingState { get; set; } = "";
    public int Version { get; set; }
    public bool CanEdit { get; set; }
    public bool CanManage { get; set; }
    public bool CanRepairTiming { get; set; }
    public bool NeedsTimingRepair { get; set; }
    public bool CanHandover { get; set; }
    public string? TimingMessage { get; set; }
    public string? ShiftHours { get; set; }
    public string? ReturnReason { get; set; }
    public List<ChecklistTaskDto> Tasks { get; set; } = [];
    public List<ChecklistHistoryDto> History { get; set; } = [];
    public List<ChecklistIssueDto> Issues { get; set; } = [];
}
public sealed record ChecklistTaskDto(int Id, string Name, string Group, string Due, bool Required,
    string Status, string? Note, string? By, DateTime? UpdatedAt,
    bool CanStart = true, bool CanComplete = true, string? TimingMessage = null, DateTime? CompletedAt = null, bool CompletionValid = false);
public sealed record ChecklistHistoryDto(long Id, string Text, string By, DateTime Time);
public sealed record ChecklistIssueDto(int Id, string Code, string Title, string Description, bool Resolved);

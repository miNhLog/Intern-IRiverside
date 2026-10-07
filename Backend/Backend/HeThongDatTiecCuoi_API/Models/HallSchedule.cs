namespace HeThongDatTiecCuoi_API.Models;

public sealed class HallSchedule
{
    public int HallScheduleId { get; set; }

    public int HallId { get; set; }

    public DateTime Date { get; set; }

    public string Shift { get; set; } = string.Empty;

    public int StatusId { get; set; }
    public byte DataStatusId { get; set; }

    public Hall Hall { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<WeddingScheduleChange> ScheduleChangesAsOldSchedule { get; set; } = [];
    public ICollection<WeddingScheduleChange> ScheduleChangesAsNewSchedule { get; set; } = [];
}

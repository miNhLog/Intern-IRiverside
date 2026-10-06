namespace HeThongDatTiecCuoi_API.Models;

public sealed class Hall
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public byte DataStatusId { get; set; }
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<HallSchedule> Schedules { get; set; } = [];
    public ICollection<ImageAsset> Images { get; set; } = [];
    public ICollection<HallManagerAssignment> HallManagerAssignments { get; set; } = [];
}

namespace HeThongDatTiecCuoi_API.Models;

public sealed class HallManagerAssignment
{
    public int HallManagerAssignmentId { get; set; }
    public int HallId { get; set; }
    public int HallManagerEmployeeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public Hall Hall { get; set; } = null!;
    public Employee HallManager { get; set; } = null!;
}

namespace HeThongDatTiecCuoi_API.Models;

public sealed class CoordinationAssignment
{
    public int CoordinationAssignmentId { get; set; }
    public int BookingId { get; set; }
    public int CoordinatorEmployeeId { get; set; }
    public int AssignedByHallManagerEmployeeId { get; set; }
    public int StatusId { get; set; }
    public DateTime AssignedAt { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public Employee Coordinator { get; set; } = null!;
    public Employee AssignedByHallManager { get; set; } = null!;
    public Status Status { get; set; } = null!;
}

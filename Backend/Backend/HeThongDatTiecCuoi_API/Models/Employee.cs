namespace HeThongDatTiecCuoi_API.Models;

public sealed class Employee
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public byte DataStatusId { get; set; }

    public User User { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<HallManagerAssignment> HallManagerAssignments { get; set; } = [];
    public ICollection<CoordinationAssignment> CoordinationAssignmentsAsCoordinator { get; set; } = [];
    public ICollection<CoordinationAssignment> CoordinationAssignmentsAsHallManager { get; set; } = [];
    public ICollection<Incident> ReportedIncidents { get; set; } = [];
}

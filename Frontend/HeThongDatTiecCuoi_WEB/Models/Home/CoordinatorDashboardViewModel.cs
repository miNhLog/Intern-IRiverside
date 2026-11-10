using HeThongDatTiecCuoi_WEB.Models.Coordination;

namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class CoordinatorDashboardViewModel
{
    public DateTime GeneratedAt { get; set; }
    public List<CoordinatorAssignedPartyViewModel> TodayParties { get; set; } = [];
    public int? TodayCount { get; set; }
    public int? UpcomingCount { get; set; }
    public int? IncompleteWorkCount { get; set; }
    public int? OpenIncidentCount { get; set; }
    public string? PartiesError { get; set; }
    public string? IncidentsError { get; set; }
    public string? ChecklistError { get; set; }
}

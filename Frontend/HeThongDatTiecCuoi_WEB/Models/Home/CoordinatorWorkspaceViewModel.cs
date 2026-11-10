using HeThongDatTiecCuoi_WEB.Models.Coordination;

namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class CoordinatorWorkspaceViewModel
{
    public string Page { get; set; } = "schedule";
    public DateTime Today { get; set; }
    public List<CoordinatorAssignedPartyViewModel> Parties { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

using HeThongDatTiecCuoi_WEB.Models.AdminHall;

namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class HomeViewModel
{
    public IReadOnlyList<HallDto> FeaturedHalls { get; init; } = [];
}

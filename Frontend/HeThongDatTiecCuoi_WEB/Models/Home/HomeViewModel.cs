using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.Banners;

namespace HeThongDatTiecCuoi_WEB.Models.Home;

public sealed class HomeViewModel
{
    public IReadOnlyList<HallDto> FeaturedHalls { get; init; } = [];
    public IReadOnlyList<BannerViewModel> Banners { get; init; } = [];
}

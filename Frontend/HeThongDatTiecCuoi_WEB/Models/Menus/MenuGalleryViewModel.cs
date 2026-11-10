namespace HeThongDatTiecCuoi_WEB.Models.Menus;

public sealed class MenuGalleryViewModel
{
    public IReadOnlyList<PublicDishDto> Dishes { get; init; } = [];
    public IReadOnlyList<MenuGallerySetViewModel> Menus { get; init; } = [];
    public int? SelectedMenuId { get; init; }
    public string? DishesError { get; init; }
    public string? MenusError { get; init; }

    public IReadOnlyList<string> Categories =>
        new[] { "Món khai vị", "Món súp", "Món chính", "Món tráng miệng" }
            .Concat(Dishes.Select(dish => dish.Category.Trim()))
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record MenuGallerySetViewModel(
    PublicMenuDto Menu,
    IReadOnlyList<PublicMenuDishDto> Dishes,
    string? ErrorMessage);

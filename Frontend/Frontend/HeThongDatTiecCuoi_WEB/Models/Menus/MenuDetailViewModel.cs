namespace HeThongDatTiecCuoi_WEB.Models.Menus;

public sealed class PublicMenuDetailDto
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public int DishCount { get; set; }
    public IReadOnlyList<PublicMenuDishDto> Dishes { get; set; } = [];
}

public sealed class PublicMenuDishDto
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
}

public sealed class MenuDetailViewModel
{
    private static readonly (string Name, string Icon)[] CategoryDefinitions =
    [
        ("Món khai vị", "room_service"),
        ("Món súp", "soup_kitchen"),
        ("Món chính", "restaurant"),
        ("Món tráng miệng", "cake")
    ];

    public int MenuId { get; init; }
    public string MenuCode { get; init; } = string.Empty;
    public string MenuName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal PricePerTable { get; init; }
    public int DishCount { get; init; }
    public IReadOnlyList<PublicMenuDishDto> Dishes { get; init; } = [];
    public bool IsNotFound { get; init; }
    public string? ErrorMessage { get; init; }

    public IReadOnlyList<MenuDishGroupViewModel> DishGroups
    {
        get
        {
            var orderedDishes = Dishes.OrderBy(dish => dish.SortOrder).ToList();
            var groups = new List<MenuDishGroupViewModel>();

            foreach (var definition in CategoryDefinitions)
            {
                var dishes = orderedDishes
                    .Where(dish => string.Equals(
                        dish.Category?.Trim(),
                        definition.Name,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (dishes.Count > 0)
                {
                    groups.Add(new MenuDishGroupViewModel(definition.Name, definition.Icon, dishes));
                }
            }

            var otherDishes = orderedDishes
                .Where(dish => !CategoryDefinitions.Any(definition => string.Equals(
                    dish.Category?.Trim(),
                    definition.Name,
                    StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (otherDishes.Count > 0)
            {
                groups.Add(new MenuDishGroupViewModel("Món khác", "restaurant_menu", otherDishes));
            }

            return groups;
        }
    }
}

public sealed record MenuDishGroupViewModel(
    string Name,
    string Icon,
    IReadOnlyList<PublicMenuDishDto> Dishes);

namespace HeThongDatTiecCuoi_API.DTOs.Dish;

public sealed class ManagerDishListResponseDto
{
    public IReadOnlyList<ManagerDishListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int TotalDishes { get; set; }
    public int ActiveDishes { get; set; }
    public int InactiveDishes { get; set; }
    public int CategoryCount { get; set; }
}

public class ManagerDishListItemDto
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int MenuUsageCount { get; set; }
}

public sealed class ManagerDishDetailDto : ManagerDishListItemDto
{
    public int UsageCount { get; set; }
    public IReadOnlyList<DishMenuUsageDto> Menus { get; set; } = [];
}

public sealed class DishMenuUsageDto
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

namespace HeThongDatTiecCuoi_WEB.Models.ManagerDish;

public sealed class ManagerDishPageViewModel
{
    public List<ManagerDishItemViewModel> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int TotalDishes { get; set; }
    public int ActiveDishes { get; set; }
    public int InactiveDishes { get; set; }
    public int CategoryCount { get; set; }
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ManagerDishItemViewModel
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

public sealed class ManagerDishDetailViewModel : ManagerDishItemViewModel
{
    public int UsageCount { get; set; }
    public List<DishMenuUsageViewModel> Menus { get; set; } = [];
}

public sealed class DishMenuUsageViewModel
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

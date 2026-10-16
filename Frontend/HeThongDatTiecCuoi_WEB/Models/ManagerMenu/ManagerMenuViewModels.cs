using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.ManagerMenu;

public sealed class ManagerMenuPageViewModel
{
    public List<ManagerMenuItemViewModel> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int TotalMenus { get; set; }
    public int ActiveMenus { get; set; }
    public int InactiveMenus { get; set; }
    public decimal AveragePricePerTable { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ManagerMenuItemViewModel
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DishCount { get; set; }
    public bool IsInUse { get; set; }
}

public sealed class BatchAddMenuDishesForm
{
    public int MenuId { get; set; }
    [MinLength(1)] public List<int> DishIds { get; set; } = [];
}

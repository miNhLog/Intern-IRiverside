namespace HeThongDatTiecCuoi_WEB.Models.AdminMenu;

public sealed class MenuManagementViewModel
{
    public List<MenuViewModel> Menus { get; set; } = new();
    public List<DishViewModel> Dishes { get; set; } = new();
    public List<DishViewModel> DishesForCounts { get; set; } = new();
    public List<DishViewModel> ActiveDishes { get; set; } = new();
    public int? SelectedMenuId { get; set; }
    public List<MenuDishViewModel> MenuDishes { get; set; } = new();
    public string? MenuKeyword { get; set; }
    public string? MenuStatus { get; set; }
    public string? DishKeyword { get; set; }
    public string? Category { get; set; }
    public string? DishStatus { get; set; }
}

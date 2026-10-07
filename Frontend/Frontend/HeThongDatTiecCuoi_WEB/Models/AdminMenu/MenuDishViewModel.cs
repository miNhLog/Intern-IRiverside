namespace HeThongDatTiecCuoi_WEB.Models.AdminMenu;

public sealed class MenuDishViewModel
{
    public int MenuDishId { get; set; }
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class MenuDishDto
{
    public int MenuDishId { get; set; }
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

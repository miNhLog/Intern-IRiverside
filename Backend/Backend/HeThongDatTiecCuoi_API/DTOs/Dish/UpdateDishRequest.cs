namespace HeThongDatTiecCuoi_API.DTOs.Dish;

public sealed class UpdateDishRequest
{
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

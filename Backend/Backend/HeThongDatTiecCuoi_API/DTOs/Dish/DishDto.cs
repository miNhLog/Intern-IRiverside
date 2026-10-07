namespace HeThongDatTiecCuoi_API.DTOs.Dish;

public sealed class DishDto
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

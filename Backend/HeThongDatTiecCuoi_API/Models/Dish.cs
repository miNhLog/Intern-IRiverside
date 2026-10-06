namespace HeThongDatTiecCuoi_API.Models;

public sealed class Dish
{
    public int DishId { get; set; }

    public string DishCode { get; set; } = string.Empty;

    public string DishName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public int StatusId { get; set; }

    public byte DataStatusId { get; set; }

    public Status Status { get; set; } = null!;

    public DataStatus DataStatus { get; set; } = null!;

    public ICollection<MenuDish> MenuDishes { get; set; } = [];
}

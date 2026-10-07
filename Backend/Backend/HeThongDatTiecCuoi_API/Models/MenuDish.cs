namespace HeThongDatTiecCuoi_API.Models;

public sealed class MenuDish
{
    public int MenuDishId { get; set; }

    public int MenuId { get; set; }

    public int DishId { get; set; }

    public int SortOrder { get; set; }

    public byte DataStatusId { get; set; }

    public Menu Menu { get; set; } = null!;

    public Dish Dish { get; set; } = null!;

    public DataStatus DataStatus { get; set; } = null!;
}

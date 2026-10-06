namespace HeThongDatTiecCuoi_API.Models;

public sealed class Menu
{
    public int MenuId { get; set; }

    public string MenuCode { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    public string MenuType { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public string? Description { get; set; }

    public decimal PricePerTable { get; set; }

    public int StatusId { get; set; }

    public byte DataStatusId { get; set; }

    public Status Status { get; set; } = null!;

    public DataStatus DataStatus { get; set; } = null!;

    public Customer? Customer { get; set; }

    public ICollection<MenuDish> MenuDishes { get; set; } = [];
}

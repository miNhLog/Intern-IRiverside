namespace HeThongDatTiecCuoi_WEB.Models.AdminMenu;

public sealed class MenuViewModel
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

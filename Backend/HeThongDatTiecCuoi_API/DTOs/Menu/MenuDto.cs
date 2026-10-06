namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class MenuDto
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class CreateMenuRequest
{
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
}

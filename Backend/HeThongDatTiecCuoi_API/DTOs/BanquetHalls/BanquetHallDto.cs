namespace HeThongDatTiecCuoi_API.DTOs.BanquetHalls;

public sealed class BanquetHallDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
}

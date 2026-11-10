namespace HeThongDatTiecCuoi_API.DTOs.Hall;

public sealed class UpdateHallRequest
{
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
}

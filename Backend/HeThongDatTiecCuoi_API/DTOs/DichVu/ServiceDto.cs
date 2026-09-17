namespace HeThongDatTiecCuoi_API.DTOs.DichVu;

public sealed class ServiceDto
{
    public int ServiceId { get; set; }

    public string ServiceCode { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string? ServiceType { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = string.Empty;
}

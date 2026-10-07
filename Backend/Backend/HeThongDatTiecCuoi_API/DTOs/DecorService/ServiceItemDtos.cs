using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.DecorService;

public sealed class ServiceItemRequest
{
    [Required, MaxLength(20)] public string ServiceCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string ServiceName { get; set; } = string.Empty;
    [MaxLength(100)] public string? ServiceType { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal Price { get; set; }
    public string? StatusCode { get; set; }
}

public sealed class ServiceItemDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string? ServiceType { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

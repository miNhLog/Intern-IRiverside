using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.DecorService;

public sealed class DecorPackageRequest
{
    [Required, MaxLength(20)] public string PackageCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string PackageName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Style { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal Price { get; set; }
    public string? StatusCode { get; set; }
}

public sealed class DecorPackageDto
{
    public int DecorationPackageId { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

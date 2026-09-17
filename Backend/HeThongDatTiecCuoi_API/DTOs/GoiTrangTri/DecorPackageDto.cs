namespace HeThongDatTiecCuoi_API.DTOs.GoiTrangTri;

public sealed class DecorPackageDto
{
    public int PackageId { get; set; }

    public string PackageCode { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string? Style { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = string.Empty;
}

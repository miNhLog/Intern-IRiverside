namespace HeThongDatTiecCuoi_WEB.Models.DecorPackages;

public sealed class DecorPackageDetailViewModel
{
    public int DecorationPackageId { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public string Style { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsNotFound { get; init; }
    public string? ErrorMessage { get; init; }
}
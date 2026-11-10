namespace HeThongDatTiecCuoi_API.DTOs.DecorService;

public sealed class PublicDecorPackageDto
{
    public int DecorationPackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class PublicDecorPackageListResponse
{
    public IReadOnlyList<PublicDecorPackageDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
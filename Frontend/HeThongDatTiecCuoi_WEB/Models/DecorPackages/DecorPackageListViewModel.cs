namespace HeThongDatTiecCuoi_WEB.Models.DecorPackages;

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

public sealed class DecorPackageListViewModel
{
    public IReadOnlyList<PublicDecorPackageDto> Packages { get; init; } = [];
    public string? Keyword { get; init; }
    public string Sort { get; init; } = "name-asc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 6;
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public string? ErrorMessage { get; init; }
}
namespace HeThongDatTiecCuoi_WEB.Models.Halls;

public sealed class PublicHallDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class PublicHallListResponse
{
    public IReadOnlyList<PublicHallDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class HallListViewModel
{
    public IReadOnlyList<PublicHallDto> Halls { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Capacity { get; init; }
    public string? PriceRange { get; init; }
    public string Sort { get; init; } = "name-asc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 6;
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class PublicHallDetailDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class HallDetailViewModel
{
    public int HallId { get; init; }
    public string HallCode { get; init; } = string.Empty;
    public string HallName { get; init; } = string.Empty;
    public int? MinimumCapacity { get; init; }
    public int MaximumCapacity { get; init; }
    public decimal RentalPrice { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public bool IsNotFound { get; init; }
    public string? ErrorMessage { get; init; }
}

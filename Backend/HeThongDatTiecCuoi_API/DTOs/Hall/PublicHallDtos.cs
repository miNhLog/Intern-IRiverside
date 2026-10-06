namespace HeThongDatTiecCuoi_API.DTOs.Hall;

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

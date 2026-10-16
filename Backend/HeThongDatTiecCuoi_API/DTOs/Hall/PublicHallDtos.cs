namespace HeThongDatTiecCuoi_API.DTOs.Hall;

public sealed class PublicHallDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int? MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách). MinimumCapacity NULL fallback 150 khách.
    public int CapacityMinGuests { get; set; }
    public int CapacityMaxGuests { get; set; }
    public int CapacityMinTables { get; set; }
    public int CapacityMaxTables { get; set; }
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
    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách). MinimumCapacity NULL fallback 150 khách.
    public int CapacityMinGuests { get; set; }
    public int CapacityMaxGuests { get; set; }
    public int CapacityMinTables { get; set; }
    public int CapacityMaxTables { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

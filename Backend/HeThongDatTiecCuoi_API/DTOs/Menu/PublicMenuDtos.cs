namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class PublicMenuDto
{
    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public int DishCount { get; set; }
}

public sealed class PublicMenuListResponse
{
    public IReadOnlyList<PublicMenuDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class PublicMenuDetailDto
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public int DishCount { get; set; }
    public IReadOnlyList<PublicMenuDishDto> Dishes { get; set; } = [];
}

public sealed class PublicMenuDishDto
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
}

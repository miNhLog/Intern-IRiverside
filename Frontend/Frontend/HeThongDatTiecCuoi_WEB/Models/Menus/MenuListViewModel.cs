namespace HeThongDatTiecCuoi_WEB.Models.Menus;

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

public sealed class MenuListViewModel
{
    public IReadOnlyList<PublicMenuDto> Menus { get; init; } = [];
    public string? Keyword { get; init; }
    public string? PriceRange { get; init; }
    public string Sort { get; init; } = "name-asc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 6;
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public string? ErrorMessage { get; init; }
}

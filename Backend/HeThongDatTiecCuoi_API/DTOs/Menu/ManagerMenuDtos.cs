namespace HeThongDatTiecCuoi_API.DTOs.Menu;

public sealed class ManagerMenuListResponseDto
{
    public IReadOnlyList<ManagerMenuListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int TotalMenus { get; set; }
    public int ActiveMenus { get; set; }
    public int InactiveMenus { get; set; }
    public decimal AveragePricePerTable { get; set; }
}

public sealed class ManagerMenuListItemDto
{
    public int MenuId { get; set; }
    public string MenuCode { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerTable { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DishCount { get; set; }
    public bool IsInUse { get; set; }
}

public sealed class BatchAddMenuDishesRequest
{
    public List<int> DishIds { get; set; } = [];
}

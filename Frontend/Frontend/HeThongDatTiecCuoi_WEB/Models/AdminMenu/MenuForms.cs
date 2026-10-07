using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_WEB.Models.AdminMenu;

public sealed class CreateMenuForm
{
    [Required]
    public string MenuName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PricePerTable { get; set; }
}

public sealed class UpdateMenuForm
{
    public int MenuId { get; set; }

    [Required]
    public string MenuName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PricePerTable { get; set; }
}

public sealed class CreateDishForm
{
    [Required]
    public string DishName { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public IFormFile? Image { get; set; }
}

public sealed class UpdateDishForm
{
    public int DishId { get; set; }

    [Required]
    public string DishName { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public IFormFile? NewImage { get; set; }

    public bool DeleteImage { get; set; }
}

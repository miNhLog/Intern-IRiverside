using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Banner;

public sealed record BannerDto(int BannerId, string Placement, string Title, string Subtitle, string Description,
    string NavigationLabel, string? ImagePath, string ImageAlt, string PrimaryActionType,
    string? PrimaryButtonLabel, string? PrimaryLink, string SecondaryActionType,
    string? SecondaryButtonLabel, string? SecondaryLink, int SortOrder, bool IsVisible,
    DateTime CreatedAt, DateTime? UpdatedAt, string Version);

public sealed class BannerUpsertRequest
{
    [Required, StringLength(30)] public string Placement { get; set; } = "HOME";
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Subtitle { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NavigationLabel { get; set; } = string.Empty;
    [Required, StringLength(250)] public string ImageAlt { get; set; } = string.Empty;
    [Required, StringLength(20)] public string PrimaryActionType { get; set; } = "NONE";
    [StringLength(80)] public string? PrimaryButtonLabel { get; set; }
    [StringLength(300)] public string? PrimaryLink { get; set; }
    [Required, StringLength(20)] public string SecondaryActionType { get; set; } = "NONE";
    [StringLength(80)] public string? SecondaryButtonLabel { get; set; }
    [StringLength(300)] public string? SecondaryLink { get; set; }
    [Range(1, int.MaxValue)] public int SortOrder { get; set; } = 1;
    public bool IsVisible { get; set; }
    public string? Version { get; set; }
}

public sealed class BannerReorderRequest
{
    [Required, StringLength(30)] public string Placement { get; set; } = "HOME";
    [Required, MinLength(1)] public List<int> BannerIds { get; set; } = [];
}

public sealed class BannerVisibilityRequest
{
    public bool IsVisible { get; set; }
    public string? Version { get; set; }
}

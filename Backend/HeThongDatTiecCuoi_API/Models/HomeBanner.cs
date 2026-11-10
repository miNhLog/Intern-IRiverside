namespace HeThongDatTiecCuoi_API.Models;

public sealed class HomeBanner
{
    public int BannerId { get; set; }
    public string Placement { get; set; } = "HOME";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string NavigationLabel { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string ImageAlt { get; set; } = string.Empty;
    public string PrimaryActionType { get; set; } = "NONE";
    public string? PrimaryButtonLabel { get; set; }
    public string? PrimaryLink { get; set; }
    public string SecondaryActionType { get; set; } = "NONE";
    public string? SecondaryButtonLabel { get; set; }
    public string? SecondaryLink { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

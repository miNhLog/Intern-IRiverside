namespace HeThongDatTiecCuoi_API.Models;

public sealed class ImageAsset
{
    public int ImageId { get; set; }
    public int? HallId { get; set; }
    public int? DecorationPackageId { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public byte DataStatusId { get; set; }

    public Hall? Hall { get; set; }
    public DecorPackage? DecorationPackage { get; set; }
    public DataStatus DataStatus { get; set; } = null!;
}

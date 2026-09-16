using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.Options;

public sealed class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxDimension = 8192;
    public const long MaxPixels = 40_000_000;

    [Required]
    public string RootPath { get; init; } = string.Empty;

    [Required]
    public string PublicBasePath { get; init; } = "/assets/images";
}

namespace HeThongDatTiecCuoi_WEB.Models;

public sealed class ImageGalleryViewModel
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Images { get; init; } = [];
    public string Placeholder { get; init; } = string.Empty;
}

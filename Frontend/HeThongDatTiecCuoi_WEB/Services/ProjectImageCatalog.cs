namespace HeThongDatTiecCuoi_WEB.Services;

// Shared, versioned images shipped with WEB; API uploads remain separate.
public sealed class ProjectImageCatalog(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IReadOnlyDictionary<string, string> GetImages(string category) =>
        configuration.GetSection("ProjectImages").GetSection(category).GetChildren()
            .Select(item => new { item.Key, Url = GetImage(category, item.Key) })
            .Where(item => item.Url is not null)
            .ToDictionary(item => item.Key, item => item.Url!, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetGallery(string category, string key)
    {
        var url = configuration.GetSection("ProjectImages").GetSection(category)[key]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/images/", StringComparison.Ordinal)
            || url.Contains('\\') || url.Split('/').Any(segment => segment is "." or ".."))
            return [];

        var directory = Path.Combine(environment.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => $"{url}/{Uri.EscapeDataString(Path.GetFileName(path))}")
            .ToArray();
    }

    public string? GetImage(string category, string key)
    {
        var url = configuration.GetSection("ProjectImages").GetSection(category)[key]?.Trim();
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/images/", StringComparison.Ordinal)
            || url.Contains('\\') || url.Split('/').Any(segment => segment is "." or ".."))
            return null;

        if (Path.GetExtension(url).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".svg"))
            return null;

        var imagePath = Path.Combine(environment.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(imagePath) ? url : null;
    }
}

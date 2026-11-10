namespace HeThongDatTiecCuoi_WEB.Services;

public static class HallGalleryCatalog
{
    private static readonly Dictionary<string, string> HallFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Amur"] = "amur",
        ["Danube"] = "danube",
        ["Elbe"] = "elbe",
        ["Grand Ballroom"] = "grand-ballroom",
        ["Green Riverside"] = "green-riverside",
        ["Nile"] = "nile",
        ["Seine"] = "seine",
        ["Thames"] = "thames",
        ["Volga"] = "volga"
    };

    public static IReadOnlyList<string> GetImages(string hallName, string webRootPath)
    {
        if (!HallFolders.TryGetValue(hallName.Trim(), out var folder)) return [];

        var directory = Path.Combine(webRootPath, "images", "halls", folder);
        if (!Directory.Exists(directory)) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => $"/images/halls/{folder}/{Uri.EscapeDataString(Path.GetFileName(path))}")
            .ToArray();
    }
}

using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[AllowAnonymous]
public sealed class SiteBannerController : Controller
{
    private static readonly IReadOnlyDictionary<string, string> Fallbacks =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HALLS"] = "/images/page-banners/halls.jpg",
            ["MENUS"] = "/images/page-banners/menus.jpg",
            ["DECOR"] = "/images/page-banners/decor.jpg",
            ["RECOMMENDATION"] = "/images/page-banners/recommendation.jpg",
            ["MY_BOOKING"] = "/images/page-banners/my-booking.jpg"
        };

    private readonly IRiversideApiClient _apiClient;
    public SiteBannerController(IRiversideApiClient apiClient) => _apiClient = apiClient;

    [HttpGet("site-banner/{placement}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
    public async Task<IActionResult> Image(string placement, CancellationToken ct)
    {
        if (!Fallbacks.TryGetValue(placement, out var fallback)) return NotFound();
        var result = await _apiClient.GetPublicBannersAsync(placement.ToUpperInvariant(), ct);
        var path = result.Succeeded ? result.Value?.OrderBy(x => x.SortOrder).FirstOrDefault()?.ImagePath : null;
        if (string.IsNullOrWhiteSpace(path)) return Redirect(fallback);
        if (path.StartsWith("/uploads/banners/", StringComparison.Ordinal))
            return Redirect($"/banner-images/{Uri.EscapeDataString(Path.GetFileName(path))}");
        if (path.StartsWith("/", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal))
            return Redirect(path);
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            return Redirect(uri.ToString());
        return Redirect(fallback);
    }
}

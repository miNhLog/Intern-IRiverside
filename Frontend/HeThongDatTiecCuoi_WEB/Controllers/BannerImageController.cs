using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[AllowAnonymous]
public sealed class BannerImageController : ControllerBase
{
    private readonly IHttpClientFactory _clients;
    public BannerImageController(IHttpClientFactory clients) => _clients = clients;

    [HttpGet("/banner-images/{fileName}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string fileName, CancellationToken ct)
    {
        if (fileName != Path.GetFileName(fileName)) return BadRequest();
        using var response = await _clients.CreateClient("RiversideApi").GetAsync($"api/banners/images/{Uri.EscapeDataString(fileName)}", ct);
        if (!response.IsSuccessStatusCode) return NotFound();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return File(bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }
}

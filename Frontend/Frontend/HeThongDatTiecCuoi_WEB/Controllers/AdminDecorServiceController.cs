using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_WEB.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class AdminDecorServiceController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly HttpClient _httpClient;

    public AdminDecorServiceController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("RiversideApi");
    }

    public IActionResult Index() => View();

    [HttpGet("AdminDecorService/data/decor")]
    public Task<IActionResult> GetDecor(CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Get, "api/admin/decor-service/decor", null, cancellationToken);

    [HttpGet("AdminDecorService/data/service")]
    public Task<IActionResult> GetServices(CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Get, "api/admin/decor-service/service", null, cancellationToken);

    [HttpPost("AdminDecorService/data/decor")]
    public Task<IActionResult> CreateDecor([FromBody] object request, CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Post, "api/admin/decor-service/decor", request, cancellationToken);

    [HttpPut("AdminDecorService/data/decor/{id:int}")]
    public Task<IActionResult> UpdateDecor(int id, [FromBody] object request, CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Put, $"api/admin/decor-service/decor/{id}", request, cancellationToken);

    [HttpPost("AdminDecorService/data/service")]
    public Task<IActionResult> CreateService([FromBody] object request, CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Post, "api/admin/decor-service/service", request, cancellationToken);

    [HttpPut("AdminDecorService/data/service/{id:int}")]
    public Task<IActionResult> UpdateService(int id, [FromBody] object request, CancellationToken cancellationToken) =>
        ProxyAsync(HttpMethod.Put, $"api/admin/decor-service/service/{id}", request, cancellationToken);

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
            return Unauthorized(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { success = false, message = "Không thể kết nối Backend API." });
        }
    }
}

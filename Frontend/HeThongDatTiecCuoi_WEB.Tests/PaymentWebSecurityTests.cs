using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Constants;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class PaymentWebSecurityTests : IClassFixture<PaymentWebFactory>
{
    private readonly PaymentWebFactory _factory;
    public PaymentWebSecurityTests(PaymentWebFactory factory) => _factory = factory;
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.Admin, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.HallManager, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Coordinator, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Customer, HttpStatusCode.Forbidden)]
    public async Task Web_RejectsNonManagers(string? role, HttpStatusCode expected)
    {
        using var client = Client(role);
        using var result = await client.PostAsJsonAsync("/quan-ly/hop-dong-thanh-toan/payments", new { });
        Assert.Equal(expected, result.StatusCode);
    }
    [Theory]
    [InlineData("payments")]
    [InlineData("contracts/1/deposit-commitment")]
    public async Task ManagerPost_RequiresCsrfToken(string path)
    {
        using var client = Client(RoleNames.Manager);
        using var result = await client.PostAsJsonAsync($"/quan-ly/hop-dong-thanh-toan/{path}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
    [Fact]
    public async Task ValidCsrf_InvalidInputRejected_AndMissingApiSessionUnauthorized()
    {
        using var client = Client(RoleNames.Manager);
        var token = await client.GetFromJsonAsync<TokenResponse>("/uc17-test/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token!.Token);
        using var invalid = await client.PostAsJsonAsync("/quan-ly/hop-dong-thanh-toan/payments", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var valid = await client.PostAsJsonAsync("/quan-ly/hop-dong-thanh-toan/payments", new
        { contractId = 1, requestId = Guid.NewGuid(), amount = 10, paymentType = "Đặt cọc", paymentMethod = "Tiền mặt" });
        Assert.Equal(HttpStatusCode.Unauthorized, valid.StatusCode);
    }
    private HttpClient Client(string? role)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (role is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Uc17Test", role);
        return client;
    }
    public sealed record TokenResponse(string Token);
}

public sealed class PaymentWebFactory : WebApplicationFactory<ManagerContractPaymentController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Uc17Test";
                options.DefaultChallengeScheme = "Uc17Test";
                options.DefaultForbidScheme = "Uc17Test";
            }).AddScheme<AuthenticationSchemeOptions, WebTestAuthentication>("Uc17Test", _ => { });
            services.AddControllers().AddApplicationPart(typeof(TestCsrfController).Assembly);
        });
    }
}

public sealed class WebTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public WebTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) || header.Scheme != "Uc17Test" || header.Parameter is null)
            return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, header.Parameter)], "Uc17Test"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Uc17Test")));
    }
}

// This endpoint exists only in the test assembly/application part, never in WEB production.
[Authorize(Roles = RoleNames.Manager)]
public sealed class TestCsrfController : Controller
{
    [HttpGet("/uc17-test/csrf")]
    public IActionResult Token([FromServices] IAntiforgery antiforgery) =>
        Json(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
}

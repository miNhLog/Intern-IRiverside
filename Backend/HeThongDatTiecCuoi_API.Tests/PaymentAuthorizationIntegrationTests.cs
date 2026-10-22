using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PaymentAuthorizationIntegrationTests : IClassFixture<PaymentAuthorizationFactory>
{
    private readonly PaymentAuthorizationFactory _factory;
    public PaymentAuthorizationIntegrationTests(PaymentAuthorizationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.Admin, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.HallManager, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Coordinator, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Customer, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Manager, HttpStatusCode.BadRequest)]
    public async Task Post_RequiresManagerAndValidInput(string? role, HttpStatusCode expected)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (role is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);
        using var response = await client.PostAsJsonAsync("/api/manager/payments", new { contractId = 1, requestId = Guid.Empty,
            amount = 10m, paymentType = "Đặt cọc", paymentMethod = "Tiền mặt" });
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task OversizedReference_AndMalformedAmount_AreBadRequests()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, RoleNames.Manager);
        foreach (var amount in new[] { "0", "-1", "0.001", "\"invalid\"" })
        {
            using var body = new StringContent($$"""{"contractId":1,"requestId":"{{Guid.NewGuid()}}","amount":{{amount}},"paymentType":"Đặt cọc","paymentMethod":"Tiền mặt"}""", System.Text.Encoding.UTF8, "application/json");
            using var result = await client.PostAsync("/api/manager/payments", body);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
        using var oversized = await client.PostAsJsonAsync("/api/manager/payments", new { contractId = 1, requestId = Guid.NewGuid(), amount = 1,
            paymentType = "Đặt cọc", paymentMethod = "Tiền mặt", transactionCode = new string('x', 101) });
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }
}

public sealed class PaymentAuthorizationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase($"uc17-auth-{Guid.NewGuid()}"));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultForbidScheme = TestRoleAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>(TestRoleAuthenticationHandler.Scheme, _ => { });
            services.AddTransient<IClaimsTransformation, PaymentTestActor>();
        });
    }
    private sealed class PaymentTestActor : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is ClaimsIdentity identity && !identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "1"));
            return Task.FromResult(principal);
        }
    }
}

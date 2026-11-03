using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AuditLog;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class AdminAuditLogTests : IClassFixture<AdminAuditLogApiFactory>
{
    private readonly AdminAuditLogApiFactory _factory;

    public AdminAuditLogTests(AdminAuditLogApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.Admin, HttpStatusCode.OK)]
    [InlineData(RoleNames.Manager, HttpStatusCode.Forbidden)]
    public async Task Get_RequiresAdmin(string? role, HttpStatusCode expected)
    {
        using var client = CreateClient(role);

        using var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Get_FiltersByEntityNameTogetherWithExistingFilters()
    {
        var marker = Guid.NewGuid().ToString("N");
        await SeedAsync(
            new AuditLog { Action = "CREATE_ACCOUNT", EntityName = "User", EntityId = 101, Timestamp = new DateTime(2026, 10, 20), Notes = marker },
            new AuditLog { Action = "CREATE_ACCOUNT", EntityName = "RoleChangeRequest", EntityId = 102, Timestamp = new DateTime(2026, 10, 20), Notes = marker },
            new AuditLog { Action = "LOCK_ACCOUNT", EntityName = "User", EntityId = 103, Timestamp = new DateTime(2026, 10, 20), Notes = marker });
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync($"/api/admin/audit-logs?entityName=User&action=CREATE_ACCOUNT&fromDate=2026-10-20&toDate=2026-10-20&keyword={marker}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<AuditLogPageDto>();
        var log = Assert.Single(page!.Items, item => item.Notes == marker);
        Assert.Equal("User", log.EntityName);
        Assert.Equal("CREATE_ACCOUNT", log.Action);
    }

    [Fact]
    public async Task Get_ReturnsBadRequestForMalformedDate()
    {
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?fromDate=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_AcceptsMaximumDateWithoutOverflow()
    {
        await SeedAsync(new AuditLog
        {
            Action = "CREATE_ACCOUNT", EntityName = "User", EntityId = 201,
            Timestamp = new DateTime(9999, 12, 31, 23, 59, 59)
        });
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?toDate=9999-12-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<AuditLogPageDto>();
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59), Assert.Single(page!.Items).Timestamp);
    }

    [Fact]
    public async Task Get_RejectsReversedDateRange()
    {
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?fromDate=2026-10-21&toDate=2026-10-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsEntityOutsideAdminVisibleLogScope()
    {
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?entityName=Payment");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ClampsExtremePageToFirstPageWhenNoResults()
    {
        await SeedAsync();
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?page=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<AuditLogPageDto>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(0, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Get_PreservesStablePaginationForExistingResults()
    {
        await SeedAsync(
            new AuditLog { AuditLogId = 1, Action = "CREATE_ACCOUNT", EntityName = "User", EntityId = 1, Timestamp = new DateTime(2026, 10, 20) },
            new AuditLog { AuditLogId = 2, Action = "CREATE_ACCOUNT", EntityName = "User", EntityId = 2, Timestamp = new DateTime(2026, 10, 20) },
            new AuditLog { AuditLogId = 3, Action = "CREATE_ACCOUNT", EntityName = "User", EntityId = 3, Timestamp = new DateTime(2026, 10, 20) });
        using var client = CreateClient(RoleNames.Admin);

        using var response = await client.GetAsync("/api/admin/audit-logs?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<AuditLogPageDto>();
        Assert.NotNull(page);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, Assert.Single(page.Items).AuditLogId);
    }

    private HttpClient CreateClient(string? role)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);
        return client;
    }

    private async Task SeedAsync(params AuditLog[] logs)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync();
    }
}

public sealed class AdminAuditLogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"uc05-audit-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultForbidScheme = TestRoleAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>(TestRoleAuthenticationHandler.Scheme, _ => { });
        });
    }
}

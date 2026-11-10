using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ChecklistRoutingIntegrationTests(EventCoordinationApiFactory factory)
    : IClassFixture<EventCoordinationApiFactory>
{
    [Theory]
    [InlineData("task")]
    [InlineData("note")]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("submit")]
    [InlineData("approve")]
    [InlineData("return")]
    [InlineData("initialize")]
    [InlineData("repair-timing")]
    public async Task OperationUrls_ReachAuthorization_InsteadOfRouting404(string operation)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync($"/api/checklists/1/{operation}", new { version = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Coordinator)]
    [InlineData(RoleNames.HallManager)]
    public async Task AuthenticatedOperation_ReachesServiceAndReturnsJson(string role)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);
        using var response = await client.PostAsJsonAsync("/api/checklists/1/task", new { version = 1, taskId = 1, status = "done" });
        // The test identity has no employee/booking; this must be the service's scoped JSON error.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("message", error);
    }
}

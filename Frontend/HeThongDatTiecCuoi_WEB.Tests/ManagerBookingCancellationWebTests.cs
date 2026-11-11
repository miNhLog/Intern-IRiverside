using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class ManagerBookingCancellationWebTests : IClassFixture<PaymentWebFactory>
{
    private readonly PaymentWebFactory _factory;
    public ManagerBookingCancellationWebTests(PaymentWebFactory factory) => _factory = factory;

    [Fact]
    public async Task CancellationClient_UsesExactUc18RoutesAndDtoFields()
    {
        var sent = new List<CapturedRequest>();
        using var http = new HttpClient(new CaptureHandler(sent)) { BaseAddress = new Uri("https://api.example/") };
        var client = new RiversideApiClient(http, new ProjectImageCatalog(new ConfigurationBuilder().Build(), new TestWebHostEnvironment()));
        var quote = new ManagerBookingCancelQuoteRequestViewModel
        {
            Origin = "CUSTOMER", PolicyVersion = "UC18-CANCEL-V1", AgreementReference = "AGREEMENT-7", AgreedLowerRetention = 100m
        };
        await client.GetManagerBookingCancelQuoteAsync(7, quote, "api-token");
        var commit = new ManagerBookingCancelRequestViewModel
        {
            Reason = "Customer request", RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111"), QuoteToken = "quote-token",
            Origin = quote.Origin, PolicyVersion = quote.PolicyVersion, AgreementReference = quote.AgreementReference,
            AgreedLowerRetention = quote.AgreedLowerRetention
        };
        await client.CancelManagerBookingAsync(7, commit, "api-token");

        Assert.Equal("api/manager/bookings/7/cancel-quote", sent[0].Path);
        Assert.Equal(HttpMethod.Post, sent[0].Method);
        using var quoteBody = JsonDocument.Parse(sent[0].Body);
        Assert.Equal("CUSTOMER", quoteBody.RootElement.GetProperty("origin").GetString());
        Assert.Equal("UC18-CANCEL-V1", quoteBody.RootElement.GetProperty("policyVersion").GetString());
        Assert.Equal("AGREEMENT-7", quoteBody.RootElement.GetProperty("agreementReference").GetString());
        Assert.Equal(100m, quoteBody.RootElement.GetProperty("agreedLowerRetention").GetDecimal());

        Assert.Equal("api/manager/bookings/7/cancel", sent[1].Path);
        Assert.Equal(HttpMethod.Post, sent[1].Method);
        using var commitBody = JsonDocument.Parse(sent[1].Body);
        Assert.Equal("Customer request", commitBody.RootElement.GetProperty("reason").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", commitBody.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("quote-token", commitBody.RootElement.GetProperty("quoteToken").GetString());
    }

    [Fact]
    public void ViewRoutesManagerCancellationThroughUc18AndHasNoLegacyCancelledPayload()
    {
        var view = File.ReadAllText(FindRepositoryFile("Frontend/HeThongDatTiecCuoi_WEB/Views/AdminBooking/Index.cshtml"));
        Assert.Contains("/AdminBooking/GetCancelQuote/", view, StringComparison.Ordinal);
        Assert.Contains("/AdminBooking/Cancel/", view, StringComparison.Ordinal);
        Assert.DoesNotContain("statusCode: 'CANCELLED'", view, StringComparison.Ordinal);
        Assert.Contains("@if (isManager && b.StatusCode", view, StringComparison.Ordinal);
        Assert.Contains("if (canManageContracts && statusCode !== 'CANCELLED'", view, StringComparison.Ordinal);
        Assert.Contains("if (!canManageContracts) return", view, StringComparison.Ordinal);
        Assert.Contains("newStatus === 'CANCELLED'", view, StringComparison.Ordinal);
        Assert.Contains("RequestVerificationToken", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationRequiresAntiforgeryAndAdminCannotMutateBookingState()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Uc17Test", RoleNames.Manager);
        var token = await client.GetFromJsonAsync<TokenResponse>("/uc17-test/csrf");
        using var missingToken = await client.PostAsJsonAsync("/AdminBooking/Cancel/7", new { reason = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        client.DefaultRequestHeaders.Add("RequestVerificationToken", token!.Token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Uc17Test", RoleNames.Admin);
        using var legacyStatus = await client.PostAsJsonAsync("/AdminBooking/UpdateStatus/7", new { statusCode = "CONFIRMED" });
        using var cancel = await client.PostAsJsonAsync("/AdminBooking/Cancel/7", new { reason = "test" });
        using var quote = await client.PostAsJsonAsync("/AdminBooking/GetCancelQuote/7", new { origin = "CUSTOMER" });
        Assert.Equal(HttpStatusCode.Forbidden, legacyStatus.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, quote.StatusCode);
    }

    [Fact]
    public async Task ManagerLegacyStatusUpdateRejectsCancelledPayload()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Uc17Test", RoleNames.Manager);
        var token = await client.GetFromJsonAsync<TokenResponse>("/uc17-test/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token!.Token);

        using var response = await client.PostAsJsonAsync("/AdminBooking/UpdateStatus/7",
            new { statusCode = "CANCELLED", cancellationReason = "legacy fallback" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record TokenResponse(string Token);

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);

    private sealed class CaptureHandler(List<CapturedRequest> sent) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sent.Add(new CapturedRequest(request.Method, request.RequestUri!.PathAndQuery.TrimStart('/'), await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WEB.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Could not find repository file {relativePath} from {AppContext.BaseDirectory}.");
    }
}

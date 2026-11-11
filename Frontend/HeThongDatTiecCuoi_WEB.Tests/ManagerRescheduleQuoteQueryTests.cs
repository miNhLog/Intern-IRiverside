using System.Net;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class ManagerRescheduleQuoteQueryTests
{
    [Fact]
    public void AttestationIsRestrictedToEligibleBookingStatusesAndCloseInvalidatesQuote()
    {
        var view = File.ReadAllText(FindRepositoryFile("Frontend/HeThongDatTiecCuoi_WEB/Views/AdminBooking/Index.cshtml"));
        Assert.Contains("['PENDING', 'CONFIRMED', 'PREPARING'].includes(statusCode)", view, StringComparison.Ordinal);
        var closeStart = view.IndexOf("function closeRescheduleModal()", StringComparison.Ordinal);
        Assert.True(closeStart >= 0);
        Assert.Contains("++rescheduleQuoteSequence", view[closeStart..], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuoteRepeatsCustomDishIdsAndOmitsMissingMenuId()
    {
        Uri? requestedUri = null;
        using var http = new HttpClient(new CaptureHandler(request => requestedUri = request.RequestUri))
        {
            BaseAddress = new Uri("https://api.example/")
        };
        var catalog = new ProjectImageCatalog(new ConfigurationBuilder().Build(), new TestWebHostEnvironment());
        var client = new RiversideApiClient(http, catalog);
        var request = new ManagerBookingRescheduleRequestViewModel
        {
            NewDate = new DateTime(2026, 12, 1),
            NewShift = "Ca tối",
            NewHallId = 4,
            ChangeMenu = true,
            MenuId = null,
            CustomDishIds = [12, 34]
        };

        await client.GetManagerBookingRescheduleQuoteAsync(7, request, "token");

        Assert.NotNull(requestedUri);
        Assert.Contains("customDishIds=12", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("customDishIds=34", requestedUri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("menuId=", requestedUri.Query, StringComparison.Ordinal);
    }

    private sealed class CaptureHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
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

using System.ComponentModel.DataAnnotations;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class RecommendationSecurityAndValidationTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static RecommendationController CreateController(ApplicationDbContext db)
    {
        var stubStatusService = new StubStatusService();
        var stubHttpClientFactory = new StubHttpClientFactory();
        var aiOptions = Microsoft.Extensions.Options.Options.Create(new AiServiceOptions());
        var pricingOptions = Microsoft.Extensions.Options.Options.Create(new PricingPolicyOptions());
        var logger = NullLogger<RecommendationController>.Instance;

        return new RecommendationController(
            db,
            stubStatusService,
            stubHttpClientFactory,
            aiOptions,
            pricingOptions,
            logger
        );
    }

    [Fact]
    public void RecommendationRequestDto_GuestCount_RejectsUnder50()
    {
        var dto = new RecommendationRequestDto
        {
            ExpectedBudget = 200_000_000m,
            GuestCount = 40, // < 50
            DesiredDate = DateTime.Today.AddDays(60),
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại"
        };

        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage?.Contains("50") == true && r.ErrorMessage?.Contains("1400") == true);
    }

    [Fact]
    public void RecommendationRequestDto_GuestCount_RejectsOver1400()
    {
        var dto = new RecommendationRequestDto
        {
            ExpectedBudget = 200_000_000m,
            GuestCount = 1500, // > 1400
            DesiredDate = DateTime.Today.AddDays(60),
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại"
        };

        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage?.Contains("1400") == true);
    }

    [Fact]
    public void RecommendationRequestDto_GuestCount_AcceptsValidRange()
    {
        var dto = new RecommendationRequestDto
        {
            ExpectedBudget = 200_000_000m,
            GuestCount = 300,
            DesiredDate = DateTime.Today.AddDays(60),
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại"
        };

        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, context, results, validateAllProperties: true);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(49)]
    [InlineData(1401)]
    [InlineData(99999)]
    public async Task SuggestTop3ViaAi_RejectsGuestCountOutOfRange(int invalidGuestCount)
    {
        using var db = new ApplicationDbContext(CreateOptions());
        var controller = CreateController(db);

        var request = new RecommendationRequestDto
        {
            GuestCount = invalidGuestCount,
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại",
            DesiredDate = DateTime.Today.AddDays(60),
            ExpectedBudget = 200_000_000m
        };

        var result = await controller.SuggestTop3ViaAi(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var messageProp = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("50 đến 1400 khách", messageProp);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(2000)]
    public async Task SuggestTop3Hybrid_RejectsGuestCountOutOfRange(int invalidGuestCount)
    {
        using var db = new ApplicationDbContext(CreateOptions());
        var controller = CreateController(db);

        var request = new RecommendationRequestDto
        {
            GuestCount = invalidGuestCount,
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại",
            DesiredDate = DateTime.Today.AddDays(60),
            ExpectedBudget = 200_000_000m
        };

        var result = await controller.SuggestTop3Hybrid(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var messageProp = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("50 đến 1400 khách", messageProp);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(1500)]
    public async Task SuggestTop3Combos_RejectsGuestCountOutOfRange(int invalidGuestCount)
    {
        using var db = new ApplicationDbContext(CreateOptions());
        var controller = CreateController(db);

        var request = new RecommendationRequestDto
        {
            GuestCount = invalidGuestCount,
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại",
            DesiredDate = DateTime.Today.AddDays(60),
            ExpectedBudget = 200_000_000m
        };

        var result = await controller.SuggestTop3Combos(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var messageProp = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("50 đến 1400 khách", messageProp);
    }

    [Fact]
    public async Task RegisterPublicBooking_RejectsGuestCountOutOfRange()
    {
        using var db = new ApplicationDbContext(CreateOptions());
        var controller = CreateController(db);

        var request = new RegisterPublicBookingDto
        {
            CustomerName = "Nguyễn Văn A",
            PhoneNumber = "0988776655",
            Email = "customer@example.com",
            GuestCount = 30 // < 50
        };

        var result = await controller.RegisterPublicBooking(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var messageProp = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("50 đến 1400 khách", messageProp);
    }

    private sealed class StubStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(101);

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(new Status { StatusId = 101, StatusGroup = statusGroup, StatusCode = statusCode });
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}

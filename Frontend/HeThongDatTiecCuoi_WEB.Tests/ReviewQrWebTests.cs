using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Review;
using HeThongDatTiecCuoi_WEB.Options;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

/// <summary>
/// UC21/UC22 WEB layer: guest review form, anonymous session handling and the
/// scoped coordinator/manager QR routes. The API client is replaced by a
/// DispatchProxy so no API, database or live service is involved.
/// </summary>
public sealed class ReviewQrWebTests
{
    private const string Token = "review-token-abc";

    private static HttpClient Client(ReviewWebFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task GuestForm_RendersSixCriteriaAndAntiforgeryToken()
    {
        using var factory = new ReviewWebFactory();
        using var client = Client(factory);

        using var response = await client.GetAsync($"/danh-gia/khach-moi/{Token}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var field in new[]
                 {
                     "HallScore", "FoodScore", "ServiceScore", "SoundScore", "LightingScore", "HygieneScore"
                 })
            Assert.Contains($"name=\"{field}\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("name=\"Comment\"", html);
        Assert.Contains("Gửi đánh giá", html);
    }

    [Fact]
    public async Task GuestForm_UnknownToken_ShowsNotFoundState()
    {
        using var factory = new ReviewWebFactory { FormFound = false };
        using var client = Client(factory);

        using var response = await client.GetAsync("/danh-gia/khach-moi/unknown-token");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Mã đánh giá không tồn tại.", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("name=\"HallScore\"", html);
    }

    [Fact]
    public async Task GuestSubmit_WithoutAntiforgeryToken_IsRejected()
    {
        using var factory = new ReviewWebFactory();
        using var client = Client(factory);

        using var response = await client.PostAsync($"/danh-gia/khach-moi/{Token}",
            new FormUrlEncodedContent(Scores()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Api.SubmitCalls);
    }

    [Fact]
    public async Task GuestSubmit_AcceptsSixScores_AndKeepsOneAnonymousSession()
    {
        using var factory = new ReviewWebFactory();
        using var client = Client(factory);
        var antiforgery = await AntiforgeryTokenAsync(client);

        using var first = await PostScoresAsync(client, antiforgery, Scores());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("Cảm ơn bạn đã đánh giá", WebUtility.HtmlDecode(await first.Content.ReadAsStringAsync()));
        Assert.Equal(1, factory.Api.SubmitCalls);
        var firstSession = factory.Api.SessionId;
        Assert.False(string.IsNullOrWhiteSpace(firstSession));
        Assert.Equal(32, firstSession!.Length);
        Assert.Equal<int?>(5, factory.Api.SubmittedScores!.HallScore);
        Assert.Equal<int?>(4, factory.Api.SubmittedScores.FoodScore);
        Assert.Equal<int?>(5, factory.Api.SubmittedScores.HygieneScore);
        Assert.Equal("Buổi tiệc chu đáo.", factory.Api.SubmittedScores.Comment);

        var nextAntiforgery = await AntiforgeryTokenAsync(client);
        using var second = await PostScoresAsync(client, nextAntiforgery, Scores());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstSession, factory.Api.SessionId);
    }

    [Fact]
    public async Task GuestSubmit_MissingCriterion_StopsBeforeCallingApi()
    {
        using var factory = new ReviewWebFactory();
        using var client = Client(factory);
        var antiforgery = await AntiforgeryTokenAsync(client);
        var scores = Scores();
        scores.Remove("HygieneScore");

        using var response = await PostScoresAsync(client, antiforgery, scores);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Vui lòng chấm đủ 6 tiêu chí từ 1 đến 5.", WebUtility.HtmlDecode(html));
        Assert.Equal(0, factory.Api.SubmitCalls);
    }

    [Fact]
    public async Task GuestSubmit_Duplicate_ShowsRecordedState()
    {
        using var factory = new ReviewWebFactory { Duplicate = true };
        using var client = Client(factory);
        var antiforgery = await AntiforgeryTokenAsync(client);

        using var response = await PostScoresAsync(client, antiforgery, Scores());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Đánh giá đã được ghi nhận", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void QrRoutes_AreScopedToCoordinatorAndManagementRoles()
    {
        AssertRoles(nameof(WeddingReviewsController.IssueForCoordinator), RoleNames.Coordinator);
        AssertRoles(nameof(WeddingReviewsController.CoordinatorQrImage), RoleNames.Coordinator);
        AssertRoles(nameof(WeddingReviewsController.ManagementQrImage),
            RoleNames.Manager + "," + RoleNames.HallManager);

        Assert.Empty(typeof(WeddingReviewsController)
            .GetMethod(nameof(WeddingReviewsController.Guest), [typeof(string), typeof(CancellationToken)])!
            .GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public async Task ManagementQrImage_StreamsApiPng()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, ReviewApiProxy>();
        var proxy = (ReviewApiProxy)(object)api;
        var controller = new WeddingReviewsController(api, NullLogger<WeddingReviewsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Cookie = "rp_api_token=test-token";

        var result = Assert.IsType<FileContentResult>(
            await controller.ManagementQrImage(7, CancellationToken.None));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(proxy.Png, result.FileContents);
    }

    [Fact]
    public async Task SubmitGuestReviewAsync_ForwardsSessionHeaderAndBodyConsistently()
    {
        var sessionId = new string('a', 32);
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var api = new RiversideApiClient(
            httpClient,
            new ProjectImageCatalog(new ConfigurationBuilder().Build(), new TestWebHostEnvironment()),
            Microsoft.Extensions.Options.Options.Create(new RecommendationOptions()));

        var scores = new GuestReviewScoresViewModel
        {
            HallScore = 5,
            FoodScore = 4,
            ServiceScore = 5,
            SoundScore = 3,
            LightingScore = 4,
            HygieneScore = 5,
            Comment = "Buổi tiệc chu đáo."
        };

        var result = await api.SubmitGuestReviewAsync("token-abc", scores, sessionId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(handler.Request);
        Assert.True(handler.Request!.Headers.TryGetValues("X-Review-Session", out var headerValues));
        Assert.Equal(sessionId, Assert.Single(headerValues));

        using var body = JsonDocument.Parse(handler.RequestBody ?? "{}");
        var root = body.RootElement;
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(5, root.GetProperty("hallScore").GetInt32());
        Assert.Equal(4, root.GetProperty("foodScore").GetInt32());
        Assert.Equal(5, root.GetProperty("serviceScore").GetInt32());
        Assert.Equal(3, root.GetProperty("soundScore").GetInt32());
        Assert.Equal(4, root.GetProperty("lightingScore").GetInt32());
        Assert.Equal(5, root.GetProperty("hygieneScore").GetInt32());
        Assert.Equal("Buổi tiệc chu đáo.", root.GetProperty("comment").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"duplicate\":false,\"message\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Production";
    }

    private static void AssertRoles(string methodName, string expected)
    {
        var attribute = typeof(WeddingReviewsController).GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(expected, attribute!.Roles);
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync($"/danh-gia/khach-moi/{Token}");
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "Guest form should render its anti-forgery token.");
        return match.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostScoresAsync(
        HttpClient client, string antiforgery, Dictionary<string, string> scores)
    {
        scores["__RequestVerificationToken"] = antiforgery;
        return client.PostAsync($"/danh-gia/khach-moi/{Token}", new FormUrlEncodedContent(scores));
    }

    private static Dictionary<string, string> Scores() => new()
    {
        ["HallScore"] = "5",
        ["FoodScore"] = "4",
        ["ServiceScore"] = "5",
        ["SoundScore"] = "3",
        ["LightingScore"] = "4",
        ["HygieneScore"] = "5",
        ["Comment"] = "Buổi tiệc chu đáo."
    };

    public class ReviewApiProxy : DispatchProxy
    {
        public int FormCalls { get; private set; }
        public int SubmitCalls { get; private set; }
        public GuestReviewScoresViewModel? SubmittedScores { get; private set; }
        public string? SessionId { get; private set; }
        public bool FormFound { get; set; } = true;
        public bool Duplicate { get; set; }
        public byte[] Png { get; } = [0x89, 0x50, 0x4E, 0x47];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IRiversideApiClient.GetGuestReviewAsync):
                    FormCalls++;
                    return Task.FromResult(FormFound
                        ? ApiCallResult<GuestReviewFormViewModel>.Success(Form())
                        : ApiCallResult<GuestReviewFormViewModel>.Failure("Mã đánh giá không tồn tại.", 404));
                case nameof(IRiversideApiClient.SubmitGuestReviewAsync):
                    SubmitCalls++;
                    SubmittedScores = (GuestReviewScoresViewModel)args![1]!;
                    SessionId = (string)args[2]!;
                    return Task.FromResult(ApiCallResult<GuestReviewSubmitResult>.Success(new GuestReviewSubmitResult
                    {
                        Success = true,
                        Duplicate = Duplicate,
                        Message = "Cảm ơn bạn đã đánh giá buổi tiệc!"
                    }));
                case nameof(IRiversideApiClient.GetManagementReviewQrPngAsync):
                    return Task.FromResult(ApiCallResult<byte[]>.Success(Png));
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }

        private static GuestReviewFormViewModel Form() => new()
        {
            BookingId = 7,
            BookingCode = "DT-20261111-ABCD",
            HallName = "Sảnh Riverside",
            EventDate = new DateTime(2026, 11, 12),
            Shift = "Ca tối",
            State = "Active",
            CanSubmit = true,
            OpensAt = new DateTime(2026, 11, 11, 8, 0, 0),
            ExpiresAt = new DateTime(2026, 11, 13, 22, 0, 0),
            Message = "Hãy chia sẻ trải nghiệm của bạn."
        };
    }
}

public sealed class ReviewWebFactory : WebApplicationFactory<WeddingReviewsController>
{
    public ReviewQrWebTests.ReviewApiProxy Api { get; } =
        (ReviewQrWebTests.ReviewApiProxy)(object)DispatchProxy.Create<IRiversideApiClient, ReviewQrWebTests.ReviewApiProxy>();

    public bool FormFound
    {
        get => Api.FormFound;
        set => Api.FormFound = value;
    }

    public bool Duplicate
    {
        get => Api.Duplicate;
        set => Api.Duplicate = value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(RepositoryRoot());
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiversideApiClient>();
            services.AddSingleton<IRiversideApiClient>(_ => (IRiversideApiClient)(object)Api);
        });
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "WeddingManager.sln")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Repository root not found.");
        return Path.Combine(directory.FullName, "Frontend", "HeThongDatTiecCuoi_WEB");
    }
}

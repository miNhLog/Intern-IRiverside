using System.Reflection;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

/// <summary>
/// UC11 – validation phía WEB MVC và dữ liệu chuyển sang API.
/// Không khởi động app, không đụng DB: API client được thay bằng DispatchProxy.
/// </summary>
public sealed class Uc11RecommendationWebTests
{
    [Fact]
    public void SurveyView_RejectsSpareTableBadInput()
    {
        var view = File.ReadAllText(Path.Combine(RepositoryRoot(), "Frontend", "HeThongDatTiecCuoi_WEB", "Views", "Recommendation", "Index.cshtml"));
        Assert.Contains("spareInput.validity.badInput", view);
    }

    [Fact]
    public void SurveyView_UsesCustomValidationAndPreservesContactHandoff()
    {
        var view = File.ReadAllText(Path.Combine(RepositoryRoot(), "Frontend", "HeThongDatTiecCuoi_WEB", "Views", "Recommendation", "Index.cshtml"));
        Assert.Contains("id=\"clientRecomForm\" novalidate", view);
        Assert.Contains("max=\"2000\" step=\"1\"", view);
        Assert.Contains("input.setAttribute('aria-invalid', 'true')", view);
        Assert.Contains("input.removeAttribute('aria-invalid')", view);
        Assert.Contains("focusField(failure.field)", view);
        Assert.Contains("input.addEventListener(eventName, () => clearFieldError(fieldId))", view);
        Assert.Contains("currentPayload.email = latestContact.email", view);
        Assert.Contains("currentPayload.phoneNumber = latestContact.phoneNumber", view);
        Assert.Contains("currentPayload.coupleName = latestContact.coupleName", view);
    }

    [Fact]
    public void AiAutofill_UnsupportedStyleDoesNotSelectFallback()
    {
        var js = File.ReadAllText(Path.Combine(RepositoryRoot(), "Frontend", "HeThongDatTiecCuoi_WEB", "wwwroot", "js", "ai-chat.js"));
        var start = js.IndexOf("function mapStyle(v)", StringComparison.Ordinal);
        var end = js.IndexOf("function fillSurvey(e)", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var mapping = js[start..end];
        Assert.DoesNotContain("'Hoàng gia'", mapping);
        Assert.Contains("return null;", mapping);
        Assert.DoesNotContain("return 'Hiện đại / lãng mạn'", mapping);
        Assert.Contains("if (style && mapped) { style.value = mapped; }", js);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "WeddingManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [Fact]
    public async Task MvcPipeline_InvalidPhone_MapsFieldAndDoesNotCallApi()
    {
        using var factory = new Uc11WebFactory();
        using var client = factory.CreateClient();
        var model = ValidModel();
        model.PhoneNumber = "123";
        using var response = await client.PostAsJsonAsync("/Recommendation/AnalyzeTop3", model);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("phoneNumber", json.RootElement.GetProperty("field").GetString());
        Assert.Equal(0, factory.Proxy.Calls);
    }

    [Fact]
    public async Task MvcPipeline_InvalidJsonNumber_ReturnsSafeGeneralErrorWithoutCallingApi()
    {
        using var factory = new Uc11WebFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"guestCount\":\"not-a-number\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Recommendation/AnalyzeTop3", content);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
        Assert.Equal(0, factory.Proxy.Calls);
    }

    [Fact]
    public async Task AnalyzeTop3_151Guests_RoundsUpTo16Tables()
    {
        var (api, proxy) = CreateApi();
        var model = ValidModel();
        model.GuestCount = 151;
        model.SpareTableCount = 0;
        await CreateController(api).AnalyzeTop3(model);
        Assert.Equal(1, proxy.Calls);
        Assert.Equal(16, proxy.CapturedRequest!.OfficialTableCount);
        Assert.Equal(16, proxy.CapturedRequest.TableCount);
    }

    [Fact]
    public async Task AnalyzeTop3_InvalidPhone_ReturnsPhoneFieldError()
    {
        var (apiClient, proxy) = CreateApi();
        var controller = CreateController(apiClient);
        var model = ValidModel();
        model.PhoneNumber = "123";

        var result = await controller.AnalyzeTop3(model);

        Assert.False(SuccessOf(result));
        Assert.Equal("phoneNumber", FieldOf(result));
        Assert.Equal(0, proxy.Calls);
    }

    [Fact]
    public async Task AnalyzeTop3_ValidRequest_DerivesTablesFromGuestCountAndSpare()
    {
        var (apiClient, proxy) = CreateApi();
        var controller = CreateController(apiClient);
        var model = ValidModel();
        model.GuestCount = 300;
        model.OfficialTableCount = 1;   // client khai sai, server phải bỏ qua
        model.SpareTableCount = 2;
        model.TableCount = 1;

        await controller.AnalyzeTop3(model);

        Assert.Equal(1, proxy.Calls);
        Assert.NotNull(proxy.CapturedRequest);
        Assert.Equal(30, proxy.CapturedRequest!.OfficialTableCount);
        Assert.Equal(2, proxy.CapturedRequest.SpareTableCount);
        Assert.Equal(32, proxy.CapturedRequest.TableCount);
    }

    [Fact]
    public async Task AnalyzeTop3_SpareTablesExceedLargestHall_ReturnsSpareFieldError()
    {
        var (apiClient, proxy) = CreateApi();
        var controller = CreateController(apiClient);
        var model = ValidModel();
        model.GuestCount = 2000; // 200 bàn chính thức
        model.SpareTableCount = 1;

        var result = await controller.AnalyzeTop3(model);

        Assert.False(SuccessOf(result));
        Assert.Equal("spareTableCount", FieldOf(result));
        Assert.Equal(0, proxy.Calls);
    }

    [Fact]
    public async Task AnalyzeTop3_ZeroCombos_IsSuccessNotValidationError()
    {
        var (apiClient, proxy) = CreateApi();
        proxy.ReturnCombos = false;
        var controller = CreateController(apiClient);

        var result = await controller.AnalyzeTop3(ValidModel());

        Assert.True(SuccessOf(result));
        var data = DataOf(result) as RecommendationResponseViewModel;
        Assert.NotNull(data);
        Assert.Empty(data!.TopCombos);
    }

    private static RecommendationRequestViewModel ValidModel() => new()
    {
        CoupleName = "Trần Hoàng & Mai Phương",
        PhoneNumber = "0912345678",
        Email = "wedding@example.invalid",
        EventDate = DateTime.Today.AddMonths(2),
        Shift = "Ca tối",
        GuestCount = 300,
        SpareTableCount = 2,
        ExpectedBudget = 250_000_000m,
        DesiredStyle = "Hiện đại / lãng mạn"
    };

    private static (IRiversideApiClient Api, Uc11ApiProxy Proxy) CreateApi()
    {
        var proxy = DispatchProxy.Create<IRiversideApiClient, Uc11ApiProxy>();
        return ((IRiversideApiClient)proxy, (Uc11ApiProxy)(object)proxy);
    }

    private static RecommendationController CreateController(IRiversideApiClient api)
    {
        var services = new ServiceCollection().AddLogging().AddControllersWithViews().Services.BuildServiceProvider();
        return new RecommendationController(api, NullLogger<RecommendationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
    }

    private static JsonResult AsJson(IActionResult result) => Assert.IsType<JsonResult>(result);

    private static object? Property(IActionResult result, string name) =>
        AsJson(result).Value?.GetType().GetProperty(name)?.GetValue(AsJson(result).Value);

    private static bool SuccessOf(IActionResult result) => Property(result, "success") is true;
    private static string? FieldOf(IActionResult result) => Property(result, "field") as string;
    private static object? DataOf(IActionResult result) => Property(result, "data");

    public class Uc11ApiProxy : DispatchProxy
    {
        public int Calls { get; private set; }
        public RecommendationRequestViewModel? CapturedRequest { get; private set; }
        public bool ReturnCombos { get; set; } = true;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRiversideApiClient.GetTop3RecommendationsAsync))
                throw new NotSupportedException(targetMethod?.Name);

            Calls++;
            CapturedRequest = (RecommendationRequestViewModel)args![0]!;
            var response = new RecommendationResponseViewModel
            {
                AlgorithmNotice = ReturnCombos ? "ok" : "no combo",
                TopCombos = ReturnCombos
                    ? new List<ComboSuggestionViewModel>
                    {
                        new() { Rank = 1, ComboTitle = "Combo 1", HallName = "Grand Ballroom", TotalEstimatedCost = 150_000_000m }
                    }
                    : new List<ComboSuggestionViewModel>()
            };
            response.Count = response.TopCombos.Count;
            return Task.FromResult(ApiCallResult<RecommendationResponseViewModel>.Success(response));
        }
    }
}

public sealed class Uc11WebFactory : WebApplicationFactory<RecommendationController>
{
    private readonly IRiversideApiClient _api = DispatchProxy.Create<IRiversideApiClient, Uc11RecommendationWebTests.Uc11ApiProxy>();
    public Uc11RecommendationWebTests.Uc11ApiProxy Proxy => (Uc11RecommendationWebTests.Uc11ApiProxy)(object)_api;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiversideApiClient>();
            services.AddSingleton(_api);
        });
    }
}

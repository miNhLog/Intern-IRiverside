using System.Reflection;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class PublicBookingLookupWebTests
{
    [Fact]
    public void PublicFormGet_DoesNotLookupFromQueryOrSetProofCookies()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, LookupApiProxy>();
        var controller = CreateController(api);
        controller.HttpContext.Request.QueryString = new QueryString("?bookingCode=DT-20261022-ABCD1234&phone=0901234567");

        var result = Assert.IsType<ViewResult>(controller.Index());

        Assert.IsType<PublicBookingLookupInputViewModel>(result.Model);
        Assert.Equal("no-store, no-cache, max-age=0", controller.Response.Headers.CacheControl);
        Assert.False(controller.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Equal(0, ((LookupApiProxy)(object)api).LookupCalls);
    }

    [Fact]
    public async Task PublicLookupPost_ForwardsNormalizedProofWithoutLogin()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, LookupApiProxy>();
        var controller = CreateController(api);
        var input = new PublicBookingLookupInputViewModel
        {
            BookingCode = " dt-20261022-abcd1234 ",
            PhoneNumber = " 0901234567 "
        };

        var result = Assert.IsType<ViewResult>(await controller.Lookup(input, default));

        Assert.Equal("DT-20261022-ABCD1234", ((LookupApiProxy)(object)api).BookingCode);
        Assert.Equal("0901234567", ((LookupApiProxy)(object)api).PhoneNumber);
        Assert.Equal("no-store, no-cache, max-age=0", controller.Response.Headers.CacheControl);
        Assert.False(controller.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Equal("Index", result.ViewName);
    }

    [Fact]
    public async Task MvcPipeline_AcceptsAndNormalizesLowercaseBookingCodePrefix()
    {
        using var factory = new PublicBookingLookupWebFactory();
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/MyBooking");
        var html = await get.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(html,
            "name=\\\"__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"([^\\\"]+)\\\"");
        Assert.True(token.Success, "MVC form should render its anti-forgery token.");

        using var post = await client.PostAsync("/MyBooking/Lookup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BookingCode"] = "dt-20261022-abcd1234",
            ["PhoneNumber"] = "0901234567",
            ["__RequestVerificationToken"] = token.Groups[1].Value
        }));

        Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
        Assert.Contains("Thông tin tiệc của bạn", await post.Content.ReadAsStringAsync());
        Assert.Equal(1, factory.Api.LookupCalls);
        Assert.Equal("DT-20261022-ABCD1234", factory.Api.BookingCode);
    }

    private static MyBookingController CreateController(IRiversideApiClient api)
    {
        var services = new ServiceCollection().AddLogging().AddControllersWithViews().Services.BuildServiceProvider();
        var controller = new MyBookingController(api, NullLogger<MyBookingController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
        return controller;
    }

    public class LookupApiProxy : DispatchProxy
    {
        public int LookupCalls { get; private set; }
        public string? BookingCode { get; private set; }
        public string? PhoneNumber { get; private set; }
        public bool ReturnSuccessfulLookup { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRiversideApiClient.LookupPublicBookingAsync))
                throw new NotSupportedException(targetMethod?.Name);

            LookupCalls++;
            BookingCode = (string)args![0]!;
            PhoneNumber = (string)args[1]!;
            var result = ReturnSuccessfulLookup
                ? ApiCallResult<PublicBookingDetailViewModel>.Success(new PublicBookingDetailViewModel
                {
                    BookingCode = BookingCode,
                    CustomerName = "Test Customer",
                    HallName = "Test Hall",
                    EventDate = new DateTime(2027, 1, 15),
                    Shift = "Ca trưa",
                    GuestCount = 100,
                    StatusCode = "CONFIRMED",
                    StatusName = "Đã xác nhận"
                })
                : ApiCallResult<PublicBookingDetailViewModel>.Failure("not found", 404);
            return Task.FromResult(result);
        }
    }
}

public sealed class PublicBookingLookupWebFactory : WebApplicationFactory<MyBookingController>
{
    public PublicBookingLookupWebTests.LookupApiProxy Api { get; } =
        (PublicBookingLookupWebTests.LookupApiProxy)(object)DispatchProxy.Create<IRiversideApiClient, PublicBookingLookupWebTests.LookupApiProxy>();

    public PublicBookingLookupWebFactory() => Api.ReturnSuccessfulLookup = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiversideApiClient>();
            services.AddSingleton<IRiversideApiClient>(_ => (IRiversideApiClient)(object)Api);
        });
    }
}

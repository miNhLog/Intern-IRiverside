using System.Reflection;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Models.ScheduleChange;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class PublicScheduleChangeWebTests
{
    [Fact]
    public async Task RescheduleEligibility_Post_ForwardsNormalizedProofAndReturnsEligibility()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, ScheduleChangeApiProxy>();
        var proxy = (ScheduleChangeApiProxy)(object)api;
        proxy.EligibilityResult = ApiCallResult<ScheduleChangeEligibilityViewModel>.Success(new ScheduleChangeEligibilityViewModel
        {
            BookingId = 7,
            BookingCode = "DT-20270115-ABCD",
            CanRequestOnline = true,
            MinimumNewDate = new DateTime(2027, 1, 20),
            CurrentHallId = 3,
            CurrentHallName = "Sảnh Ruby",
            ScheduleChangeFee = 100000
        });
        var controller = CreateController(api);

        var result = Assert.IsType<JsonResult>(await controller.RescheduleEligibility(" dt-20270115-abcd ", " 0901234567 ", default));

        Assert.Equal("DT-20270115-ABCD", proxy.BookingCode);
        Assert.Equal("0901234567", proxy.PhoneNumber);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"canRequestOnline\":true", json);
        Assert.Contains("\"currentHallId\":3", json);
    }

    [Fact]
    public async Task SendVerifyAndCreateReschedule_UseExistingEndpointsAndKeepProofOutOfQuery()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, ScheduleChangeApiProxy>();
        var proxy = (ScheduleChangeApiProxy)(object)api;
        proxy.SendOtpResult = ApiCallResult<string>.Success("sent");
        proxy.VerifyOtpResult = ApiCallResult<OtpVerificationViewModel>.Success(new OtpVerificationViewModel
        {
            VerificationToken = "token-123",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        proxy.CreateResult = ApiCallResult<ScheduleChangeViewModel>.Success(new ScheduleChangeViewModel
        {
            ScheduleChangeId = 55,
            Status = "PENDING",
            StatusName = "Chờ tiếp nhận"
        });
        var controller = CreateController(api);

        var send = Assert.IsType<JsonResult>(await controller.SendRescheduleOtp("DT-20270115-ABCD", "0901234567", default));
        Assert.Equal("SCHEDULE_CHANGE_CREATE", proxy.OtpPurpose);
        Assert.Null(proxy.ScheduleChangeId);
        var sendJson = System.Text.Json.JsonSerializer.Serialize(send.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("gửi tới email", sendJson);
        Assert.DoesNotContain("SMS", sendJson);

        var verify = Assert.IsType<JsonResult>(await controller.VerifyRescheduleOtp("DT-20270115-ABCD", "0901234567", "123456", default));
        var verifyJson = System.Text.Json.JsonSerializer.Serialize(verify.Value);
        Assert.Contains("\"verificationToken\":\"token-123\"", verifyJson);

        var create = Assert.IsType<JsonResult>(await controller.CreateRescheduleRequest(
            "DT-20270115-ABCD", "0901234567", "token-123", 3, "2027-02-01", "Ca tối", "Đổi lịch do gia đình", default));
        Assert.NotNull(proxy.CreateInput);
        Assert.Equal("DT-20270115-ABCD", proxy.CreateInput!.BookingCode);
        Assert.Equal("0901234567", proxy.CreateInput.PhoneNumber);
        Assert.Equal("token-123", proxy.CreateInput.VerificationToken);
        Assert.Equal(3, proxy.CreateInput.NewHallId);
        Assert.Equal(new DateTime(2027, 2, 1), proxy.CreateInput.NewDate);
        Assert.Equal("Ca tối", proxy.CreateInput.NewShift);
        var createJson = System.Text.Json.JsonSerializer.Serialize(create.Value);
        Assert.Contains("\"success\":true", createJson);
        Assert.Contains("\"scheduleChangeId\":55", createJson);
    }

    [Fact]
    public async Task MvcPipeline_RescheduleEndpoints_RequireAntiForgeryToken()
    {
        using var factory = new PublicScheduleChangeWebFactory();
        using var client = factory.CreateClient();

        async Task AssertRejected(string path, Dictionary<string, string> form)
        {
            using var post = await client.PostAsync(path, new FormUrlEncodedContent(form));
            Assert.Equal(StatusCodes.Status400BadRequest, (int)post.StatusCode);
        }

        var proof = new Dictionary<string, string> { ["bookingCode"] = "DT-20270115-ABCD", ["phoneNumber"] = "0901234567" };
        await AssertRejected("/MyBooking/RescheduleEligibility", proof);
        await AssertRejected("/MyBooking/RescheduleAvailability", new Dictionary<string, string>(proof)
        {
            ["date"] = "2027-02-01",
            ["shift"] = "Ca trưa"
        });
        await AssertRejected("/MyBooking/SendRescheduleOtp", proof);
        await AssertRejected("/MyBooking/VerifyRescheduleOtp", new Dictionary<string, string>(proof) { ["otpCode"] = "123456" });
        await AssertRejected("/MyBooking/CreateRescheduleRequest", new Dictionary<string, string>(proof)
        {
            ["verificationToken"] = "token",
            ["newHallId"] = "3",
            ["newDate"] = "2027-02-01",
            ["newShift"] = "Ca tối",
            ["reason"] = "Lý do"
        });

        Assert.Equal(0, factory.Api.EligibilityCalls);
        Assert.Equal(0, factory.Api.SendOtpCalls);
        Assert.Equal(0, factory.Api.VerifyOtpCalls);
        Assert.Equal(0, factory.Api.CreateCalls);
    }

    [Fact]
    public async Task MvcPipeline_RescheduleEligibility_WithAntiForgeryToken_Succeeds()
    {
        using var factory = new PublicScheduleChangeWebFactory();
        factory.Api.EligibilityResult = ApiCallResult<ScheduleChangeEligibilityViewModel>.Success(new ScheduleChangeEligibilityViewModel
        {
            CanRequestOnline = true,
            MinimumNewDate = new DateTime(2027, 1, 20),
            CurrentHallId = 3
        });
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/MyBooking");
        var html = await get.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(html,
            "name=\\\"__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"([^\\\"]+)\\\"");
        Assert.True(token.Success);

        using var post = await client.PostAsync("/MyBooking/RescheduleEligibility", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingCode"] = "dt-20270115-abcd",
            ["phoneNumber"] = "0901234567",
            ["__RequestVerificationToken"] = token.Groups[1].Value
        }));

        Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body);
        Assert.Contains("\"canRequestOnline\":true", body);
        Assert.Equal(1, factory.Api.EligibilityCalls);
        Assert.Equal("DT-20270115-ABCD", factory.Api.BookingCode);
    }

    [Fact]
    public async Task LookupSendVerifyAndAcceptQuote_UseExistingMethodsAndReturnAcceptedResult()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, ScheduleChangeApiProxy>();
        var proxy = (ScheduleChangeApiProxy)(object)api;
        proxy.LookupResult = ApiCallResult<List<ScheduleChangeViewModel>>.Success(new List<ScheduleChangeViewModel>
        {
            new()
            {
                ScheduleChangeId = 55,
                Status = "AWAITING_QUOTE_ACCEPTANCE",
                StatusName = "Chờ xác nhận báo giá",
                Quote = new ScheduleChangeQuoteViewModel { QuoteId = 77, AmountDue = 150000 }
            }
        });
        proxy.SendOtpResult = ApiCallResult<string>.Success("sent");
        proxy.VerifyOtpResult = ApiCallResult<OtpVerificationViewModel>.Success(new OtpVerificationViewModel
        {
            VerificationToken = "quote-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        proxy.AcceptQuoteResult = ApiCallResult<QuoteAcceptanceResultViewModel>.Success(new QuoteAcceptanceResultViewModel
        {
            ScheduleChange = new ScheduleChangeViewModel
            {
                ScheduleChangeId = 55,
                Status = "APPROVED",
                StatusName = "Đã thay đổi lịch thành công"
            },
            Payment = new ScheduleChangePaymentViewModel
            {
                PaymentStatus = "PAID",
                Status = "APPROVED",
                BusinessAmount = 150000,
                Message = "Đã thanh toán"
            }
        });
        var controller = CreateController(api);

        var lookup = Assert.IsType<JsonResult>(await controller.RescheduleRequests(" dt-20270115-abcd ", "0901234567", default));
        Assert.Equal(1, proxy.LookupCalls);
        Assert.Equal("DT-20270115-ABCD", proxy.BookingCode);
        var lookupJson = System.Text.Json.JsonSerializer.Serialize(lookup.Value);
        Assert.Contains("\"scheduleChangeId\":55", lookupJson);
        Assert.Contains("\"quoteId\":77", lookupJson);

        var send = Assert.IsType<JsonResult>(await controller.SendRescheduleQuoteOtp("DT-20270115-ABCD", "0901234567", 55, default));
        Assert.Equal("SCHEDULE_CHANGE_ACCEPT_QUOTE", proxy.OtpPurpose);
        Assert.Equal(55, proxy.ScheduleChangeId);
        var sendJson = System.Text.Json.JsonSerializer.Serialize(send.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("gửi tới email", sendJson);

        var verify = Assert.IsType<JsonResult>(await controller.VerifyRescheduleQuoteOtp("DT-20270115-ABCD", "0901234567", 55, "123456", default));
        var verifyJson = System.Text.Json.JsonSerializer.Serialize(verify.Value);
        Assert.Contains("\"verificationToken\":\"quote-token\"", verifyJson);
        Assert.Equal(55, proxy.ScheduleChangeId);

        var accept = Assert.IsType<JsonResult>(await controller.AcceptRescheduleQuote("DT-20270115-ABCD", "0901234567", 55, 77, "quote-token", default));
        Assert.Equal(1, proxy.AcceptQuoteCalls);
        Assert.Equal(55, proxy.AcceptedScheduleChangeId);
        Assert.Equal(77, proxy.AcceptedQuoteId);
        Assert.Equal("quote-token", proxy.AcceptedVerificationToken);
        var acceptJson = System.Text.Json.JsonSerializer.Serialize(accept.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("\"success\":true", acceptJson);
        Assert.Contains("\"status\":\"APPROVED\"", acceptJson);
        Assert.Contains("\"paymentStatus\":\"PAID\"", acceptJson);
    }

    [Fact]
    public async Task MvcPipeline_QuoteEndpoints_RequireAntiForgeryTokenAndWithToken_Succeeds()
    {
        using var factory = new PublicScheduleChangeWebFactory();
        factory.Api.LookupResult = ApiCallResult<List<ScheduleChangeViewModel>>.Success(new List<ScheduleChangeViewModel>
        {
            new() { ScheduleChangeId = 55, Status = "AWAITING_QUOTE_ACCEPTANCE", Quote = new ScheduleChangeQuoteViewModel { QuoteId = 77 } }
        });
        factory.Api.SendOtpResult = ApiCallResult<string>.Success("sent");
        factory.Api.VerifyOtpResult = ApiCallResult<OtpVerificationViewModel>.Success(new OtpVerificationViewModel { VerificationToken = "quote-token", ExpiresAt = DateTime.UtcNow.AddMinutes(5) });
        factory.Api.AcceptQuoteResult = ApiCallResult<QuoteAcceptanceResultViewModel>.Success(new QuoteAcceptanceResultViewModel
        {
            ScheduleChange = new ScheduleChangeViewModel { ScheduleChangeId = 55, Status = "APPROVED", StatusName = "Đã thay đổi lịch thành công" },
            Payment = new ScheduleChangePaymentViewModel { PaymentStatus = "PENDING", CheckoutUrl = "https://pay.example/checkout" }
        });
        using var client = factory.CreateClient();

        var proof = new Dictionary<string, string> { ["bookingCode"] = "DT-20270115-ABCD", ["phoneNumber"] = "0901234567" };
        async Task AssertRejected(string path, Dictionary<string, string> form)
        {
            using var post = await client.PostAsync(path, new FormUrlEncodedContent(form));
            Assert.Equal(StatusCodes.Status400BadRequest, (int)post.StatusCode);
        }
        await AssertRejected("/MyBooking/RescheduleRequests", proof);
        await AssertRejected("/MyBooking/SendRescheduleQuoteOtp", new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55" });
        await AssertRejected("/MyBooking/VerifyRescheduleQuoteOtp", new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55", ["otpCode"] = "123456" });
        await AssertRejected("/MyBooking/AcceptRescheduleQuote", new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55", ["quoteId"] = "77", ["verificationToken"] = "quote-token" });
        Assert.Equal(0, factory.Api.LookupCalls);
        Assert.Equal(0, factory.Api.AcceptQuoteCalls);

        using var get = await client.GetAsync("/MyBooking");
        var html = await get.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(html,
            "name=\\\"__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"([^\\\"]+)\\\"");
        Assert.True(token.Success);
        var t = token.Groups[1].Value;

        using (var post = await client.PostAsync("/MyBooking/RescheduleRequests", new FormUrlEncodedContent(new Dictionary<string, string>(proof) { ["__RequestVerificationToken"] = t })))
        {
            Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
            var body = await post.Content.ReadAsStringAsync();
            Assert.Contains("\"scheduleChangeId\":55", body);
        }
        using (var post = await client.PostAsync("/MyBooking/SendRescheduleQuoteOtp", new FormUrlEncodedContent(new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55", ["__RequestVerificationToken"] = t })))
        {
            Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
        }
        using (var post = await client.PostAsync("/MyBooking/VerifyRescheduleQuoteOtp", new FormUrlEncodedContent(new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55", ["otpCode"] = "123456", ["__RequestVerificationToken"] = t })))
        {
            Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
            var body = await post.Content.ReadAsStringAsync();
            Assert.Contains("\"verificationToken\":\"quote-token\"", body);
        }
        using (var post = await client.PostAsync("/MyBooking/AcceptRescheduleQuote", new FormUrlEncodedContent(new Dictionary<string, string>(proof) { ["scheduleChangeId"] = "55", ["quoteId"] = "77", ["verificationToken"] = "quote-token", ["__RequestVerificationToken"] = t })))
        {
            Assert.Equal(StatusCodes.Status200OK, (int)post.StatusCode);
            var body = await post.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":\"APPROVED\"", body);
            Assert.Contains("\"checkoutUrl\":\"https://pay.example/checkout\"", body);
        }

        Assert.Equal(1, factory.Api.LookupCalls);
        Assert.Equal(1, factory.Api.SendOtpCalls);
        Assert.Equal(1, factory.Api.VerifyOtpCalls);
        Assert.Equal(1, factory.Api.AcceptQuoteCalls);
    }

    private static MyBookingController CreateController(IRiversideApiClient api)
    {
        var services = new ServiceCollection().AddLogging().AddControllersWithViews().Services.BuildServiceProvider();
        return new MyBookingController(api, NullLogger<MyBookingController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
    }

    public class ScheduleChangeApiProxy : DispatchProxy
    {
        public int EligibilityCalls { get; private set; }
        public int AvailabilityCalls { get; private set; }
        public int SendOtpCalls { get; private set; }
        public int VerifyOtpCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int LookupCalls { get; private set; }
        public int AcceptQuoteCalls { get; private set; }
        public string? BookingCode { get; private set; }
        public string? PhoneNumber { get; private set; }
        public string? OtpPurpose { get; private set; }
        public int? ScheduleChangeId { get; private set; }
        public int? AcceptedScheduleChangeId { get; private set; }
        public int? AcceptedQuoteId { get; private set; }
        public string? AcceptedVerificationToken { get; private set; }
        public CreateScheduleChangeInput? CreateInput { get; private set; }

        public ApiCallResult<ScheduleChangeEligibilityViewModel> EligibilityResult { get; set; } =
            ApiCallResult<ScheduleChangeEligibilityViewModel>.Failure("not configured", 500);
        public ApiCallResult<List<ScheduleChangeAvailabilityViewModel>> AvailabilityResult { get; set; } =
            ApiCallResult<List<ScheduleChangeAvailabilityViewModel>>.Success(new List<ScheduleChangeAvailabilityViewModel>());
        public ApiCallResult<string> SendOtpResult { get; set; } = ApiCallResult<string>.Failure("not configured", 500);
        public ApiCallResult<OtpVerificationViewModel> VerifyOtpResult { get; set; } =
            ApiCallResult<OtpVerificationViewModel>.Failure("not configured", 500);
        public ApiCallResult<ScheduleChangeViewModel> CreateResult { get; set; } =
            ApiCallResult<ScheduleChangeViewModel>.Failure("not configured", 500);
        public ApiCallResult<List<ScheduleChangeViewModel>> LookupResult { get; set; } =
            ApiCallResult<List<ScheduleChangeViewModel>>.Failure("not configured", 500);
        public ApiCallResult<QuoteAcceptanceResultViewModel> AcceptQuoteResult { get; set; } =
            ApiCallResult<QuoteAcceptanceResultViewModel>.Failure("not configured", 500);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IRiversideApiClient.GetScheduleChangeEligibilityAsync):
                    EligibilityCalls++;
                    BookingCode = (string?)args![0];
                    PhoneNumber = (string?)args[1];
                    return Task.FromResult(EligibilityResult);
                case nameof(IRiversideApiClient.GetScheduleChangeAvailabilityAsync):
                    AvailabilityCalls++;
                    BookingCode = (string?)args![0];
                    PhoneNumber = (string?)args[1];
                    return Task.FromResult(AvailabilityResult);
                case nameof(IRiversideApiClient.SendScheduleChangeOtpAsync):
                    SendOtpCalls++;
                    BookingCode = (string?)args![0];
                    PhoneNumber = (string?)args[1];
                    OtpPurpose = (string?)args[2];
                    ScheduleChangeId = (int?)args[3];
                    return Task.FromResult(SendOtpResult);
                case nameof(IRiversideApiClient.VerifyScheduleChangeOtpAsync):
                    VerifyOtpCalls++;
                    BookingCode = (string?)args![0];
                    PhoneNumber = (string?)args[1];
                    OtpPurpose = (string?)args[2];
                    ScheduleChangeId = (int?)args[3];
                    return Task.FromResult(VerifyOtpResult);
                case nameof(IRiversideApiClient.CreateScheduleChangeAsync):
                    CreateCalls++;
                    CreateInput = (CreateScheduleChangeInput?)args![0];
                    return Task.FromResult(CreateResult);
                case nameof(IRiversideApiClient.LookupScheduleChangesAsync):
                    LookupCalls++;
                    BookingCode = (string?)args![0];
                    PhoneNumber = (string?)args[1];
                    return Task.FromResult(LookupResult);
                case nameof(IRiversideApiClient.AcceptScheduleChangeQuoteAsync):
                    AcceptQuoteCalls++;
                    AcceptedScheduleChangeId = (int?)args![0];
                    BookingCode = (string?)args[1];
                    PhoneNumber = (string?)args[2];
                    AcceptedQuoteId = (int?)args[3];
                    AcceptedVerificationToken = (string?)args[4];
                    return Task.FromResult(AcceptQuoteResult);
                case nameof(IRiversideApiClient.LookupPublicBookingAsync):
                    return Task.FromResult(ApiCallResult<PublicBookingDetailViewModel>.Failure("not used", 404));
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}

public sealed class PublicScheduleChangeWebFactory : WebApplicationFactory<MyBookingController>
{
    public PublicScheduleChangeWebTests.ScheduleChangeApiProxy Api { get; } =
        (PublicScheduleChangeWebTests.ScheduleChangeApiProxy)(object)DispatchProxy.Create<IRiversideApiClient, PublicScheduleChangeWebTests.ScheduleChangeApiProxy>();

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

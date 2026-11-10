using System.Security.Claims;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.DTOs.AiChat;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS.Models.Webhooks;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class HardeningSecurityTests
{
    private static PaymentController CreateFaultyPaymentController(string exceptionMessage)
    {
        var stubService = new FaultyPaymentService(exceptionMessage);
        var logger = NullLogger<PaymentController>.Instance;

        var controller = new PaymentController(stubService, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.TraceIdentifier = "TEST-TRACE-ID-12345";
        return controller;
    }

    [Fact]
    public async Task CreateDeposit_OnException_HidesExceptionMessageAndIncludesTraceId()
    {
        const string secretMessage = "SQL Syntax Error near Table DatTiec WHERE Password=xyz";
        var controller = CreateFaultyPaymentController(secretMessage);

        var result = await controller.CreateDeposit(new CreateDepositPaymentRequestDto { BookingId = 10 });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(statusResult.Value);
        Assert.DoesNotContain(secretMessage, json);
        Assert.DoesNotContain("detail", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEST-TRACE-ID-12345", json);
    }

    [Fact]
    public async Task CreateContractPayment_OnException_HidesExceptionMessageAndIncludesTraceId()
    {
        const string secretMessage = "Internal Server Exception with DB details";
        var controller = CreateFaultyPaymentController(secretMessage);

        var result = await controller.CreateContractPayment(new CreateCustomerContractPaymentRequestDto { BookingId = 10, Amount = 5000000 });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(statusResult.Value);
        Assert.DoesNotContain(secretMessage, json);
        Assert.DoesNotContain("detail", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEST-TRACE-ID-12345", json);
    }

    [Fact]
    public async Task CheckStatus_OnException_HidesExceptionMessageAndIncludesTraceId()
    {
        const string secretMessage = "Database Deadlock Error at Connection Pool";
        var controller = CreateFaultyPaymentController(secretMessage);

        var result = await controller.CheckStatus(123456789);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(statusResult.Value);
        Assert.DoesNotContain(secretMessage, json);
        Assert.DoesNotContain("detail", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEST-TRACE-ID-12345", json);
    }

    [Fact]
    public async Task VerifyInvoice_OnException_HidesExceptionMessageAndIncludesTraceId()
    {
        const string secretMessage = "CryptographicException in SHA256RSA provider";
        var controller = CreateFaultyPaymentController(secretMessage);

        var result = await controller.VerifyInvoice("DT-20261028-0001", "0988776655");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);

        var json = System.Text.Json.JsonSerializer.Serialize(statusResult.Value);
        Assert.DoesNotContain(secretMessage, json);
        Assert.DoesNotContain("detail", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEST-TRACE-ID-12345", json);
    }

    [Fact]
    public void AiServiceOptions_DefaultValues_Have15SecondTimeoutAndLocalhost()
    {
        var options = new AiServiceOptions();
        Assert.Equal(15, options.TimeoutSeconds);
        Assert.Equal("http://127.0.0.1:8000/", options.BaseUrl);
    }

    [Fact]
    public async Task AiChatService_OnConnectionError_ReturnsFriendlyFallbackGracefully()
    {
        var stubFactory = new StubFailingHttpClientFactory();
        var options = Microsoft.Extensions.Options.Options.Create(new AiServiceOptions { TimeoutSeconds = 15 });
        var logger = NullLogger<AiChatService>.Instance;
        var service = new AiChatService(stubFactory, options, logger);

        var result = await service.SendChatAsync("Giá sảnh tiệc bao nhiêu?", new List<object>());

        Assert.NotNull(result);
        Assert.Equal(0, result.Confidence);
        Assert.Contains("Dạ trợ lý AI Riverside Palace hiện đang bận", result.Reply);
        Assert.Contains("Hotline 0933 777 888", result.Reply);
    }

    private sealed class StubFailingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            // Point to a non-listening port or use a handler that throws HttpRequestException
            var handler = new FailingHttpMessageHandler();
            return new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:9999/") };
        }
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused");
        }
    }

    private sealed class FaultyPaymentService : IPaymentService
    {
        private readonly string _exceptionMessage;

        public FaultyPaymentService(string exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        public Task<CreateDepositPaymentResponseDto> CreateDepositPaymentAsync(CreateDepositPaymentRequestDto request, int? userId = null) =>
            throw new Exception(_exceptionMessage);

        public Task<CreateContractPayOsLinkResponseDto> CreateCustomerContractPaymentAsync(CreateCustomerContractPaymentRequestDto request, int? userId = null) =>
            throw new Exception(_exceptionMessage);

        public Task<PaymentStatusResponseDto> GetPaymentStatusAsync(long orderCode) =>
            throw new Exception(_exceptionMessage);

        public Task<bool> ProcessWebhookAsync(Webhook webhookBody) =>
            throw new Exception(_exceptionMessage);

        public Task<(byte[] PdfBytes, string FileName, InvoiceSignatureResult Signature)> GetSignedInvoicePdfAsync(string bookingCode, string phone) =>
            throw new Exception(_exceptionMessage);

        public Task<CreateContractPayOsLinkResponseDto> CreateContractPaymentAsync(CreateContractPayOsLinkRequestDto request, int? managerUserId = null) =>
            throw new Exception(_exceptionMessage);

        public Task<PaymentStatusResponseDto> CheckContractPaymentStatusAsync(long orderCode) =>
            throw new Exception(_exceptionMessage);
    }
}

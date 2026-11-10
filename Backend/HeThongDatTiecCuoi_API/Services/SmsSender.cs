using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public interface ISmsSender
{
    Task<bool> SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}

public sealed class ConfigurableSmsSender : ISmsSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<SmsOptions> _options;
    private readonly IHostEnvironment _environment;

    public ConfigurableSmsSender(IHttpClientFactory httpClientFactory, IOptions<SmsOptions> options, IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _environment = environment;
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.Enabled) return false;

        if (string.Equals(options.Provider, "Development", StringComparison.OrdinalIgnoreCase))
            return _environment.IsDevelopment() && options.DevelopmentCode is { Length: 6 };

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.ApiKey))
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                to = phoneNumber,
                sender = options.SenderId,
                message = $"Ma xac thuc Riverside Palace cua ban la {code}. Ma co hieu luc 5 phut."
            })
        };
        request.Headers.TryAddWithoutValidation(options.ApiKeyHeader, options.ApiKey);
        using var response = await _httpClientFactory.CreateClient("SmsProvider").SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}


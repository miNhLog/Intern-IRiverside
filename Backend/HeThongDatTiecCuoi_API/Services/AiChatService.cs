using System.Net.Http.Json;
using System.Text.Json;
using HeThongDatTiecCuoi_API.DTOs.AiChat;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>
/// UC10: gọi AI_Service FastAPI (BaseUrl từ appsettings <c>AiService:BaseUrl</c>,
/// timeout 5s qua named HttpClient "AiService"). Mọi lỗi đều fallback thân thiện,
/// không để exception văng 500 rỗng ra client.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string FriendlyFallbackReply =
        "Dạ trợ lý AI Riverside Palace hiện đang bận, Quý khách vui lòng thử lại sau ít phút " +
        "hoặc gọi Hotline 0933 777 888 để chuyên viên hỗ trợ ngay ạ!";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiServiceOptions> options,
        ILogger<AiChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiChatReplyDto> SendChatAsync(
        string message, List<object> history, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var response = await client.PostAsJsonAsync(
                "api/v1/ai/rag/chat",
                new { message = message ?? string.Empty, history = history ?? new List<object>() },
                JsonOptions,
                cts.Token);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<AiChatReplyDto>(JsonOptions, cts.Token);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Reply))
            {
                _logger.LogWarning("AiChatService.SendChatAsync: AI trả rỗng, dùng fallback.");
                return new AiChatReplyDto { Reply = FriendlyFallbackReply, Confidence = 0 };
            }
            return dto;
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
        {
            _logger.LogWarning(ex, "AiChatService.SendChatAsync: AI lỗi ({BaseUrl}), fallback thân thiện.",
                _options.BaseUrl);
            return new AiChatReplyDto { Reply = FriendlyFallbackReply, Confidence = 0 };
        }
    }

    public async Task<AiChatEntitiesDto?> ExtractEntitiesAsync(
        List<object> history, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var response = await client.PostAsJsonAsync(
                "api/v1/ai/rag/extract-entities",
                new { history = history ?? new List<object>() },
                JsonOptions,
                cts.Token);
            response.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cts.Token), default, cts.Token);
            if (doc.RootElement.TryGetProperty("entities", out var entities))
            {
                return entities.Deserialize<AiChatEntitiesDto>(JsonOptions);
            }
            return doc.RootElement.Deserialize<AiChatEntitiesDto>(JsonOptions);
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
        {
            _logger.LogWarning(ex, "AiChatService.ExtractEntitiesAsync: AI lỗi ({BaseUrl}), trả null.",
                _options.BaseUrl);
            return null;
        }
    }
}

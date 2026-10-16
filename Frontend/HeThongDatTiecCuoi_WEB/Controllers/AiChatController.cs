using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.AiChat;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

/// <summary>UC10: proxy FE -&gt; BE cho Trợ lý ảo AI (chat RAG + trích xuất thực thể).</summary>
public class AiChatController : Controller
{
    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(IRiversideApiClient apiClient, ILogger<AiChatController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] AiChatSendViewModel request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Message))
                return Json(new { success = false, message = "Vui lòng nhập nội dung cần tư vấn." });
            request.History ??= new List<AiChatMessageViewModel>();
            var result = await _apiClient.SendAiChatAsync(request, cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                _logger.LogWarning("AiChat.Send: Backend trả lỗi: {Error}", result.Error);
                return Json(new { success = false, message = result.Error ?? "Trợ lý AI đang bận, vui lòng thử lại sau." });
            }
            return Json(new
            {
                success = true,
                reply = result.Value.Reply,
                sources = result.Value.Sources,
                confidence = result.Value.Confidence,
                isOutOfScope = result.Value.IsOutOfScope
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiChat.Send thất bại. traceId={TraceId}.", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Trợ lý AI đang bận, vui lòng thử lại sau.", traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Extract([FromBody] AiChatSendViewModel request, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = request?.History ?? new List<AiChatMessageViewModel>();
            var result = await _apiClient.ExtractAiEntitiesAsync(history, cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                _logger.LogWarning("AiChat.Extract: Backend trả lỗi: {Error}", result.Error);
                return Json(new { success = false, entities = new AiChatEntitiesViewModel() });
            }
            return Json(new { success = result.Value.Success, entities = result.Value.Entities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiChat.Extract thất bại. traceId={TraceId}.", HttpContext.TraceIdentifier);
            return Json(new { success = false, entities = new AiChatEntitiesViewModel(), traceId = HttpContext.TraceIdentifier });
        }
    }
}

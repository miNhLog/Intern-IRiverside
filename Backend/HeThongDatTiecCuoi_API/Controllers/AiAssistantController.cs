using HeThongDatTiecCuoi_API.DTOs.AiChat;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

/// <summary>UC10: Trợ lý ảo AI tư vấn thông minh (proxy FE/BE -&gt; AI_Service RAG).</summary>
[Route("api/v1/ai-chat")]
[ApiController]
public sealed class AiAssistantController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly ILogger<AiAssistantController> _logger;

    public AiAssistantController(IAiChatService aiChatService, ILogger<AiAssistantController> logger)
    {
        _aiChatService = aiChatService;
        _logger = logger;
    }

    /// <summary>Gửi tin nhắn cho trợ lý ảo RAG.</summary>
    [HttpPost("send")]
    [AllowAnonymous]
    public async Task<IActionResult> Send([FromBody] AiChatSendRequestDto? request)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { message = "Vui lòng nhập nội dung cần tư vấn." });
            var reply = await _aiChatService.SendChatAsync(
                request.Message.Trim(), request.History ?? new List<object>());
            return Ok(new
            {
                reply = reply.Reply,
                sources = reply.Sources,
                confidence = reply.Confidence,
                isOutOfScope = reply.IsOutOfScope
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiAssistant.Send lỗi ngoài dự kiến. traceId={TraceId}.",
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Trợ lý AI đang bận, vui lòng thử lại sau hoặc gọi Hotline 0933 777 888.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>Trích xuất tiêu chí (ngân sách/bàn/khách/ngày/ca/gu) từ hội thoại.</summary>
    [HttpPost("extract")]
    [AllowAnonymous]
    public async Task<IActionResult> Extract([FromBody] AiChatExtractRequestDto? request)
    {
        try
        {
            if (request is null)
                return BadRequest(new { message = "Thiếu lịch sử hội thoại." });
            var entities = await _aiChatService.ExtractEntitiesAsync(request.History ?? new List<object>());
            if (entities is null)
                return Ok(new { success = false, entities = new AiChatEntitiesDto() });
            return Ok(new { success = true, entities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiAssistant.Extract lỗi ngoài dự kiến. traceId={TraceId}.",
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể trích xuất thông tin lúc này, vui lòng thử lại.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }
}

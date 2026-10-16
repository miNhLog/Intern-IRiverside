using HeThongDatTiecCuoi_API.DTOs.AiChat;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>UC10: proxy Backend -&gt; AI_Service (FastAPI :8000) cho chat RAG và trích xuất thực thể.</summary>
public interface IAiChatService
{
    Task<AiChatReplyDto> SendChatAsync(string message, List<object> history, CancellationToken cancellationToken = default);
    Task<AiChatEntitiesDto?> ExtractEntitiesAsync(List<object> history, CancellationToken cancellationToken = default);
}

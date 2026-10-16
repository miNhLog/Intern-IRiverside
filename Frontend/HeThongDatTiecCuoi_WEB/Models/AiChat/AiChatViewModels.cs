using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_WEB.Models.AiChat;

/// <summary>UC10: ViewModels cho Trợ lý ảo AI (proxy FE -&gt; BE -&gt; AI_Service RAG).</summary>
public sealed class AiChatMessageViewModel
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class AiChatSendViewModel
{
    public string Message { get; set; } = string.Empty;
    public List<AiChatMessageViewModel> History { get; set; } = new();
}

public sealed class AiChatSourceViewModel
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("heading")]
    public string? Heading { get; set; }
}

public sealed class AiChatReplyViewModel
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<AiChatSourceViewModel> Sources { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("isOutOfScope")]
    public bool IsOutOfScope { get; set; }
}

public sealed class AiChatEntitiesViewModel
{
    [JsonPropertyName("ngan_sach_tran")]
    public long? NganSachTran { get; set; }

    [JsonPropertyName("so_ban_du_kien")]
    public int? SoBanDuKien { get; set; }

    [JsonPropertyName("so_khach_du_kien")]
    public int? SoKhachDuKien { get; set; }

    [JsonPropertyName("ngay_to_chuc")]
    public string? NgayToChuc { get; set; }

    [JsonPropertyName("ca_to_chuc")]
    public string? CaToChuc { get; set; }

    [JsonPropertyName("phong_cach")]
    public string? PhongCach { get; set; }
}

public sealed class AiChatExtractResultViewModel
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("entities")]
    public AiChatEntitiesViewModel Entities { get; set; } = new();
}

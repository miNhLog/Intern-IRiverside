using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_API.DTOs.AiChat;

/// <summary>UC10: Trợ lý ảo AI tư vấn thông minh (RAG + Entity Extraction).</summary>
public sealed class AiChatSendRequestDto
{
    public string Message { get; set; } = string.Empty;
    public List<object> History { get; set; } = new();
}

public sealed class AiChatExtractRequestDto
{
    public List<object> History { get; set; } = new();
}

public sealed class AiChatSourceDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("heading")]
    public string? Heading { get; set; }
}

public sealed class AiChatReplyDto
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<AiChatSourceDto> Sources { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("is_out_of_scope")]
    public bool IsOutOfScope { get; set; }
}

public sealed class AiChatEntitiesDto
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

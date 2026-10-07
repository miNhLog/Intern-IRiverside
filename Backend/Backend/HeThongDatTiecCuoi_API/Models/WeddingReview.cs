namespace HeThongDatTiecCuoi_API.Models;

public sealed class WeddingReview
{
    public int ReviewId { get; set; }
    public int ReviewQrCodeId { get; set; }
    public string ReviewerType { get; set; } = string.Empty;
    public int? HallScore { get; set; }
    public int? FoodScore { get; set; }
    public int? ServiceScore { get; set; }
    public int? SoundScore { get; set; }
    public int? LightingScore { get; set; }
    public int OverallScore { get; set; }
    public string? Comment { get; set; }
    public DateTime ReviewedAt { get; set; }

    public ReviewQrCode ReviewQrCode { get; set; } = null!;
}

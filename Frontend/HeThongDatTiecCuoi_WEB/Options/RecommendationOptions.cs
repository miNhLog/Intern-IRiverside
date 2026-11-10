namespace HeThongDatTiecCuoi_WEB.Options;

/// <summary>VĐ2: cờ chọn Frontend mặc định gọi suggest-top3-hybrid, giữ fallback về suggest-top3 (heuristic).
/// Tắt khẩn: UseHybridDefault=false là FE quay về heuristic cũ (&lt;5 phút), không đụng DB.</summary>
public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendation";

    public bool UseHybridDefault { get; set; } = true;

    public bool FallbackToHeuristic { get; set; } = true;

    public int HybridTimeoutSeconds { get; set; } = 5;
}

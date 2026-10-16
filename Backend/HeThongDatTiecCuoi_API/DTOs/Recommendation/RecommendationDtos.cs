using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_API.DTOs.Recommendation;

public sealed class RecommendationRequestDto
{
    [Range(typeof(decimal), "100000000", "3000000000", ErrorMessage = "Ngân sách tối thiểu 100 triệu (chi phí tối thiểu ~103,3 triệu cho 150 khách) đến 3 tỷ VNĐ.")]
    public decimal ExpectedBudget { get; set; }

    [Range(1, int.MaxValue)]
    public int GuestCount { get; set; }

    /// <summary>Số bàn chính thức quy đổi từ GuestCount (ceil(GuestCount/10)). Optional để tương thích JS cũ.</summary>
    [Range(0, 500)]
    public int OfficialTableCount { get; set; }

    /// <summary>Số bàn dự phòng. Optional.</summary>
    [Range(0, 50)]
    public int SpareTableCount { get; set; }

    /// <summary>Tổng bàn = Official + Spare. Optional, Backend tự tính lại nếu <= 0.</summary>
    [Range(0, 500)]
    public int TableCount { get; set; }

    public DateTime DesiredDate { get; set; }

    [Required, MaxLength(20)]
    public string DesiredShift { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DesiredStyle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ServiceNeeds { get; set; }
}

public sealed class ComboSuggestionDto
{
    public int Rank { get; set; }
    // Chuẩn hóa tên combo — không bao giờ undefined ở Frontend.
    // ComboType: BALANCED / STANDARD / LUXURY ; ComboTitle + ComboBadge dùng render card.
    public string ComboType { get; set; } = string.Empty;
    public string ComboTitle { get; set; } = string.Empty;
    public string ComboBadge { get; set; } = string.Empty;
    // Alias chuẩn hóa cho JS cũ dùng comboName/packageName/combo_name.
    public string ComboName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public double OverallMatchScore { get; set; }
    public double BudgetMatchScore { get; set; }
    public double SpaceFitScore { get; set; }
    public double StyleThemeScore { get; set; }
    public double RatingCredibilityScore { get; set; }
    public int HallScheduleId { get; set; }
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    /// <summary>Ảnh đại diện sảnh (primary ImageAsset). Frontend fallback placeholder khi rỗng.</summary>
    public string? HallImageUrl { get; set; }
    /// <summary>Số khách tối đa (giữ tương thích cũ). Đừng render kèm chữ "bàn".</summary>
    public int HallCapacity { get; set; }
    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách; SucChua NULL fallback 150 khách).
    [JsonPropertyName("capacity_min_guests")] public int CapacityMinGuests { get; set; }
    [JsonPropertyName("capacity_max_guests")] public int CapacityMaxGuests { get; set; }
    [JsonPropertyName("capacity_min_tables")] public int CapacityMinTables { get; set; }
    [JsonPropertyName("capacity_max_tables")] public int CapacityMaxTables { get; set; }
    public decimal HallRentalPrice { get; set; }
    public string HallDescription { get; set; } = string.Empty;
    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public decimal MenuPricePerTable { get; set; }
    public int EstimatedTableCount { get; set; }
    public decimal MenuTotalPrice { get; set; }
    public string MenuDescription { get; set; } = string.Empty;
    public int DecorationPackageId { get; set; }
    public string DecorationPackageName { get; set; } = string.Empty;
    public string DecorationStyle { get; set; } = string.Empty;
    public decimal DecorationPrice { get; set; }
    public string DecorationDescription { get; set; } = string.Empty;
    public string? DecorationImageUrl { get; set; }
    // Dịch vụ ưu tiên đi kèm (giá lấy trực tiếp từ bảng DichVu).
    public List<RecommendedServiceDto> IncludedServices { get; set; } = [];
    public decimal ServicesTotalPrice { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal BudgetDifference { get; set; }
    public string BudgetAnalysisText { get; set; } = string.Empty;
    public List<string> RecommendationReasons { get; set; } = [];
    /// <summary>XAI định lượng (P0 NFR04): đồng bộ với Python explanation_arguments. Serialize "explanations" để FE đọc 1 key.</summary>
    [JsonPropertyName("explanations")]
    public List<string> Explanations { get; set; } = [];
}

public sealed class RecommendationResponseDto
{
    public long RecommendationRequestId { get; set; }
    public int TotalCandidatesEvaluated { get; set; }
    public List<ComboSuggestionDto> TopCombos { get; set; } = [];
    public string AlgorithmNotice { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    /// <summary>Số combo trả về (P0 fallback 0 combo: count==0 + data==[] + suggestedNext).</summary>
    public int Count => TopCombos?.Count ?? 0;
    /// <summary>Alias của TopCombos cho contract {count,data,suggestedNext}.</summary>
    public List<ComboSuggestionDto> Data => TopCombos;
    public SuggestedNextDto? SuggestedNext { get; set; }
}

public sealed class SuggestedDateDto
{
    public string Date { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public List<string> AvailableHalls { get; set; } = [];
}

public sealed class SuggestedNextDto
{
    public decimal MinRequiredBudget { get; set; }
    public List<SuggestedDateDto> SuggestedDates { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class RegisterPublicBookingDto
{
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    // Email validate thủ công trong action để trả message tiếng Việt {success,message}
    // (tránh [ApiController] auto-400 ProblemDetails không có message khiến FE hiện tiếng Anh).
    public string? Email { get; set; }
    public int HallScheduleId { get; set; }
    public int HallId { get; set; }
    /// <summary>Ngày tổ chức (yyyy-MM-dd). Dùng auto-init LichSanh khi HallScheduleId &lt;= 0 hoặc trỏ nhầm sang HallId.</summary>
    public DateTime? EventDate { get; set; }
    /// <summary>Ca tổ chức (Ca trưa / Ca tối). Dùng auto-init LichSanh khi chưa có record.</summary>
    public string? Shift { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int GuestCount { get; set; }
    /// <summary>Tổng bàn (Official + Spare). Dùng fallback GuestCount = TableCount*10 khi GuestCount &lt;= 0.</summary>
    public int TableCount { get; set; }
    public int SpareTableCount { get; set; }
    /// <summary>Ngân sách dự kiến (forward từ Frontend để nhúng vào SpecialRequests/snapshot).</summary>
    public decimal? ExpectedBudget { get; set; }
    /// <summary>Phong cách mong muốn (forward từ Frontend để nhúng vào SpecialRequests/snapshot).</summary>
    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
    /// <summary>Tên thực đơn tùy chỉnh do khách tự chọn món (vd "Thực đơn tùy chỉnh 1"). Optional.</summary>
    public string? CustomMenuName { get; set; }
    /// <summary>Giá mỗi bàn của thực đơn tùy chỉnh (= tổng giá món đã chọn). Optional.</summary>
    public decimal? CustomMenuPricePerTable { get; set; }
    /// <summary>Danh sách món trong thực đơn tùy chỉnh ("Tên món (giá)") để lưu vào SpecialRequests. Optional.</summary>
    public List<string>? CustomMenuItems { get; set; }
    /// <summary>Mã dịch vụ khách đã chọn ở form (DV001...) để lưu BookingServices + cộng vào EstimatedTotal.</summary>
    public List<string>? ServiceCodes { get; set; }
}

/// <summary>
/// Contract chuẩn cho GET api/recommendations/hall-availability.
/// Frontend deserialize thành object {queryDate, halls:[...]} (camelCase qua Web defaults).
/// Bắt buộc có [JsonPropertyName] để khóa tên field, tránh lệch object-vs-array.
/// </summary>
public sealed class HallAvailabilityItemDto
{
    [JsonPropertyName("hallId")] public int HallId { get; set; }
    [JsonPropertyName("hallName")] public string HallName { get; set; } = string.Empty;
    [JsonPropertyName("hallCode")] public string HallCode { get; set; } = string.Empty;
    /// <summary>Số khách tối đa (giữ tương thích cũ). Đừng render kèm chữ "bàn".</summary>
    [JsonPropertyName("capacity")] public int Capacity { get; set; }
    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách; SucChua NULL fallback 150 khách).
    [JsonPropertyName("capacity_min_guests")] public int CapacityMinGuests { get; set; }
    [JsonPropertyName("capacity_max_guests")] public int CapacityMaxGuests { get; set; }
    [JsonPropertyName("capacity_min_tables")] public int CapacityMinTables { get; set; }
    [JsonPropertyName("capacity_max_tables")] public int CapacityMaxTables { get; set; }
    [JsonPropertyName("rentalPrice")] public decimal RentalPrice { get; set; }
    [JsonPropertyName("isNoonAvailable")] public bool IsNoonAvailable { get; set; }
    [JsonPropertyName("isEveningAvailable")] public bool IsEveningAvailable { get; set; }
}

public sealed class HallAvailabilityResponseDto
{
    [JsonPropertyName("queryDate")] public string QueryDate { get; set; } = string.Empty;
    [JsonPropertyName("halls")] public List<HallAvailabilityItemDto> Halls { get; set; } = new();
}

/// <summary>
/// Item gợi ý ngày còn sảnh trống cho modal "Xem Lịch &amp; Ca Trống Thực Tế".
/// </summary>
public sealed class AvailableDateItemDto
{
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("shift")] public string Shift { get; set; } = string.Empty;
    [JsonPropertyName("availableHallCount")] public int AvailableHallCount { get; set; }
    [JsonPropertyName("availableHalls")] public List<string> AvailableHalls { get; set; } = new();
}

/// <summary>
/// Dịch vụ ưu tiên đi kèm trong 1 combo (giá lấy trực tiếp từ bảng DichVu).
/// Contract JSON: {serviceCode, serviceName, price, note} (camelCase qua Web defaults).
/// </summary>
public sealed class RecommendedServiceDto
{
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Note { get; set; } = string.Empty;
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_WEB.Models.Recommendation;

public class RecommendationRequestViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên cặp đôi")]
    [Display(Name = "Họ tên cặp đôi / Người đại diện")]
    public string CoupleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày dự kiến")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddMonths(2);

    [Required]
    public string Shift { get; set; } = "Ca tối";

    public bool IsDateFlexible { get; set; } = false;

    [Required(ErrorMessage = "Vui lòng nhập số lượng khách dự kiến")]
    [Range(150, 1400, ErrorMessage = "Số lượng khách từ 150 đến 1400 (quy chuẩn 10 khách/bàn)")]
    public int GuestCount { get; set; } = 300;

    [Range(0, 500)]
    public int OfficialTableCount { get; set; } = 30;

    [Range(0, 10, ErrorMessage = "Bàn dự phòng từ 0 đến 10 bàn")]
    public int SpareTableCount { get; set; } = 2;

    public int TotalTables => OfficialTableCount + SpareTableCount;

    [Range(0, 500)]
    public int TableCount { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập ngân sách dự kiến")]
    [Range(100000000, 3000000000, ErrorMessage = "Ngân sách tối thiểu 100 triệu (chi phí tối thiểu ~103 triệu cho 150 khách) đến 3 tỷ VNĐ")]
    public decimal ExpectedBudget { get; set; } = 250000000;

    public string BudgetPriority { get; set; } = "BALANCED";

    public string? PreferredSpaceType { get; set; }

    public string? DesiredStyle { get; set; }

    public string? MainColorTone { get; set; }

    public string? FoodPreference { get; set; }

    public bool HasVegetarianOption { get; set; } = false;

    public List<string> SelectedServiceCodes { get; set; } = new();

    public string? SpecialRequests { get; set; }
}

public class ComboSuggestionViewModel
{
    public int Rank { get; set; }
    public string ComboType { get; set; } = string.Empty;
    public string ComboTitle { get; set; } = string.Empty;
    public string ComboBadge { get; set; } = string.Empty;
    // Alias chuẩn hóa chống undefined cho JS cũ (comboName/packageName/combo_name).
    public string ComboName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public double OverallMatchScore { get; set; }

    public double BudgetMatchScore { get; set; }
    public double SpaceFitScore { get; set; }
    public double StyleThemeScore { get; set; }
    public double RatingCredibilityScore { get; set; }

    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public string? HallImageUrl { get; set; }
    public int HallCapacity { get; set; }
    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách). Backend trả snake_case.
    [JsonPropertyName("capacity_min_guests")]
    public int CapacityMinGuests { get; set; }
    [JsonPropertyName("capacity_max_guests")]
    public int CapacityMaxGuests { get; set; }
    [JsonPropertyName("capacity_min_tables")]
    public int CapacityMinTables { get; set; }
    [JsonPropertyName("capacity_max_tables")]
    public int CapacityMaxTables { get; set; }
    public decimal HallRentalPrice { get; set; }
    public string HallDescription { get; set; } = string.Empty;

    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public decimal MenuPricePerTable { get; set; }
    public decimal MenuTotalPrice { get; set; }
    public string MenuDescription { get; set; } = string.Empty;

    public int DecorationPackageId { get; set; }
    public string DecorationPackageName { get; set; } = string.Empty;
    public string DecorationStyle { get; set; } = string.Empty;
    public decimal DecorationPrice { get; set; }
    public string DecorationDescription { get; set; } = string.Empty;

    public List<SuggestedServiceItemViewModel> IncludedServices { get; set; } = new();
    public decimal ServicesTotalPrice { get; set; }

    public decimal TotalEstimatedCost { get; set; }
    public decimal BudgetDifference { get; set; }
    public string BudgetAnalysisText { get; set; } = string.Empty;

    public List<string> RecommendationReasons { get; set; } = new();

    /// <summary>XAI định lượng (P0 NFR04): Backend C# "explanations" / Python "explanation_arguments".</summary>
    [JsonPropertyName("explanations")]
    public List<string> Explanations { get; set; } = new();

    [JsonPropertyName("explanation_arguments")]
    public List<string>? ExplanationArguments { get; set; }
}

public class SuggestedServiceItemViewModel
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class RecommendationResponseViewModel
{
    public string InquiryCode { get; set; } = string.Empty;
    public string CoupleName { get; set; } = string.Empty;
    public int TotalCandidatesEvaluated { get; set; }
    public List<ComboSuggestionViewModel> TopCombos { get; set; } = new();
    public string AlgorithmNotice { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;
    public int Count { get; set; }
    public List<ComboSuggestionViewModel>? Data { get; set; }
    public SuggestedNextViewModel? SuggestedNext { get; set; }
}

public class SuggestedDateViewModel
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
    [JsonPropertyName("shift")]
    public string Shift { get; set; } = string.Empty;
    [JsonPropertyName("availableHalls")]
    public List<string> AvailableHalls { get; set; } = new();
}

public class SuggestedNextViewModel
{
    [JsonPropertyName("minRequiredBudget")]
    public decimal MinRequiredBudget { get; set; }
    [JsonPropertyName("suggestedDates")]
    public List<SuggestedDateViewModel> SuggestedDates { get; set; } = new();
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class HallAvailabilityResponseViewModel
{
    [JsonPropertyName("queryDate")]
    public string QueryDate { get; set; } = string.Empty;

    [JsonPropertyName("halls")]
    public List<HallSlotItemViewModel> Halls { get; set; } = new();

    // Alias để tương thích nếu Backend cũ trả { data: [...] } bọc ngoài.
    [JsonPropertyName("availabilities")]
    public List<HallSlotItemViewModel>? AvailabilitiesLegacy { get; set; }
}

public class HallSlotItemViewModel
{
    [JsonPropertyName("hallId")]
    public int HallId { get; set; }

    [JsonPropertyName("hallName")]
    public string HallName { get; set; } = string.Empty;

    [JsonPropertyName("hallCode")]
    public string HallCode { get; set; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    // Chuẩn hóa khách vs bàn (1 bàn = 10 khách). Backend trả snake_case.
    [JsonPropertyName("capacity_min_guests")]
    public int CapacityMinGuests { get; set; }

    [JsonPropertyName("capacity_max_guests")]
    public int CapacityMaxGuests { get; set; }

    [JsonPropertyName("capacity_min_tables")]
    public int CapacityMinTables { get; set; }

    [JsonPropertyName("capacity_max_tables")]
    public int CapacityMaxTables { get; set; }

    [JsonPropertyName("rentalPrice")]
    public decimal RentalPrice { get; set; }

    [JsonPropertyName("isNoonAvailable")]
    public bool IsNoonAvailable { get; set; }

    [JsonPropertyName("isEveningAvailable")]
    public bool IsEveningAvailable { get; set; }
}

public class PublicBookingRequestViewModel
{
    public int? UserId { get; set; } // Thêm trường này
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = "Ca tối";
    public int HallId { get; set; }
    public int HallScheduleId { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int TableCount { get; set; }
    public int SpareTableCount { get; set; }
    public int GuestCount { get; set; }
    public decimal? ExpectedBudget { get; set; }
    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
    public string? CustomMenuName { get; set; }
    public decimal? CustomMenuPricePerTable { get; set; }
    public List<string>? CustomMenuItems { get; set; }
    public List<string>? ServiceCodes { get; set; }
}

public class AvailableDateItemViewModel
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("shift")]
    public string Shift { get; set; } = string.Empty;

    [JsonPropertyName("availableHallCount")]
    public int AvailableHallCount { get; set; }

    [JsonPropertyName("availableHalls")]
    public List<string> AvailableHalls { get; set; } = new();
}
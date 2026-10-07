using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

    [Required(ErrorMessage = "Vui lòng nhập số lượng bàn")]
    [Range(5, 250, ErrorMessage = "Số lượng từ 5 đến 250 bàn")]
    public int OfficialTableCount { get; set; } = 30;

    public int SpareTableCount { get; set; } = 2;

    public int TotalTables => OfficialTableCount + SpareTableCount;

    public int GuestCount { get; set; } = 300;

    [Required(ErrorMessage = "Vui lòng nhập ngân sách dự kiến")]
    [Range(50000000, 3000000000, ErrorMessage = "Ngân sách từ 50 triệu đến 3 tỷ VNĐ")]
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
    public string ComboType { get; set; } = string.Empty;
    public string ComboTitle { get; set; } = string.Empty;
    public string ComboBadge { get; set; } = string.Empty;
    public double OverallMatchScore { get; set; }

    public double BudgetMatchScore { get; set; }
    public double SpaceFitScore { get; set; }
    public double StyleThemeScore { get; set; }
    public double RatingCredibilityScore { get; set; }

    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public int HallCapacity { get; set; }
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
}

public class HallAvailabilityResponseViewModel
{
    public string QueryDate { get; set; } = string.Empty;
    public List<HallSlotItemViewModel> Halls { get; set; } = new();
}

public class HallSlotItemViewModel
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal RentalPrice { get; set; }
    public bool IsNoonAvailable { get; set; }
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
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int TableCount { get; set; }
    public decimal? ExpectedBudget { get; set; }
    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
}
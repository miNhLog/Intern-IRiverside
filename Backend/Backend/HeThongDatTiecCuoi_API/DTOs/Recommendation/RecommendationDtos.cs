using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Recommendation;

public sealed class RecommendationRequestDto
{
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal ExpectedBudget { get; set; }

    [Range(1, int.MaxValue)]
    public int GuestCount { get; set; }

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
    public double OverallMatchScore { get; set; }
    public double BudgetMatchScore { get; set; }
    public double SpaceFitScore { get; set; }
    public double StyleThemeScore { get; set; }
    public int HallScheduleId { get; set; }
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public int HallCapacity { get; set; }
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
    public decimal TotalEstimatedCost { get; set; }
    public decimal BudgetDifference { get; set; }
    public string BudgetAnalysisText { get; set; } = string.Empty;
    public List<string> RecommendationReasons { get; set; } = [];
}

public sealed class RecommendationResponseDto
{
    public long RecommendationRequestId { get; set; }
    public int TotalCandidatesEvaluated { get; set; }
    public List<ComboSuggestionDto> TopCombos { get; set; } = [];
    public string AlgorithmNotice { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}

public sealed class RegisterPublicBookingDto
{
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int HallScheduleId { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }
}

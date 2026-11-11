using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Review;

public sealed class SubmitWeddingReviewRequest
{
    // Validated explicitly in the controller so guest requests can enforce the six
    // required criteria while the host contract keeps its previous shape.
    public int? HallScore { get; set; }
    public int? FoodScore { get; set; }
    public int? ServiceScore { get; set; }
    public int? SoundScore { get; set; }
    public int? LightingScore { get; set; }
    public int? HygieneScore { get; set; }
    public int OverallScore { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(80)] public string? SessionId { get; set; }
}

public sealed record ReviewQrIssueDto(
    int BookingId, string BookingCode, string Token, string ReviewUrl, DateTime ExpiresAt, bool Created);

public sealed record ReviewFormDto(
    int BookingId, string BookingCode, string HallName, DateTime EventDate, string Shift,
    string State, DateTime OpensAt, DateTime ExpiresAt, bool CanSubmit, string Message, bool AlreadyReviewed = false);

public sealed record ReviewQrListItemDto(
    int BookingId, string BookingCode, string HallName, DateTime EventDate, string Shift,
    string? Token, string? ReviewUrl, string State, DateTime OpensAt, DateTime ExpiresAt,
    int ReviewCount, double? AverageScore, IReadOnlyList<ReviewCommentDto> Reviews);

public sealed record ReviewCommentDto(string ReviewerType, int OverallScore, string? Comment, DateTime ReviewedAt);

public sealed record ReviewStatisticsDto(
    int BookingId, string BookingCode, string CustomerName, string HallName, DateTime EventDate, string Shift,
    string State, DateTime OpensAt, DateTime ExpiresAt, int TotalReviews, int GuestReviews, int HostReviews,
    double? OverallAverage, double? HallAverage, double? FoodAverage, double? ServiceAverage,
    double? SoundAverage, double? LightingAverage, double? HygieneAverage, IReadOnlyDictionary<int, int> RatingDistribution,
    IReadOnlyList<ReviewCommentDto> Reviews);

public sealed record ReviewCriterionAveragesDto(
    double? Hall, double? Food, double? Service, double? Sound, double? Lighting, double? Hygiene);

public sealed record ReviewFeedbackDto(
    int ReviewId, string ReviewerType, int OverallScore, int? HallScore, int? FoodScore,
    int? ServiceScore, int? SoundScore, int? LightingScore, int? HygieneScore, string? Comment,
    DateTime ReviewedAt, string Classification);

public sealed record ReviewOverviewBookingDto(
    int BookingId, string BookingCode, string HallName, int HallId, DateTime EventDate, string Shift,
    string BookingStatus, string QrState, int ReviewCount, double? AverageScore);

public sealed record AttentionReviewDto(
    int BookingId, string BookingCode, string HallName, int Score, string? Comment, DateTime ReviewedAt);

public sealed record ReviewOverviewDto(
    int TotalBookings, int ReviewedBookings, int TotalReviews, double? AverageScore,
    double? PositiveRate, double? NeutralRate, double? NegativeRate,
    IReadOnlyDictionary<int, int> RatingDistribution, ReviewCriterionAveragesDto Criteria,
    IReadOnlyList<ReviewOverviewBookingDto> Bookings, IReadOnlyList<AttentionReviewDto> AttentionReviews,
    IReadOnlyList<ReviewHallOptionDto> Halls, int? SelectedHallId, int Page, int PageSize, int TotalPages);

public sealed record ReviewHallOptionDto(int HallId, string HallName);

public sealed record ReviewBookingDetailStatisticsDto(
    int BookingId, string BookingCode, string HallName, DateTime EventDate, string Shift, string QrState,
    int TotalReviews, int GuestReviews, int HostReviews, int PositiveCount, int NeutralCount, int NegativeCount,
    double? AverageScore, double? SatisfactionRate, IReadOnlyDictionary<int, int> RatingDistribution,
    ReviewCriterionAveragesDto Criteria, IReadOnlyList<ReviewFeedbackDto> Reviews);

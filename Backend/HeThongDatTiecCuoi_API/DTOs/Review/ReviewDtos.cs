using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Review;

public sealed class SubmitWeddingReviewRequest
{
    [Range(1, 5)] public int? HallScore { get; set; }
    [Range(1, 5)] public int? FoodScore { get; set; }
    [Range(1, 5)] public int? ServiceScore { get; set; }
    [Range(1, 5)] public int? SoundScore { get; set; }
    [Range(1, 5)] public int? LightingScore { get; set; }
    [Range(1, 5)] public int OverallScore { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(80)] public string? SubmissionId { get; set; }
}

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
    double? SoundAverage, double? LightingAverage, IReadOnlyDictionary<int, int> RatingDistribution,
    IReadOnlyList<ReviewCommentDto> Reviews);

public sealed record ReviewCriterionAveragesDto(
    double? Hall, double? Food, double? Service, double? Sound, double? Lighting);

public sealed record ReviewFeedbackDto(
    int ReviewId, string ReviewerType, int OverallScore, int? HallScore, int? FoodScore,
    int? ServiceScore, int? SoundScore, int? LightingScore, string? Comment,
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

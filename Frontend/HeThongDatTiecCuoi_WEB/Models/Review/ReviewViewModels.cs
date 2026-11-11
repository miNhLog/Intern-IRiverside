using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Review;

public sealed class GuestReviewFormViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime OpensAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool CanSubmit { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool AlreadyReviewed { get; set; }

    // Filled in by the WEB controller: the API form DTO does not echo the token.
    public string Token { get; set; } = string.Empty;
    public bool Submitted { get; set; }
    public GuestReviewScoresViewModel Scores { get; set; } = new();
}

public sealed class GuestReviewScoresViewModel
{
    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? HallScore { get; set; }

    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? FoodScore { get; set; }

    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? ServiceScore { get; set; }

    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? SoundScore { get; set; }

    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? LightingScore { get; set; }

    [Required(ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    [Range(1, 5, ErrorMessage = "Vui lòng chấm điểm từ 1 đến 5.")]
    public int? HygieneScore { get; set; }

    [StringLength(1000, ErrorMessage = "Cảm nhận không vượt quá 1000 ký tự.")]
    public string? Comment { get; set; }
}

public sealed class GuestReviewSubmitResult
{
    public bool Success { get; set; }
    public bool Duplicate { get; set; }
    public string? Message { get; set; }
}

public sealed class ReviewQrIssueViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ReviewUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Created { get; set; }
}

public sealed class ReviewStatusViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

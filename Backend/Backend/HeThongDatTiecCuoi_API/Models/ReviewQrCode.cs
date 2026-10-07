namespace HeThongDatTiecCuoi_API.Models;

public sealed class ReviewQrCode
{
    public int ReviewQrCodeId { get; set; }
    public int BookingId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int StatusId { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ICollection<WeddingReview> Reviews { get; set; } = [];
}

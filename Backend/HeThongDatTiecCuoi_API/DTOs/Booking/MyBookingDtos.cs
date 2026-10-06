namespace HeThongDatTiecCuoi_API.DTOs.Booking;

public sealed class MyBookingDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string? MenuName { get; set; }
    public decimal? FinalMenuPrice { get; set; }
    public string? DecorName { get; set; }
    public decimal? FinalDecorationPrice { get; set; }
    public decimal? FinalHallPrice { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
    public string? SpecialRequests { get; set; }
}

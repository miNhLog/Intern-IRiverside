using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

public class ManagerBookingRescheduleQuoteRequestDto
{
    [Required] public DateTime? NewDate { get; set; }
    [Required] public string NewShift { get; set; } = string.Empty;
    [Required] public int? NewHallId { get; set; }
}

public sealed class ManagerBookingRescheduleRequestDto : ManagerBookingRescheduleQuoteRequestDto
{
    [MaxLength(500)] public string? Reason { get; set; }
    public bool PriceDifferenceConfirmed { get; set; }
    public decimal? ConfirmedPriceDifference { get; set; }
}

public sealed class ManagerBookingCancelRequestDto
{
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
}

public sealed class ManagerBookingRescheduleQuoteDto
{
    public int BookingId { get; set; }
    public int NewHallId { get; set; }
    public DateTime NewDate { get; set; }
    public string NewShift { get; set; } = string.Empty;
    public decimal? SavedHallPrice { get; set; }
    public decimal CurrentHallPrice { get; set; }
    public decimal PriceDifference { get; set; }
    public bool RequiresPriceConfirmation { get; set; }
    public string CapacityCheck { get; set; } = "UNKNOWN";
    public string CapacityMessage { get; set; } = string.Empty;
    public int? SpareTableCount { get; set; }
}

public sealed class ManagerBookingHallOptionDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public decimal RentalPrice { get; set; }
}

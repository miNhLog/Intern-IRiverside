namespace HeThongDatTiecCuoi_API.Models;

public sealed class WeddingBookingService
{
    public int BookingServiceId { get; set; }
    public int BookingId { get; set; }
    public int ServiceId { get; set; }
    public int Quantity { get; set; }
    public decimal FinalUnitPrice { get; set; }
    public byte DataStatusId { get; set; }

    public WeddingBooking WeddingBooking { get; set; } = null!;
    public ServiceItem ServiceItem { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
}

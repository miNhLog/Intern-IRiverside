namespace HeThongDatTiecCuoi_API.Models;

public sealed class Contract
{
    public int ContractId { get; set; }
    public int BookingId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public decimal TotalValue { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
    public int StatusId { get; set; }

    public WeddingBooking Booking { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}

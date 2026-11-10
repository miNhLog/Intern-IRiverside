namespace HeThongDatTiecCuoi_API.Models;

public sealed class PaymentTransactionEvent
{
    public long PaymentTransactionEventId { get; set; }
    public long PaymentTransactionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public string? Details { get; set; }

    public PaymentTransaction PaymentTransaction { get; set; } = null!;
}

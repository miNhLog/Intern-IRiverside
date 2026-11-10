namespace HeThongDatTiecCuoi_API.Models;

public sealed class Payment
{
    public int PaymentId { get; set; }
    public int ContractId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionCode { get; set; }
    public int StatusId { get; set; }

    public Contract Contract { get; set; } = null!;
    public Status Status { get; set; } = null!;
}

namespace HeThongDatTiecCuoi_WEB.Models.Payment;

public sealed class CreateDepositPaymentRequestViewModel
{
    public int BookingId { get; set; }
    public string? BookingCode { get; set; }
    public string? PhoneNumber { get; set; }
}

public sealed class CreateDepositPaymentResponseViewModel
{
    public long OrderCode { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? BankBin { get; set; }
    public string? BankName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BusinessDeposit { get; set; }
    public decimal BusinessAmount { get; set; }
    public decimal Amount { get; set; }
    public int ActualAmount { get; set; }
    public bool IsDemoMode { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class PaymentStatusResponseViewModel
{
    public long OrderCode { get; set; }
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public decimal BusinessDeposit { get; set; }
    public decimal ActualAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateCustomerContractPaymentRequestViewModel
{
    public int BookingId { get; set; }
    public string? BookingCode { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? Amount { get; set; }
}

namespace HeThongDatTiecCuoi_API.DTOs.Invoice;

public sealed class InvoicePdfDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string PaymentType { get; set; } = "Đặt cọc (20%)";
    public string PaymentMethod { get; set; } = "VIETQR_PAYOS";
    public string TransactionCode { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public int TotalTables { get; set; }
    public string? MenuName { get; set; }
    public decimal? MenuPricePerTable { get; set; }
    public string? DecorName { get; set; }
    public decimal? DecorPrice { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AccumulatedPaid { get; set; }
    public decimal RemainingDebt { get; set; }
    public string CompanyName { get; set; } = "CÔNG TY CỔ PHẦN TRUNG TÂM HỘI NGHỊ TIỆC CƯỚI RIVERSIDE PALACE";
    public string CompanyTaxCode { get; set; } = "0310896789";
    public string CompanyAddress { get; set; } = "360D Bến Vân Đồn, Phường 1, Quận 4, TP. Hồ Chí Minh";
    public string CompanyHotline { get; set; } = "0933 777 888 - (028) 6256 8888";
    public string VerificationUrl { get; set; } = string.Empty;
    public bool IsDigitallySigned { get; set; } = true;
    public DateTime? SignedAt { get; set; } = DateTime.Now;
    public string SignatureAlgorithm { get; set; } = "RSA-SHA256 (PAdES Basic)";
}

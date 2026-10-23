using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

/// <summary>
/// UC14: Request tra cứu tiệc cưới trực tuyến (AllowAnonymous).
/// Map DatTiec.MaDatTiec + KhachHang.SoDienThoai.
/// </summary>
public sealed class PublicBookingLookupRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [StringLength(60)]
    [RegularExpression(@"^\s*(?i:DT)-\d{8}-[A-Za-z0-9]{4,}\s*$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(30)]
    [RegularExpression(@"^\s*0\d{9}\s*$",
        ErrorMessage = "Số điện thoại Việt Nam phải gồm đúng 10 số và bắt đầu bằng 0.")]
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// UC15: Request hủy giữ chỗ online, verify sở hữu bằng mã + SĐT.
/// </summary>
public sealed class PublicBookingCancelHoldRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [RegularExpression(@"^DT-\d{8}-[A-Z0-9]{4,}$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại Việt Nam phải gồm đúng 10 số và bắt đầu bằng 0.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}

/// <summary>Customer-facing successful payment information without transaction identifiers.</summary>
public sealed class PublicBookingPaymentDto
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
}

/// <summary>UC14 customer-facing summary; excludes internal identifiers and unrelated personal data.</summary>
public sealed class PublicBookingDetailDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventDateString { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public bool CanCancelOnline { get; set; }
    public DateTime HoldExpiresAt { get; set; }
    public bool HasContract { get; set; }
    public decimal? ContractTotal { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public decimal? RequiredDepositAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal DepositPaid { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal? RemainingBalance { get; set; }
    public List<PublicBookingPaymentDto> SuccessfulPayments { get; set; } = [];
}

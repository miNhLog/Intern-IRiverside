using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.MyBookings;

public class PublicBookingLookupInputViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [RegularExpression(@"^DT-\d{8}-[A-Z0-9]{4,}$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    [Display(Name = "Mã đặt tiệc")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [RegularExpression(@"^(0\d{9}|[^\s@]+@[^\s@]+\.[^\s@]+)$",
        ErrorMessage = "Vui lòng nhập số điện thoại 10 số hoặc email hợp lệ.")]
    [StringLength(320)]
    [Display(Name = "Email hoặc số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    public string? CancelReason { get; set; }
}

public class PublicBookingDishViewModel
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PublicBookingServiceViewModel
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class PublicBookingDetailViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime EventDate { get; set; }
    public string EventDateString { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public decimal HallPrice { get; set; }
    public int HallCapacity { get; set; }
    public int GuestCount { get; set; }
    public int OfficialTables { get; set; }
    public int SpareTables { get; set; }
    public int TotalTables { get; set; }
    public int? MenuId { get; set; }
    public string? MenuName { get; set; }
    public decimal? MenuPricePerTable { get; set; }
    public List<PublicBookingDishViewModel> Dishes { get; set; } = new();
    public int? DecorId { get; set; }
    public string? DecorName { get; set; }
    public decimal? DecorPrice { get; set; }
    public string? DecorStyle { get; set; }
    public List<PublicBookingServiceViewModel> Services { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal DepositPaid { get; set; }
    public decimal RemainingDebt { get; set; }
    public bool HasPaidStage2 { get; set; }
    public decimal Stage2Paid { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public bool CanCancelOnline { get; set; }
    public DateTime HoldExpiresAt { get; set; }
    public DateTime BookedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? SpecialRequests { get; set; }
    public string? ConsultantName { get; set; }
    public string? ConsultantPhone { get; set; }

    public int? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? ContractStatusCode { get; set; }
    public string? ContractStatusName { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? ContractTotalValue { get; set; }
    public decimal? ContractPaidAmount { get; set; }
    public bool HasContract => ContractId.HasValue;

    // Compatibility aliases for the existing public booking view.
    public decimal? ContractTotal => ContractTotalValue;
    public decimal? EstimatedTotal => TotalAmount;
    public decimal? RequiredDepositAmount { get; set; }
    public decimal TotalPaid => ContractPaidAmount ?? DepositPaid;
    public decimal? RemainingBalance => ContractTotalValue.HasValue
        ? Math.Max(0, ContractTotalValue.Value - TotalPaid)
        : RemainingDebt;
    public bool CanRescheduleOnline { get; set; }
    public bool IsHoldExpired => HoldExpiresAt <= DateTime.Now;
    public List<PublicBookingPaymentViewModel> SuccessfulPayments { get; set; } = new();

    public bool IsCancelled => StatusCode == "CANCELLED" || StatusName == "Đã hủy";
    public bool IsPending => StatusCode == "PENDING" || StatusName == "Chờ xác nhận";
}

public sealed class PublicBookingPaymentViewModel
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
}

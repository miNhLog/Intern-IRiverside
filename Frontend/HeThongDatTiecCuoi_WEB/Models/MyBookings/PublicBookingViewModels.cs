using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.MyBookings;

public class PublicBookingLookupInputViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [StringLength(60)]
    [RegularExpression(@"^\s*(?i:DT)-\d{8}-[A-Za-z0-9]{4,}\s*$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    [Display(Name = "Mã đặt tiệc")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(30)]
    [RegularExpression(@"^\s*0\d{9}\s*$",
        ErrorMessage = "Số điện thoại phải gồm đúng 10 số và bắt đầu bằng 0.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    public string? CancelReason { get; set; }
}

public class PublicBookingDetailViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventDateString { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public bool HasContract { get; set; }
    public decimal? ContractTotal { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public decimal? RequiredDepositAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal DepositPaid { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal? RemainingBalance { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public bool CanCancelOnline { get; set; }
    public bool CanRescheduleOnline { get; set; }
    public bool IsHoldExpired { get; set; }
    public DateTime HoldExpiresAt { get; set; }
    public List<PublicBookingPaymentViewModel> SuccessfulPayments { get; set; } = new();

    public bool IsCancelled => StatusCode == "CANCELLED" || StatusName == "Đã hủy";
    public bool IsPending => StatusCode == "PENDING" || StatusName == "Chờ xác nhận";
}

public class PublicBookingPaymentViewModel
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
}

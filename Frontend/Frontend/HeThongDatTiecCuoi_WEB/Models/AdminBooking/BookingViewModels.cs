using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.AdminBooking;

public class BookingFilterRequestViewModel
{
    public string? Keyword { get; set; }
    public string? Status { get; set; } = "ALL";
    public int? HallId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class BookingListResponseViewModel
{
    public List<BookingSummaryViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 10));

    public int PendingConfirmationCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int DepositedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
}

public class BookingSummaryViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public decimal EstimatedTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? BookedAt { get; set; }
}

public class BookingDetailViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public int? HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;

    public int? MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public int? DecorationPackageId { get; set; }
    public string DecorationPackageName { get; set; } = string.Empty;

    public int GuestCount { get; set; }
    public int TableCount { get; set; }
    public decimal ExpectedBudget { get; set; }
    public string DesiredStyle { get; set; } = string.Empty;
    public string SpecialRequests { get; set; } = string.Empty;

    public decimal FinalMenuPrice { get; set; }
    public decimal FinalDecorationPrice { get; set; }
    public decimal FinalHallPrice { get; set; }
    public decimal EstimatedTotal { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateBookingRequestViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn khách hàng")]
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sảnh tiệc")]
    public int HallId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày tổ chức")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddDays(30);

    [Required(ErrorMessage = "Vui lòng chọn ca tổ chức")]
    public string Shift { get; set; } = "Ca tối";

    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }

    [Range(10, 3000, ErrorMessage = "Số lượng khách từ 10 đến 3000")]
    public int GuestCount { get; set; } = 100;

    [Range(1, 300, ErrorMessage = "Số lượng bàn từ 1 đến 300")]
    public int TableCount { get; set; } = 10;

    public decimal? ExpectedBudget { get; set; }
    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
}

public class UpdateBookingStatusRequestViewModel
{
    [Required]
    public string NewStatus { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Notes { get; set; }
}
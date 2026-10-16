using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    [JsonPropertyName("pendingCount")]
    public int PendingConfirmationCount { get; set; }
    public int ConfirmedCount { get; set; }
    [JsonPropertyName("preparingCount")]
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
    [JsonPropertyName("statusName")]
    public string Status { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? BookedAt { get; set; }
    public bool HasContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? ContractStatusCode { get; set; }
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

    [JsonPropertyName("statusName")]
    public string Status { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool HasContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? ContractStatusCode { get; set; }
}

public class CreateBookingRequestViewModel
{
    [Required, MaxLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress, MaxLength(255)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sảnh tiệc")]
    public int HallId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày tổ chức")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddMonths(1);

    [Required(ErrorMessage = "Vui lòng chọn ca tổ chức")]
    public string Shift { get; set; } = "Ca tối";

    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }

    [Range(1, 300, ErrorMessage = "Số lượng bàn từ 1 đến 300")]
    public int TableCount { get; set; } = 10;

    [Range(0, 50)]
    public int SpareTableCount { get; set; }

    public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
    public List<CreateBookingServiceViewModel> Services { get; set; } = new();
}

public class CreateBookingServiceViewModel
{
    [Range(1, int.MaxValue)] public int ServiceId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public class BookingCreateOptionsViewModel
{
    public string QueryDate { get; set; } = string.Empty;
    public List<BookingHallOptionViewModel> Halls { get; set; } = new();
    public List<BookingMenuOptionViewModel> Menus { get; set; } = new();
    public List<BookingDecorOptionViewModel> Decorations { get; set; } = new();
    public List<BookingServiceOptionViewModel> Services { get; set; } = new();
}

public class BookingHallOptionViewModel
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public int MinimumCapacity { get; set; }
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public bool IsNoonAvailable { get; set; }
    public bool IsEveningAvailable { get; set; }
}

public class BookingMenuOptionViewModel { public int MenuId { get; set; } public string MenuName { get; set; } = string.Empty; public decimal PricePerTable { get; set; } }
public class BookingDecorOptionViewModel { public int DecorationPackageId { get; set; } public string PackageName { get; set; } = string.Empty; public string Style { get; set; } = string.Empty; public decimal Price { get; set; } }
public class BookingServiceOptionViewModel { public int ServiceId { get; set; } public string ServiceName { get; set; } = string.Empty; public string? ServiceType { get; set; } public decimal Price { get; set; } }
public class CreateBookingResponseViewModel { public bool Success { get; set; } public int BookingId { get; set; } public string BookingCode { get; set; } = string.Empty; }

public class UpdateBookingStatusRequestViewModel
{
    [Required]
    [JsonPropertyName("statusCode")]
    public string NewStatus { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Notes { get; set; }
}

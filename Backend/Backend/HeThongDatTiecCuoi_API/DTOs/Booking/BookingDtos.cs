using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

public sealed class BookingFilterRequestDto
{
    public string? Keyword { get; set; }
    public string? StatusCode { get; set; }
    public int? HallId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int PageIndex { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class BookingListResponseDto
{
    public List<BookingSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int PreparingCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
}

public sealed class BookingSummaryDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
}

public sealed class BookingDetailDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int HallScheduleId { get; set; }
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int? MenuId { get; set; }
    public string? MenuName { get; set; }
    public int? DecorationPackageId { get; set; }
    public string? DecorationPackageName { get; set; }
    public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }
    public decimal? FinalMenuPrice { get; set; }
    public decimal? FinalDecorationPrice { get; set; }
    public decimal? FinalHallPrice { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime BookedAt { get; set; }
    public List<BookingServiceDto> Services { get; set; } = [];
}

public sealed class BookingServiceDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal FinalUnitPrice { get; set; }
}

public sealed class CreateBookingRequestDto
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Range(1, int.MaxValue)] public int HallScheduleId { get; set; }
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    [Range(1, int.MaxValue)] public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }
}

public sealed class UpdateBookingStatusRequestDto
{
    [Required] public string StatusCode { get; set; } = string.Empty;
    [MaxLength(500)] public string? CancellationReason { get; set; }
}

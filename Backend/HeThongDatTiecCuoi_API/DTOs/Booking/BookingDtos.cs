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
    public bool HasContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? ContractStatusCode { get; set; }
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
    public bool HasContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? ContractStatusCode { get; set; }
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
    [Required, MaxLength(150)] public string CustomerName { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string PhoneNumber { get; set; } = string.Empty;
    [EmailAddress, MaxLength(255)] public string? Email { get; set; }
    [Range(1, int.MaxValue)] public int HallId { get; set; }
    public DateTime EventDate { get; set; }
    [Required] public string Shift { get; set; } = string.Empty;
    public int? MenuId { get; set; }
    public int? DecorationPackageId { get; set; }
    [Range(1, 300)] public int TableCount { get; set; }
    [Range(0, 50)] public int SpareTableCount { get; set; }
    [MaxLength(150)] public string? DesiredStyle { get; set; }
    public string? SpecialRequests { get; set; }
    public List<CreateBookingServiceDto> Services { get; set; } = [];
}

public sealed class CreateBookingServiceDto
{
    [Range(1, int.MaxValue)] public int ServiceId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public sealed class BookingCreateOptionsDto
{
    public string QueryDate { get; set; } = string.Empty;
    public List<BookingHallOptionDto> Halls { get; set; } = [];
    public List<BookingMenuOptionDto> Menus { get; set; } = [];
    public List<BookingDecorOptionDto> Decorations { get; set; } = [];
    public List<BookingServiceOptionDto> Services { get; set; } = [];
}
public sealed class BookingHallOptionDto { public int HallId { get; set; } public string HallName { get; set; } = string.Empty; public int MinimumCapacity { get; set; } public int MaximumCapacity { get; set; } public decimal RentalPrice { get; set; } public bool IsNoonAvailable { get; set; } public bool IsEveningAvailable { get; set; } }
public sealed class BookingMenuOptionDto { public int MenuId { get; set; } public string MenuName { get; set; } = string.Empty; public decimal PricePerTable { get; set; } }
public sealed class BookingDecorOptionDto { public int DecorationPackageId { get; set; } public string PackageName { get; set; } = string.Empty; public string Style { get; set; } = string.Empty; public decimal Price { get; set; } }
public sealed class BookingServiceOptionDto { public int ServiceId { get; set; } public string ServiceName { get; set; } = string.Empty; public string? ServiceType { get; set; } public decimal Price { get; set; } }

public sealed class UpdateBookingStatusRequestDto
{
    [Required] public string StatusCode { get; set; } = string.Empty;
    [MaxLength(500)] public string? CancellationReason { get; set; }
}

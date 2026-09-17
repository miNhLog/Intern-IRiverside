using HeThongDatTiecCuoi_API.DTOs.BanquetHalls;

namespace HeThongDatTiecCuoi_API.DTOs.HallSchedules;

public sealed class WeeklyHallScheduleResponse
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<HallScheduleSummaryDto> Halls { get; set; } = [];
}

public sealed class HallScheduleSummaryDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public string HallStatus { get; set; } = string.Empty;
    public List<ScheduleSlotDto> Slots { get; set; } = [];
}

public sealed class ScheduleSlotDto
{
    public int ScheduleId { get; set; }
    public DateTime Date { get; set; }
    public string EventSession { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public BookingSummaryDto? Booking { get; set; }
}

public sealed class ScheduleDetailDto
{
    public int ScheduleId { get; set; }
    public int HallId { get; set; }
    public DateTime Date { get; set; }
    public string EventSession { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public BanquetHallDto? Hall { get; set; }
}

public sealed class BookingSummaryDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int? TableCount { get; set; }
    public int? GuestCount { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
}

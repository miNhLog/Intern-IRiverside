namespace HeThongDatTiecCuoi_WEB.Models.AdminHall;

public sealed class HallManagementViewModel
{
    public List<HallDto> Halls { get; set; } = new();

    public WeeklyHallScheduleDto? WeeklySchedule { get; set; }

    public DateTime StartDate { get; set; }

    public int? HallId { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class HallDto
{
    public int HallId { get; set; }

    public string HallCode { get; set; } = string.Empty;

    public string HallName { get; set; } = string.Empty;

    public int? MinimumCapacity { get; set; }

    public int MaximumCapacity { get; set; }

    public decimal RentalPrice { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class WeeklyHallScheduleDto
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public List<HallScheduleGroupDto> Halls { get; set; } = new();
}

public sealed class HallScheduleGroupDto
{
    public int HallId { get; set; }

    public string HallCode { get; set; } = string.Empty;

    public string HallName { get; set; } = string.Empty;

    public string HallStatus { get; set; } = string.Empty;
    public string HallStatusName { get; set; } = string.Empty;

    public List<HallScheduleSlotDto> Schedule { get; set; } = new();
}

public sealed class HallScheduleSlotDto
{
    public int HallScheduleId { get; set; }

    public DateTime Date { get; set; }

    public string Shift { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public ScheduleBookingDto? Booking { get; set; }
}

public sealed class ScheduleBookingDto
{
    public int BookingId { get; set; }

    public string BookingCode { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public int? TableCount { get; set; }

    public int? GuestCount { get; set; }

    public string BookingStatus { get; set; } = string.Empty;
}

public sealed class ActionResponseDto
{
    public string Message { get; set; } = string.Empty;
}

namespace HeThongDatTiecCuoi_API.DTOs.Dashboard;

public sealed class DashboardPeriodQueryDto
{
    public string? FromDate { get; init; }

    public string? ToDate { get; init; }

    public string? Granularity { get; init; }
}

public sealed class DashboardPeriodDto
{
    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    public string Granularity { get; init; } = string.Empty;
}

public sealed class MoneyMetricDto
{
    public decimal Amount { get; init; }

    public string Currency { get; init; } = "VND";

    public string DateBasis { get; init; } = string.Empty;
}

public sealed class DashboardKpiResponseDto
{
    public DashboardPeriodDto Period { get; init; } = new();

    public MoneyMetricDto CashCollected { get; init; } = new();

    public MoneyMetricDto ContractedValue { get; init; } = new();

    public int TotalBookings { get; init; }

    public int CancelledBookings { get; init; }

    public int UnmappedStatusCount { get; init; }

    public decimal CancellationRate { get; init; }

    public int OccupiedSlots { get; init; }

    public int AvailableSlots { get; init; }

    public int BlockedSlots { get; init; }

    public decimal HallUtilizationRate { get; init; }

    public decimal? AverageOverallScore { get; init; }

    public int ReviewCount { get; init; }

    public int RatedEventCount { get; init; }

    public IReadOnlyList<StarDistributionItemDto> StarDistribution { get; init; } = [];
}

public sealed class RevenueTrendResponseDto
{
    public DashboardPeriodDto Period { get; init; } = new();

    public string CashCollectedDateBasis { get; init; } = "payment-date";

    public string ContractedValueDateBasis { get; init; } = "event-date";

    public IReadOnlyList<RevenueTrendPointDto> Points { get; init; } = [];
}

public sealed class RevenueTrendPointDto
{
    public DateOnly PeriodStart { get; init; }

    public DateOnly PeriodEnd { get; init; }

    public decimal CashCollected { get; init; }

    public decimal ContractedValue { get; init; }
}

public sealed class BookingStatusDistributionResponseDto
{
    public DashboardPeriodDto Period { get; init; } = new();

    public int TotalBookings { get; init; }

    public int UnmappedStatusCount { get; init; }

    public decimal CancellationRate { get; init; }

    public IReadOnlyList<BookingStatusMetricDto> Statuses { get; init; } = [];
}

public sealed class BookingStatusMetricDto
{
    public string StatusCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int Count { get; init; }

    public decimal Percentage { get; init; }
}

public sealed class HallUtilizationResponseDto
{
    public DashboardPeriodDto Period { get; init; } = new();

    public int OccupiedSlots { get; init; }

    public int AvailableSlots { get; init; }

    public int BlockedSlots { get; init; }

    public decimal UtilizationRate { get; init; }

    public IReadOnlyList<HallUtilizationItemDto> Halls { get; init; } = [];
}

public sealed class HallUtilizationItemDto
{
    public int HallId { get; init; }

    public string HallCode { get; init; } = string.Empty;

    public string HallName { get; init; } = string.Empty;

    public int OccupiedSlots { get; init; }

    public int AvailableSlots { get; init; }

    public int BlockedSlots { get; init; }

    public decimal UtilizationRate { get; init; }
}

public sealed class StarDistributionItemDto
{
    public int Stars { get; init; }

    public int Count { get; init; }
}

public sealed class UpcomingEventsResponseDto
{
    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    public IReadOnlyList<UpcomingEventDto> Events { get; init; } = [];
}

public sealed class UpcomingEventDto
{
    public int BookingId { get; init; }

    public string BookingCode { get; init; } = string.Empty;

    public DateOnly EventDate { get; init; }

    public string EventSession { get; init; } = string.Empty;

    public string BookingStatus { get; init; } = string.Empty;

    public int HallId { get; init; }

    public string HallCode { get; init; } = string.Empty;

    public string HallName { get; init; } = string.Empty;

    public int CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public int? GuestCount { get; init; }

    public int? TableCount { get; init; }
}

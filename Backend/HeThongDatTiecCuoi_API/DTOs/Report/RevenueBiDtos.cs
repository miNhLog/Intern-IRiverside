namespace HeThongDatTiecCuoi_API.DTOs.Report;

public class RevenueBiReportDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int Month { get; set; }
    public int? HallId { get; set; }
    public string? Shift { get; set; }
    public KpiSummaryDto Kpi { get; set; } = new();
    public List<MonthlyRevenueDto> MonthlyRevenues { get; set; } = new();
    public List<RevenueStructureDto> RevenueStructures { get; set; } = new();
    public List<HallPerformanceDto> HallPerformances { get; set; } = new();
    public List<RecentBookingDto> RecentBookings { get; set; } = new();
    public List<ShiftOccupancyDto> OccupancyByShift { get; set; } = new();
    public QualitySummaryDto Quality { get; set; } = new();
    public List<ReportHallOptionDto> HallOptions { get; set; } = new();
}

public class KpiSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal EstimatedTotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int EventsHeld { get; set; }
    public int EventsCancelled { get; set; }
    public int TotalContracts { get; set; }
    public decimal TotalContractValue { get; set; }
    public decimal OutstandingReceivables { get; set; }
    public decimal AverageRevenuePerTable { get; set; }
    public decimal AverageMenuPricePerTable { get; set; }
    public int TotalTables { get; set; }
    public double AverageOccupancyRate { get; set; }
    // Tiền cọc bị mất và tỷ lệ theo thao tác hủy UC18 thực tế của các tiệc hủy đã lọc.
    public decimal? ForfeitedDepositAmount { get; set; }
    public double? ForfeitedDepositRate { get; set; }
    public int? RecordedUc18CancellationCount { get; set; }
    public string? ForfeitedDepositDenominatorLabel { get; set; }
    public bool ForfeitedDepositAvailable { get; set; }
}

public class MonthlyRevenueDto
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal EstimatedRevenue { get; set; }
    public int BookingCount { get; set; }
    public int ContractCount { get; set; }
}

public class RevenueStructureDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public string ColorHex { get; set; } = string.Empty;
}

public class HallPerformanceDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int BookingCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal EstimatedRevenue { get; set; }
    public double OccupancyRate { get; set; }
}

public class ShiftOccupancyDto
{
    public string Shift { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public int OccupiedSlots { get; set; }
    public int MaxSlots { get; set; }
    public double OccupancyRate { get; set; }
}

public class QualitySummaryDto
{
    public int ReviewCount { get; set; }
    public double? AverageOverallScore { get; set; }
    public double? AverageHallScore { get; set; }
    public double? AverageFoodScore { get; set; }
    public double? AverageServiceScore { get; set; }
    public double? AverageSoundScore { get; set; }
    public double? AverageLightingScore { get; set; }
    // Trung bình điểm vệ sinh từ các phiếu đánh giá đã lọc (bỏ qua giá trị trống).
    public double? AverageHygieneScore { get; set; }
    public bool HygieneScoreAvailable { get; set; }
}

public class ReportHallOptionDto
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
}

public class RecentBookingDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int TableCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class HallScheduleMatrixDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<MatrixDayHeaderDto> Days { get; set; } = new();
    public List<HallMatrixRowDto> Halls { get; set; } = new();
}

public class MatrixDayHeaderDto
{
    public DateTime Date { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
    public bool IsWeekend { get; set; }
}

public class HallMatrixRowDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public string HallCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public List<HallMatrixSlotDto> Slots { get; set; } = new();
}

public class HallMatrixSlotDto
{
    public DateTime Date { get; set; }
    public string Shift { get; set; } = string.Empty; // "Ca trưa" hoặc "Ca tối"
    public int StatusId { get; set; }
    public string StatusCode { get; set; } = "AVAILABLE"; // "AVAILABLE", "BOOKED", "LOCKED"
    public string StatusText { get; set; } = "Trống";
    public string? BookingCode { get; set; }
    public string? CustomerName { get; set; }
    public int? TableCount { get; set; }
    public string? BookingStatus { get; set; }
}

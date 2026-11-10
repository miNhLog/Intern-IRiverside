using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Contract;

public sealed class ManagerContractFilterDto
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class ManagerContractStatisticsDto
{
    public int TotalContracts { get; set; }
    public int ActiveContracts { get; set; }
    public int AwaitingPaymentContracts { get; set; }
    public decimal TotalCollectedRevenue { get; set; }
}

public sealed class ManagerContractListResponseDto
{
    public List<ManagerContractListItemDto> Items { get; set; } = [];
    public ManagerContractStatisticsDto Statistics { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed class ManagerContractListItemDto
{
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public decimal TotalValue { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance => TotalValue - PaidAmount;
    public decimal PaymentPercentage => TotalValue > 0 ? PaidAmount / TotalValue * 100m : 0m;
    public bool HasPaymentInconsistency => PaidAmount > TotalValue;
}

public sealed class EligibleBookingDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string? MenuName { get; set; }
    public string? DecorationPackageName { get; set; }
    public List<string> Services { get; set; } = [];
    public decimal EstimatedTotal { get; set; }
    public string DefaultContractContent { get; set; } = string.Empty;
    public string DefaultPaymentTerms { get; set; } = string.Empty;
}

public sealed class CreateContractRequestDto
{
    [Range(1, int.MaxValue)] public int BookingId { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? TotalValue { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
}

public sealed class UpdateDraftContractRequestDto
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal TotalValue { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
}

public sealed class ManagerContractDetailDto
{
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public decimal TotalValue { get; set; }
    public string ContractContent { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance => TotalValue - PaidAmount;
    public decimal PaymentPercentage => TotalValue > 0 ? PaidAmount / TotalValue * 100m : 0m;
    public bool HasPaymentInconsistency => PaidAmount > TotalValue;
    public ManagerContractBookingDto Booking { get; set; } = new();
    public List<ManagerPaymentListItemDto> Payments { get; set; } = [];
    public List<ContractAuditEventDto> History { get; set; } = [];
}

public sealed class ManagerContractBookingDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public string? MenuName { get; set; }
    public string? DecorationPackageName { get; set; }
    public List<string> Services { get; set; } = [];
    public decimal EstimatedTotal { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class ManagerPaymentFilterDto
{
    public string? Keyword { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class ManagerPaymentListResponseDto
{
    public List<ManagerPaymentListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed class ManagerPaymentListItemDto
{
    public int PaymentId { get; set; }
    public string? TransactionCode { get; set; }
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
}

public sealed class ContractAuditEventDto
{
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
}

public sealed class ContractActionResponseDto
{
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

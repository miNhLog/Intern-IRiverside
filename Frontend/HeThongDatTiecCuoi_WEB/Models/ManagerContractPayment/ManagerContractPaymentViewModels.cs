using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment;

public sealed class ManagerContractPaymentPageViewModel
{
    public ManagerContractListResponseViewModel Contracts { get; set; } = new();
}

public sealed class ManagerContractListResponseViewModel
{
    public List<ManagerContractListItemViewModel> Items { get; set; } = [];
    public ManagerContractStatisticsViewModel Statistics { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ManagerContractStatisticsViewModel
{
    public int TotalContracts { get; set; }
    public int ActiveContracts { get; set; }
    public int AwaitingPaymentContracts { get; set; }
    public decimal TotalCollectedRevenue { get; set; }
}

public sealed class ManagerContractListItemViewModel
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
    public decimal RemainingBalance { get; set; }
    public decimal PaymentPercentage { get; set; }
    public bool HasPaymentInconsistency { get; set; }
}

public sealed class ManagerContractDetailViewModel
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
    public decimal RemainingBalance { get; set; }
    public decimal PaymentPercentage { get; set; }
    public bool HasPaymentInconsistency { get; set; }
    public ManagerContractBookingViewModel Booking { get; set; } = new();
    public List<ManagerPaymentListItemViewModel> Payments { get; set; } = [];
    public List<ContractAuditEventViewModel> History { get; set; } = [];
}

public sealed class ManagerContractBookingViewModel
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

public sealed class EligibleBookingViewModel
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

public sealed class CreateContractViewModel
{
    [Range(1, int.MaxValue)] public int BookingId { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? TotalValue { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
}

public sealed class UpdateDraftContractViewModel
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal TotalValue { get; set; }
    public string? ContractContent { get; set; }
    public string? PaymentTerms { get; set; }
}

public sealed class ContractActionResponseViewModel
{
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ManagerPaymentListResponseViewModel
{
    public List<ManagerPaymentListItemViewModel> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ManagerPaymentListItemViewModel
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

public sealed class ContractAuditEventViewModel
{
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
}

public sealed record PdfDownloadViewModel(byte[] Content, string FileName, string ContentType);

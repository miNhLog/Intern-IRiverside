using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment;

public sealed class RecordPaymentViewModel
{
    [Range(1, int.MaxValue)] public int ContractId { get; set; }
    public Guid RequestId { get; set; }
    [Required, StringLength(50)] public string PaymentType { get; set; } = string.Empty;
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public decimal Amount { get; set; }
    [Required, StringLength(50)] public string PaymentMethod { get; set; } = string.Empty;
    [StringLength(100)] public string? TransactionCode { get; set; }
}

public sealed class ConfirmDepositViewModel
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public decimal RequiredDepositAmount { get; set; }
}

public sealed class PaymentActionResponseViewModel
{
    public int PaymentId { get; set; }
    public bool Replayed { get; set; }
    public string Message { get; set; } = string.Empty;
}

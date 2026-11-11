namespace HeThongDatTiecCuoi_API.Models;

public sealed class Uc18RefundConfirmation
{
    public Guid RequestId { get; set; }
    public Guid OperationRequestId { get; set; }
    public int ActorUserId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string ExternalEvidenceReference { get; set; } = string.Empty;
    public DateTime ConfirmedAtUtc { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal DepositPrincipalPaid { get; set; }
    public decimal CompensationPaid { get; set; }

    public Uc18BookingOperation Operation { get; set; } = null!;
    public User Actor { get; set; } = null!;
}

namespace HeThongDatTiecCuoi_API.Models;

/// <summary>
/// Shared JSON envelope stored in Uc18BookingOperation.SnapshotJson for signed contract amendments.
/// Keep the default System.Text.Json property naming: the reschedule/menu-change writers use this shape.
/// </summary>
public sealed class Uc18ContractAmendmentSnapshot
{
    public Uc18ContractEventSnapshot? OriginalEvent { get; set; }
    public Uc18ContractEventSnapshot? PreviousEvent { get; set; }
    public Uc18ContractEventSnapshot? CurrentEvent { get; set; }
    public string? Reason { get; set; }
}

public sealed class Uc18ContractEventSnapshot
{
    public DateTime Date { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public string? MenuName { get; set; }
    public int Guests { get; set; }
    public string? DecorationPackageName { get; set; }
    public List<string> Services { get; set; } = [];
}

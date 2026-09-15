namespace HeThongDatTiecCuoi_API.Options;

public sealed class DevelopmentAccountsOptions
{
    public const string SectionName = "DevelopmentAccounts";

    public bool Enabled { get; init; }
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string StaffEmail { get; init; } = string.Empty;
    public string StaffPassword { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPassword { get; init; } = string.Empty;
}

namespace HeThongDatTiecCuoi_API.Options;

public sealed class DevelopmentAccountsOptions
{
    public const string SectionName = "DevelopmentAccounts";

    public bool Enabled { get; init; }
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string ManagerEmail { get; init; } = string.Empty;
    public string ManagerPassword { get; init; } = string.Empty;
    public string HallManagerEmail { get; init; } = string.Empty;
    public string HallManagerPassword { get; init; } = string.Empty;
    public string CoordinatorEmail { get; init; } = string.Empty;
    public string CoordinatorPassword { get; init; } = string.Empty;
    // Tương thích ngược với cấu hình cũ (Staff = Coordinator).
    public string StaffEmail { get; init; } = string.Empty;
    public string StaffPassword { get; init; } = string.Empty;
}

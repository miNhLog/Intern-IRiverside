namespace HeThongDatTiecCuoi_API.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 30;
}

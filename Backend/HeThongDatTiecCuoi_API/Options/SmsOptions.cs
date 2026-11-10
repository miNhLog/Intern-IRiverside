using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.Options;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = "Disabled";
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiKeyHeader { get; init; } = "X-Api-Key";
    public string SenderId { get; init; } = "Riverside";

    [RegularExpression("^[0-9]{6}$")]
    public string? DevelopmentCode { get; init; }
}


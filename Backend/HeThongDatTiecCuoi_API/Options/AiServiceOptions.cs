using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.Options;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    [Required, Url]
    public string BaseUrl { get; init; } = "http://127.0.0.1:8000/";

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 15;
}

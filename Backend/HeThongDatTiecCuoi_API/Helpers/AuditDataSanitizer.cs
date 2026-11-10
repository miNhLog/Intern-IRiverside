using System.Text.Json;

namespace HeThongDatTiecCuoi_API.Helpers;

public static class AuditDataSanitizer
{
    private static readonly string[] SensitiveFragments =
    [
        "password", "matkhau", "token", "otp", "securitycode", "authorization", "salt"
    ];

    public static string? Sanitize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(ToSafeValue(document.RootElement));
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static bool IsSensitive(string key)
    {
        var normalized = new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return SensitiveFragments.Any(normalized.Contains);
    }

    private static object? ToSafeValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var number) ? number : value.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => IsSensitive(property.Name) ? (object?)"[Đã ẩn]" : ToSafeValue(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(ToSafeValue).ToArray(),
        _ => null
    };
}

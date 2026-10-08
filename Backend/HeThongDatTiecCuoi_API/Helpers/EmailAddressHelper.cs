using System.Net.Mail;

namespace HeThongDatTiecCuoi_API.Helpers;

public static class EmailAddressHelper
{
    private static readonly IReadOnlyDictionary<string, string> CommonDomainTypos =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gmaiil.com"] = "gmail.com",
            ["gmaill.com"] = "gmail.com",
            ["gmial.com"] = "gmail.com",
            ["gmal.com"] = "gmail.com",
            ["gmail.co"] = "gmail.com",
            ["gmail.con"] = "gmail.com",
            ["gmail.cm"] = "gmail.com",
            ["hotmaiil.com"] = "hotmail.com",
            ["hotmal.com"] = "hotmail.com",
            ["outlok.com"] = "outlook.com",
            ["yahooo.com"] = "yahoo.com"
        };

    public static bool TryNormalize(string? value, out string normalized, out string error)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        error = string.Empty;

        if (normalized.Length == 0 || normalized.Length > 255 ||
            !MailAddress.TryCreate(normalized, out var parsed) ||
            !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            error = "Địa chỉ email không đúng định dạng.";
            return false;
        }

        var atIndex = normalized.LastIndexOf('@');
        var localPart = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        if (localPart.Length > 64 || !domain.Contains('.') ||
            domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains(".."))
        {
            error = "Địa chỉ email không đúng định dạng.";
            return false;
        }

        if (CommonDomainTypos.TryGetValue(domain, out var suggestedDomain))
        {
            error = $"Tên miền email '{domain}' có thể đã nhập sai. Bạn có muốn dùng '{suggestedDomain}'?";
            return false;
        }

        return true;
    }
}

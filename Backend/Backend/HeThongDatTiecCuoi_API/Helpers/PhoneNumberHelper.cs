using System.Text.RegularExpressions;

namespace HeThongDatTiecCuoi_API.Helpers;

public static partial class PhoneNumberHelper
{
    public static string Normalize(string value)
    {
        var digits = NonDigitRegex().Replace(value, string.Empty);

        return digits.StartsWith("84") && digits.Length == 11
            ? $"0{digits[2..]}"
            : digits;
    }

    [GeneratedRegex(@"[^0-9]")]
    private static partial Regex NonDigitRegex();
}

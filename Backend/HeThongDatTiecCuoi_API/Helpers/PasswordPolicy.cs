using System.Text.RegularExpressions;

namespace HeThongDatTiecCuoi_API.Helpers;

public static partial class PasswordPolicy
{
    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) && StrongPasswordRegex().IsMatch(password);

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$")]
    private static partial Regex StrongPasswordRegex();
}

using System.Text;

namespace HeThongDatTiecCuoi_API.Services;

public static class TextNormalization
{
    public static string Required(string? value) =>
        (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);

    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Normalize(NormalizationForm.FormC);
}

namespace HeThongDatTiecCuoi_WEB.Models.AdminMenu;

public sealed class ApiActionResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }

    public static ApiActionResult Success() => new() { Succeeded = true };

    public static ApiActionResult Failure(string error) =>
        new() { Succeeded = false, Error = error };
}

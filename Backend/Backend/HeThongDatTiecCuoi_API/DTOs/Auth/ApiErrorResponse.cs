namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed record ApiErrorResponse(string Message, IReadOnlyDictionary<string, string[]>? Errors = null);

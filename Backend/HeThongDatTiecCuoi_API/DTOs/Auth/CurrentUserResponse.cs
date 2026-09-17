namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed record CurrentUserResponse(
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Role,
    string Status);

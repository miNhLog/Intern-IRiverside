namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserResponse User);

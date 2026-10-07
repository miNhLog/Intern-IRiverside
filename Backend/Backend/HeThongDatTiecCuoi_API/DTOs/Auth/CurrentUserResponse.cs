namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed record CurrentUserResponse(
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string RoleName,
    string Status,
    bool MustChangePassword);

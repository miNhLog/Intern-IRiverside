namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed record CurrentUserDto(
    int NguoiDungId,
    string Email,
    string HoTen,
    string? SoDienThoai,
    string VaiTro,
    string TrangThai);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserDto User);

public sealed record ApiErrorDto(string Message, Dictionary<string, string[]>? Errors = null);

public sealed record ApiCallResult<T>(
    bool Succeeded,
    T? Value,
    string? Error,
    Dictionary<string, string[]>? Errors = null)
{
    public static ApiCallResult<T> Success(T value) => new(true, value, null, null);
    public static ApiCallResult<T> Failure(
        string error,
        Dictionary<string, string[]>? errors = null) =>
        new(false, default, error, errors);
}

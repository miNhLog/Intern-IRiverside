namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed record CurrentUserResponse(
    int NguoiDungId,
    string Email,
    string HoTen,
    string? SoDienThoai,
    string VaiTro,
    string TrangThai);

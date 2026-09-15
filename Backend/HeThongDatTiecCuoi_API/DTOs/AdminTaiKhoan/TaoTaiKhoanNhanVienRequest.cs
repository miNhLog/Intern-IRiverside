namespace HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;

public sealed class TaoTaiKhoanNhanVienRequest
{
    public int VaiTroID { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? SoDienThoai { get; set; }

    public string Email { get; set; } = string.Empty;

    public string MatKhau { get; set; } = string.Empty;
}
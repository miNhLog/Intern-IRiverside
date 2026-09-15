namespace HeThongDatTiecCuoi_API.DTOs.AdminTaiKhoan;

public sealed class TaiKhoanDto
{
    public int NguoiDungID { get; set; }

    public string Email { get; set; } = string.Empty;

    public int VaiTroID { get; set; }

    public string TenVaiTro { get; set; } = string.Empty;

    public string? HoTen { get; set; }

    public string? MaNhanVien { get; set; }

    public string? SoDienThoai { get; set; }

    public string TrangThai { get; set; } = string.Empty;

    public DateTime NgayTao { get; set; }
    public string? TrangThaiNhanVien { get; set; }
}
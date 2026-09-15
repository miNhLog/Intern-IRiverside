namespace HeThongDatTiecCuoi_API.Models;

public sealed class NhanVien
{
    public int NhanVienID { get; set; }
    public int NguoiDungID { get; set; }
    public string MaNhanVien { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string TrangThai { get; set; } = "Đang làm việc";

    public NguoiDung NguoiDung { get; set; } = null!;
}

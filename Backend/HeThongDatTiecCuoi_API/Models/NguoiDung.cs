namespace HeThongDatTiecCuoi_API.Models;

public sealed class NguoiDung
{
    public int NguoiDungID { get; set; }
    public int VaiTroID { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string TrangThai { get; set; } = "Hoạt động";
    public DateTime NgayTao { get; set; }

    public VaiTro VaiTro { get; set; } = null!;
    public KhachHang? KhachHang { get; set; }
    public NhanVien? NhanVien { get; set; }
}

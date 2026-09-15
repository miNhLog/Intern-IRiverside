namespace HeThongDatTiecCuoi_API.Models;

public sealed class KhachHang
{
    public int KhachHangID { get; set; }
    public int? NguoiDungID { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;

    public NguoiDung? NguoiDung { get; set; }
}

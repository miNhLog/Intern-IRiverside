namespace HeThongDatTiecCuoi_API.Models;

public sealed class ThanhToan
{
    public int ThanhToanID { get; set; }

    public int HopDongID { get; set; }

    public string LoaiThanhToan { get; set; } = string.Empty;

    public decimal SoTien { get; set; }

    public DateTime NgayThanhToan { get; set; }

    public string? PhuongThuc { get; set; }

    public string? MaGiaoDich { get; set; }

    public string TrangThai { get; set; } = string.Empty;

    public HopDong HopDong { get; set; } = null!;
}

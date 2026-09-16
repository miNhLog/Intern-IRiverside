namespace HeThongDatTiecCuoi_API.Models;

public sealed class DatTiec
{
    public int DatTiecID { get; set; }

    public string MaDatTiec { get; set; } = string.Empty;

    public int KhachHangID { get; set; }

    public int LichSanhID { get; set; }

    public int? ThucDonID { get; set; }

    public int? GoiTrangTriID { get; set; }

    public int? NhanVienTuVanID { get; set; }

    public decimal? NganSachDuKien { get; set; }

    public string? PhongCachMongMuon { get; set; }

    public int? SoLuongKhach { get; set; }

    public int? SoBan { get; set; }

    public decimal? GiaThucDonChot { get; set; }

    public decimal? GiaTrangTriChot { get; set; }

    public decimal? GiaSanhChot { get; set; }

    public decimal? TongTienDuKien { get; set; }

    public string? YeuCauDacBiet { get; set; }

    public string TrangThai { get; set; } = "Chờ xác nhận";

    public string? LyDoHuy { get; set; }

    public DateTime NgayDat { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public KhachHang KhachHang { get; set; } = null!;

    public LichSanh LichSanh { get; set; } = null!;

    public GoiTrangTri? GoiTrangTri { get; set; }

    public ICollection<DatTiecDichVu> DatTiecDichVus { get; set; } = new List<DatTiecDichVu>();
}

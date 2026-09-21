namespace HeThongDatTiecCuoi_API.Models;

public sealed class DanhGia
{
    public int DanhGiaID { get; set; }

    public int MaQRDanhGiaID { get; set; }

    public string LoaiNguoiDanhGia { get; set; } = string.Empty;

    public int? DiemSanh { get; set; }

    public int? DiemMonAn { get; set; }

    public int? DiemPhucVu { get; set; }

    public int? DiemAmThanh { get; set; }

    public int? DiemAnhSang { get; set; }

    public int? DiemVeSinh { get; set; }

    public int DiemTongThe { get; set; }

    public DateTime NgayDanhGia { get; set; }

    public MaQRDanhGia MaQRDanhGia { get; set; } = null!;
}

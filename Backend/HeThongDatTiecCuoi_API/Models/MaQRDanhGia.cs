namespace HeThongDatTiecCuoi_API.Models;

public sealed class MaQRDanhGia
{
    public int MaQRDanhGiaID { get; set; }

    public int DatTiecID { get; set; }

    public DatTiec DatTiec { get; set; } = null!;

    public ICollection<DanhGia> DanhGias { get; set; } = new List<DanhGia>();
}

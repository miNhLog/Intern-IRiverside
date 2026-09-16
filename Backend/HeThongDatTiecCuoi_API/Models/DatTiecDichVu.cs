namespace HeThongDatTiecCuoi_API.Models;

public sealed class DatTiecDichVu
{
    public int DatTiecDichVuID { get; set; }

    public int DatTiecID { get; set; }

    public int DichVuID { get; set; }

    public int SoLuong { get; set; } = 1;

    public decimal DonGiaChot { get; set; }

    public string? GhiChu { get; set; }

    public DatTiec DatTiec { get; set; } = null!;

    public DichVu DichVu { get; set; } = null!;
}

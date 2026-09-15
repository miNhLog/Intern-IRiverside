namespace HeThongDatTiecCuoi_API.Models;

public sealed class LichSanh
{
    public int LichSanhID { get; set; }

    public int SanhTiecID { get; set; }

    public DateTime Ngay { get; set; }

    public string CaToChuc { get; set; } = string.Empty;

    public string TrangThai { get; set; } = "Trống";

    public string? GhiChu { get; set; }

    public SanhTiec SanhTiec { get; set; } = null!;
}
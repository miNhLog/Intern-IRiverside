namespace HeThongDatTiecCuoi_API.Models;

public sealed class HopDong
{
    public int HopDongID { get; set; }

    public int DatTiecID { get; set; }

    public string MaHopDong { get; set; } = string.Empty;

    public DateTime NgayLap { get; set; }

    public decimal TongGiaTri { get; set; }

    public string TrangThai { get; set; } = string.Empty;

    public DatTiec DatTiec { get; set; } = null!;

    public ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}

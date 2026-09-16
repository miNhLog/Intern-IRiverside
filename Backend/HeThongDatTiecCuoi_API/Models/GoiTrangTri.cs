namespace HeThongDatTiecCuoi_API.Models;

public sealed class GoiTrangTri
{
    public int GoiTrangTriID { get; set; }

    public string MaGoi { get; set; } = string.Empty;

    public string TenGoi { get; set; } = string.Empty;

    public string? PhongCach { get; set; }

    public string? MoTa { get; set; }

    public decimal Gia { get; set; }

    public string? HinhAnh { get; set; }

    public string TrangThai { get; set; } = "Áp dụng";

    public ICollection<DatTiec> DatTiecs { get; set; } = new List<DatTiec>();
}

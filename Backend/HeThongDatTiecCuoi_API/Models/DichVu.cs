namespace HeThongDatTiecCuoi_API.Models;

public sealed class DichVu
{
    public int DichVuID { get; set; }

    public string MaDichVu { get; set; } = string.Empty;

    public string TenDichVu { get; set; } = string.Empty;

    public string? LoaiDichVu { get; set; }

    public string? MoTa { get; set; }

    public decimal Gia { get; set; }

    public string? HinhAnh { get; set; }

    public string TrangThai { get; set; } = "Áp dụng";

    public ICollection<DatTiecDichVu> DatTiecDichVus { get; set; } = new List<DatTiecDichVu>();
}

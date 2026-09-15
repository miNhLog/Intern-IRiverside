namespace HeThongDatTiecCuoi_API.Models;

public sealed class SanhTiec
{
    public int SanhTiecID { get; set; }
    public string MaSanh { get; set; } = string.Empty;
    public string TenSanh { get; set; } = string.Empty;
    public int? SucChuaToiThieu { get; set; }
    public int SucChuaToiDa { get; set; }
    public decimal GiaThue { get; set; }
    public string? MoTa { get; set; }
    public string? HinhAnh { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

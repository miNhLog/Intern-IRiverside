namespace HeThongDatTiecCuoi_WEB.Models.AdminSanh;

public sealed class QuanLySanhViewModel
{
    public List<SanhTiecDto> DanhSachSanh { get; set; } = new();

    public LichSanhTuanDto? LichTuan { get; set; }

    public DateTime NgayBatDau { get; set; }

    public int? SanhTiecId { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class SanhTiecDto
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

public sealed class LichSanhTuanDto
{
    public DateTime TuNgay { get; set; }

    public DateTime DenNgay { get; set; }

    public List<SanhLichDto> DanhSachSanh { get; set; } = new();
}

public sealed class SanhLichDto
{
    public int SanhTiecID { get; set; }

    public string MaSanh { get; set; } = string.Empty;

    public string TenSanh { get; set; } = string.Empty;

    public string TrangThaiSanh { get; set; } = string.Empty;

    public List<LichSanhSlotDto> Lich { get; set; } = new();
}

public sealed class LichSanhSlotDto
{
    public int LichSanhID { get; set; }

    public DateTime Ngay { get; set; }

    public string CaToChuc { get; set; } = string.Empty;

    public string TrangThai { get; set; } = string.Empty;

    public string? GhiChu { get; set; }

    public BookingLichDto? Booking { get; set; }
}

public sealed class BookingLichDto
{
    public int DatTiecID { get; set; }

    public string MaDatTiec { get; set; } = string.Empty;

    public string HoTenKhachHang { get; set; } = string.Empty;

    public int? SoBan { get; set; }

    public int? SoLuongKhach { get; set; }

    public string TrangThaiDatTiec { get; set; } = string.Empty;
}
public sealed class ActionResponseDto
{
    public string Message { get; set; } = string.Empty;
}
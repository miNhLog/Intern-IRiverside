using System.Text.Json.Serialization;

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
    [JsonPropertyName("hallId")]
    public int SanhTiecID { get; set; }

    [JsonPropertyName("hallCode")]
    public string MaSanh { get; set; } = string.Empty;

    [JsonPropertyName("hallName")]
    public string TenSanh { get; set; } = string.Empty;

    [JsonPropertyName("minCapacity")]
    public int? SucChuaToiThieu { get; set; }

    [JsonPropertyName("maxCapacity")]
    public int SucChuaToiDa { get; set; }

    [JsonPropertyName("rentalPrice")]
    public decimal GiaThue { get; set; }

    [JsonPropertyName("description")]
    public string? MoTa { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? HinhAnh { get; set; }

    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class LichSanhTuanDto
{
    [JsonPropertyName("startDate")]
    public DateTime TuNgay { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime DenNgay { get; set; }

    [JsonPropertyName("halls")]
    public List<SanhLichDto> DanhSachSanh { get; set; } = new();
}

public sealed class SanhLichDto
{
    [JsonPropertyName("hallId")]
    public int SanhTiecID { get; set; }

    [JsonPropertyName("hallCode")]
    public string MaSanh { get; set; } = string.Empty;

    [JsonPropertyName("hallName")]
    public string TenSanh { get; set; } = string.Empty;

    [JsonPropertyName("hallStatus")]
    public string TrangThaiSanh { get; set; } = string.Empty;

    [JsonPropertyName("slots")]
    public List<LichSanhSlotDto> Lich { get; set; } = new();
}

public sealed class LichSanhSlotDto
{
    [JsonPropertyName("scheduleId")]
    public int LichSanhID { get; set; }

    [JsonPropertyName("date")]
    public DateTime Ngay { get; set; }

    [JsonPropertyName("eventSession")]
    public string CaToChuc { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? GhiChu { get; set; }

    public BookingLichDto? Booking { get; set; }
}

public sealed class BookingLichDto
{
    [JsonPropertyName("bookingId")]
    public int DatTiecID { get; set; }

    [JsonPropertyName("bookingCode")]
    public string MaDatTiec { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string HoTenKhachHang { get; set; } = string.Empty;

    [JsonPropertyName("tableCount")]
    public int? SoBan { get; set; }

    [JsonPropertyName("guestCount")]
    public int? SoLuongKhach { get; set; }

    [JsonPropertyName("bookingStatus")]
    public string TrangThaiDatTiec { get; set; } = string.Empty;
}
public sealed class ActionResponseDto
{
    public string Message { get; set; } = string.Empty;
}

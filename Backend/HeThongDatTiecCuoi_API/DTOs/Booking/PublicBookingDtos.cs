using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Booking;

/// <summary>
/// UC14: Request tra cứu tiệc cưới trực tuyến (AllowAnonymous).
/// Map DatTiec.MaDatTiec + KhachHang.SoDienThoai.
/// </summary>
public sealed class PublicBookingLookupRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [RegularExpression(@"^DT-\d{8}-[A-Z0-9]{4,}$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại Việt Nam phải gồm đúng 10 số và bắt đầu bằng 0.")]
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// UC15: Request hủy giữ chỗ online, verify sở hữu bằng mã + SĐT.
/// </summary>
public sealed class PublicBookingCancelHoldRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập mã đặt tiệc.")]
    [RegularExpression(@"^DT-\d{8}-[A-Z0-9]{4,}$",
        ErrorMessage = "Mã đặt tiệc phải có dạng DT-yyyyMMdd-XXXX (ví dụ DT-20261012-0001).")]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại Việt Nam phải gồm đúng 10 số và bắt đầu bằng 0.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}

public sealed class PublicBookingDishDto
{
    public int DishId { get; set; }
    public string DishCode { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class PublicBookingServiceDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// UC14: Chi tiết hồ sơ tiệc cưới trả về cho khách tra cứu online.
/// Nguồn cột: DatTiec (MaDatTiec/SoLuongKhach/GiaThucDonChot/GiaTrangTriChot/GiaSanhChot/TongTienDuKien/NgayDat/LyDoHuy),
/// KhachHang (HoTen/SoDienThoai/Email), LichSanh (Ngay/CaToChuc) + SanhTiec (TenSanh/GiaThue/SucChuaToiDa),
/// ThucDon (TenThucDon/GiaMoiBan) + ChiTietThucDon/MonAn (7 món), GoiTrangTri (TenGoi/Gia/PhongCach),
/// DatTiec_DichVu + DichVu, HopDong (TongGiaTri) + ThanhToan (SoTien SUCCESS), TrangThai (TenTrangThai),
/// PhanCongDieuPhoi + NhanVien (tư vấn viên).
/// </summary>
public sealed class PublicBookingDetailDto
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    // Serialize yyyy-MM-dd để Frontend hiển thị đúng, tránh lệch giờ SQL date.
    public DateTime EventDate { get; set; }
    public string EventDateString { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;

    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public decimal HallPrice { get; set; }
    public int HallCapacity { get; set; }

    public int GuestCount { get; set; }
    public int OfficialTables { get; set; }
    public int SpareTables { get; set; }
    public int TotalTables { get; set; }

    public int? MenuId { get; set; }
    public string? MenuName { get; set; }
    public decimal? MenuPricePerTable { get; set; }
    public List<PublicBookingDishDto> Dishes { get; set; } = [];

    public int? DecorId { get; set; }
    public string? DecorName { get; set; }
    public decimal? DecorPrice { get; set; }
    public string? DecorStyle { get; set; }

    public List<PublicBookingServiceDto> Services { get; set; } = [];

    public decimal TotalAmount { get; set; }
    public decimal DepositPaid { get; set; }
    public decimal RemainingDebt { get; set; }

    // 5 trạng thái thực tế trong DB (TrangThai NhomTrangThai=BOOKING):
    // Chờ xác nhận / Đã xác nhận / Đang chuẩn bị / Hoàn tất / Đã hủy.
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public bool CanCancelOnline { get; set; }
    public DateTime HoldExpiresAt { get; set; }
    public DateTime BookedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? SpecialRequests { get; set; }

    public string? ConsultantName { get; set; }
    public string? ConsultantPhone { get; set; }
}

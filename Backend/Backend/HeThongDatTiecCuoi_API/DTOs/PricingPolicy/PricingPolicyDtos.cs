namespace HeThongDatTiecCuoi_API.DTOs.PricingPolicy
{
    // DTO cho Tab 1: Chính sách & Cọc (Ánh xạ từ điều khoản HopDong)
    public class PaymentPolicyDto
    {
        public int Id { get; set; }
        public string MaQuyDinh { get; set; } = string.Empty;
        public string TenDieuKhoan { get; set; } = string.Empty;
        public string PhanLoai { get; set; } = string.Empty;
        public string DinhMuc { get; set; } = string.Empty;
        public string ThoiHan { get; set; } = string.Empty;
        public string MoTa { get; set; } = string.Empty;
        public string CanCu { get; set; } = string.Empty;
        public string TrangThai { get; set; } = "Hiệu lực";
    }

    // DTO cho Tab 2: Đơn giá ca & Phụ thu (Tính toán trực tiếp từ bảng SanhTiec)
    public class HallShiftPricingDto
    {
        public int SanhTiecId { get; set; }
        public string MaSanh { get; set; } = string.Empty;
        public string TenSanh { get; set; } = string.Empty;
        public int? SucChuaToiThieu { get; set; }
        public int SucChuaToiDa { get; set; }
        public decimal GiaGoc { get; set; }           // Cột GiaThue trong bảng SanhTiec
        public decimal GiaCaTrua { get; set; }        // 100% GiaThue
        public decimal GiaCaToi { get; set; }         // GiaThue * 1.15 (+15%)
        public decimal GiaCuoiTuan { get; set; }      // GiaThue * 1.25 (+25%)
        public string TrangThai { get; set; } = "Hoạt động";
    }

    // DTO cập nhật giá sảnh
    public class UpdateHallPriceRequest
    {
        public decimal GiaThueMoi { get; set; }
    }

    // DTO cập nhật điều khoản mẫu
    public class UpdatePaymentPolicyRequest
    {
        public string TenDieuKhoan { get; set; } = string.Empty;
        public string DinhMuc { get; set; } = string.Empty;
        public string ThoiHan { get; set; } = string.Empty;
        public string MoTa { get; set; } = string.Empty;
    }
}
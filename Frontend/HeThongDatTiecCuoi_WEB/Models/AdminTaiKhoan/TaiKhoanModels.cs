namespace HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;


// ========================================
// TÀI KHOẢN TRẢ VỀ TỪ API
// ========================================
public sealed class TaiKhoanDto
{
    public int NguoiDungID { get; set; }

    public string Email { get; set; } = string.Empty;

    public int VaiTroID { get; set; }

    public string TenVaiTro { get; set; } = string.Empty;

    public string? HoTen { get; set; }

    public string? MaNhanVien { get; set; }

    public string? SoDienThoai { get; set; }

    // Trạng thái tài khoản NguoiDung:
    // Hoạt động / Tạm khóa / Ngừng hoạt động
    public string TrangThai { get; set; } = string.Empty;

    // Chỉ có với nhân viên:
    // Đang làm việc / Tạm nghỉ / Đã nghỉ việc
    public string? TrangThaiNhanVien { get; set; }

    public DateTime NgayTao { get; set; }
}


// ========================================
// VAI TRÒ
// ========================================
public sealed class VaiTroDto
{
    public int VaiTroID { get; set; }

    public string TenVaiTro { get; set; } = string.Empty;
}


// ========================================
// MODEL CHO TOÀN TRANG
// ========================================
public sealed class QuanLyTaiKhoanViewModel
{
    public List<TaiKhoanDto> DanhSachTaiKhoan { get; set; } = [];

    public List<VaiTroDto> DanhSachVaiTro { get; set; } = [];

    public string? TuKhoa { get; set; }

    public int? VaiTroID { get; set; }

    public string? TrangThai { get; set; }

    public string? Loi { get; set; }
}


// ========================================
// TẠO NHÂN VIÊN
// ========================================
public sealed class TaoTaiKhoanNhanVienRequest
{
    public int VaiTroID { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? SoDienThoai { get; set; }

    public string Email { get; set; } = string.Empty;

    public string MatKhau { get; set; } = string.Empty;
}


// ========================================
// CẬP NHẬT NHÂN VIÊN
// ========================================
public sealed class CapNhatTaiKhoanNhanVienRequest
{
    public int VaiTroID { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? SoDienThoai { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? TrangThaiNhanVien { get; set; }
}


// ========================================
// ĐẶT LẠI MẬT KHẨU
// ========================================
public sealed class DatLaiMatKhauRequest
{
    public string MatKhauMoi { get; set; } = string.Empty;
}


// ========================================
// ĐỔI TRẠNG THÁI TÀI KHOẢN
// ========================================
public sealed class CapNhatTrangThaiTaiKhoanRequest
{
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class ThaoTacTaiKhoanResponse
{
    public string Message { get; set; } = string.Empty;

    public int? NguoiDungID { get; set; }

    public string? Email { get; set; }

    public string? MaNhanVien { get; set; }

    public string? HoTen { get; set; }

    public string? SoDienThoai { get; set; }

    public string? VaiTro { get; set; }

    public string? TrangThai { get; set; }
}
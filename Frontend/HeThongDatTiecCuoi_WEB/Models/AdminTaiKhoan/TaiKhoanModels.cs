using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;


// ========================================
// TÀI KHOẢN TRẢ VỀ TỪ API
// ========================================
public sealed class TaiKhoanDto
{
    [JsonPropertyName("userId")]
    public int NguoiDungID { get; set; }

    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("roleId")]
    public int VaiTroID { get; set; }

    [JsonPropertyName("roleName")]
    public string TenVaiTro { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string? HoTen { get; set; }

    [JsonPropertyName("employeeCode")]
    public string? MaNhanVien { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? SoDienThoai { get; set; }

    // Trạng thái tài khoản NguoiDung:
    // Hoạt động / Tạm khóa / Ngừng hoạt động
    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;

    // Chỉ có với nhân viên:
    // Đang làm việc / Tạm nghỉ / Đã nghỉ việc
    [JsonPropertyName("employeeStatus")]
    public string? TrangThaiNhanVien { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime NgayTao { get; set; }
}


// ========================================
// VAI TRÒ
// ========================================
public sealed class VaiTroDto
{
    [JsonPropertyName("roleId")]
    public int VaiTroID { get; set; }

    [JsonPropertyName("roleName")]
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
    [JsonPropertyName("roleId")]
    public int VaiTroID { get; set; }

    [JsonPropertyName("fullName")]
    public string HoTen { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string? SoDienThoai { get; set; }

    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string MatKhau { get; set; } = string.Empty;
}


// ========================================
// CẬP NHẬT NHÂN VIÊN
// ========================================
public sealed class CapNhatTaiKhoanNhanVienRequest
{
    [JsonPropertyName("roleId")]
    public int VaiTroID { get; set; }

    [JsonPropertyName("fullName")]
    public string HoTen { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string? SoDienThoai { get; set; }

    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("employeeStatus")]
    public string? TrangThaiNhanVien { get; set; }
}


// ========================================
// ĐẶT LẠI MẬT KHẨU
// ========================================
public sealed class DatLaiMatKhauRequest
{
    [JsonPropertyName("newPassword")]
    public string MatKhauMoi { get; set; } = string.Empty;
}


// ========================================
// ĐỔI TRẠNG THÁI TÀI KHOẢN
// ========================================
public sealed class CapNhatTrangThaiTaiKhoanRequest
{
    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class ThaoTacTaiKhoanResponse
{
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public int? NguoiDungID { get; set; }

    public string? Email { get; set; }

    [JsonPropertyName("employeeCode")]
    public string? MaNhanVien { get; set; }

    [JsonPropertyName("fullName")]
    public string? HoTen { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? SoDienThoai { get; set; }

    [JsonPropertyName("role")]
    public string? VaiTro { get; set; }

    [JsonPropertyName("status")]
    public string? TrangThai { get; set; }

    [JsonPropertyName("accountStatus")]
    public string? TrangThaiTaiKhoan { get; set; }

    [JsonPropertyName("employeeStatus")]
    public string? TrangThaiNhanVien { get; set; }
}

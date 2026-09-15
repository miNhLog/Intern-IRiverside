using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [StringLength(150)]
    public string DinhDanh { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100)]
    public string MatKhau { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(Customer|Staff)$", ErrorMessage = "Loại tài khoản không hợp lệ.")]
    public string LoaiTaiKhoan { get; init; } = "Customer";

    public bool GhiNhoDangNhap { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [Display(Name = "Email hoặc số điện thoại")]
    public string DinhDanh { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = string.Empty;

    public string LoaiTaiKhoan { get; set; } = "Customer";
    public bool GhiNhoDangNhap { get; set; }
    public string? ReturnUrl { get; set; }
}

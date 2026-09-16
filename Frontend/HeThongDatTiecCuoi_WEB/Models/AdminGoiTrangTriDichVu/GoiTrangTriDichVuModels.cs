using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_WEB.Models.AdminGoiTrangTriDichVu;

public sealed class GoiTrangTriDto
{
    public int GoiTrangTriID { get; set; }
    public string MaGoi { get; set; } = string.Empty;
    public string TenGoi { get; set; } = string.Empty;
    public string? PhongCach { get; set; }
    public string? MoTa { get; set; }
    public decimal Gia { get; set; }
    public string? HinhAnh { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class DichVuDto
{
    public int DichVuID { get; set; }
    public string MaDichVu { get; set; } = string.Empty;
    public string TenDichVu { get; set; } = string.Empty;
    public string? LoaiDichVu { get; set; }
    public string? MoTa { get; set; }
    public decimal Gia { get; set; }
    public string? HinhAnh { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class GoiTrangTriFormModel
{
    [Required(ErrorMessage = "Mã gói không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã gói không được vượt quá 50 ký tự.")]
    public string MaGoi { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên gói không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên gói không được vượt quá 150 ký tự.")]
    public string TenGoi { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Phong cách không được vượt quá 100 ký tự.")]
    public string? PhongCach { get; set; }

    public string? MoTa { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Giá phải lớn hơn hoặc bằng 0.")]
    public decimal Gia { get; set; }

    public IFormFile? HinhAnhFile { get; set; }

    public bool XoaHinhAnh { get; set; }
}

public sealed class DichVuFormModel
{
    [Required(ErrorMessage = "Mã dịch vụ không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã dịch vụ không được vượt quá 50 ký tự.")]
    public string MaDichVu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên dịch vụ không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên dịch vụ không được vượt quá 150 ký tự.")]
    public string TenDichVu { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Loại dịch vụ không được vượt quá 100 ký tự.")]
    public string? LoaiDichVu { get; set; }

    public string? MoTa { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Giá phải lớn hơn hoặc bằng 0.")]
    public decimal Gia { get; set; }

    public IFormFile? HinhAnhFile { get; set; }

    public bool XoaHinhAnh { get; set; }
}

public sealed class CapNhatTrangThaiGoiTrangTriRequest
{
    [Required]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class CapNhatTrangThaiDichVuRequest
{
    [Required]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class QuanLyGoiTrangTriDichVuViewModel
{
    public List<GoiTrangTriDto> DanhSachGoiTrangTri { get; set; } = [];
    public List<DichVuDto> DanhSachDichVu { get; set; } = [];
    public string ActiveTab { get; set; } = "goi-trang-tri";

    public string? GoiTuKhoa { get; set; }
    public string? GoiPhongCach { get; set; }
    public string? GoiTrangThai { get; set; }
    public string? DichVuTuKhoa { get; set; }
    public string? DichVuLoai { get; set; }
    public string? DichVuTrangThai { get; set; }
    public string? LoiGoiTrangTri { get; set; }
    public string? LoiDichVu { get; set; }
}

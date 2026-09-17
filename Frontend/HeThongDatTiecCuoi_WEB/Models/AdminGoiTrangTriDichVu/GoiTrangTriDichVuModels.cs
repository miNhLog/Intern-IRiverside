using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_WEB.Models.AdminGoiTrangTriDichVu;

public sealed class GoiTrangTriDto
{
    [JsonPropertyName("packageId")]
    public int GoiTrangTriID { get; set; }
    [JsonPropertyName("packageCode")]
    public string MaGoi { get; set; } = string.Empty;
    [JsonPropertyName("packageName")]
    public string TenGoi { get; set; } = string.Empty;
    [JsonPropertyName("style")]
    public string? PhongCach { get; set; }
    [JsonPropertyName("description")]
    public string? MoTa { get; set; }
    [JsonPropertyName("price")]
    public decimal Gia { get; set; }
    [JsonPropertyName("imageUrl")]
    public string? HinhAnh { get; set; }
    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class DichVuDto
{
    [JsonPropertyName("serviceId")]
    public int DichVuID { get; set; }
    [JsonPropertyName("serviceCode")]
    public string MaDichVu { get; set; } = string.Empty;
    [JsonPropertyName("serviceName")]
    public string TenDichVu { get; set; } = string.Empty;
    [JsonPropertyName("serviceType")]
    public string? LoaiDichVu { get; set; }
    [JsonPropertyName("description")]
    public string? MoTa { get; set; }
    [JsonPropertyName("price")]
    public decimal Gia { get; set; }
    [JsonPropertyName("imageUrl")]
    public string? HinhAnh { get; set; }
    [JsonPropertyName("status")]
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
    [JsonPropertyName("status")]
    public string TrangThai { get; set; } = string.Empty;
}

public sealed class CapNhatTrangThaiDichVuRequest
{
    [Required]
    [JsonPropertyName("status")]
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

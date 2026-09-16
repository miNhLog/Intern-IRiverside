using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_API.DTOs.DichVu;

public sealed class TaoDichVuRequest
{
    [Required(ErrorMessage = "Mã dịch vụ không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã dịch vụ không được vượt quá 50 ký tự.")]
    public string MaDichVu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên dịch vụ không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên dịch vụ không được vượt quá 150 ký tự.")]
    public string TenDichVu { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Loại dịch vụ không được vượt quá 100 ký tự.")]
    public string? LoaiDichVu { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Đơn giá phải nằm trong khoảng từ 0 đến 9999999999999999,99.")]
    public decimal Gia { get; set; }

    public string? MoTa { get; set; }

    public IFormFile? HinhAnhFile { get; set; }
}

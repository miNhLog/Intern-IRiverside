using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_API.DTOs.GoiTrangTri;

public sealed class CreateDecorPackageRequest
{
    [Required(ErrorMessage = "Mã gói không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã gói không được vượt quá 50 ký tự.")]
    public string PackageCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên gói không được để trống.")]
    [StringLength(150, ErrorMessage = "Tên gói không được vượt quá 150 ký tự.")]
    public string PackageName { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Phong cách không được vượt quá 100 ký tự.")]
    public string? Style { get; set; }

    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Giá phải nằm trong khoảng từ 0 đến 9999999999999999,99.")]
    public decimal Price { get; set; }

    public IFormFile? ImageFile { get; set; }
}

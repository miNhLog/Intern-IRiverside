using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.GoiTrangTri;

public sealed class CapNhatTrangThaiGoiTrangTriRequest
{
    [Required(ErrorMessage = "Trạng thái gói không được để trống.")]
    [RegularExpression("^(Áp dụng|Ngừng áp dụng)$", ErrorMessage = "Trạng thái gói chỉ được là Áp dụng hoặc Ngừng áp dụng.")]
    public string TrangThai { get; set; } = string.Empty;
}

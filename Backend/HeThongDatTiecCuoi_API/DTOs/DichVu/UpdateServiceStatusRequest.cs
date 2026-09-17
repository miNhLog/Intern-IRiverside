using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.DichVu;

public sealed class UpdateServiceStatusRequest
{
    [Required(ErrorMessage = "Trạng thái dịch vụ không được để trống.")]
    [RegularExpression("^(Áp dụng|Ngừng áp dụng)$", ErrorMessage = "Trạng thái dịch vụ chỉ được là Áp dụng hoặc Ngừng áp dụng.")]
    public string Status { get; set; } = string.Empty;
}

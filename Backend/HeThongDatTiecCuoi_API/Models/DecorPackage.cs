namespace HeThongDatTiecCuoi_API.Models;

public sealed class DecorPackage
{
    public int GoiTrangTriID { get; set; }
    public string MaGoi { get; set; } = string.Empty;
    public string TenGoi { get; set; } = string.Empty;
    public string PhongCach { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public decimal Gia { get; set; }
    public int StatusId { get; set; }
    public byte DataStatusId { get; set; }
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<ImageAsset> Images { get; set; } = [];
}

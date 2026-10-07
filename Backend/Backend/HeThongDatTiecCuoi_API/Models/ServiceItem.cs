namespace HeThongDatTiecCuoi_API.Models;

public sealed class ServiceItem
{
    public int DichVuID { get; set; }
    public string MaDichVu { get; set; } = string.Empty;
    public string TenDichVu { get; set; } = string.Empty;
    public string? LoaiDichVu { get; set; }
    public string? MoTa { get; set; }
    public decimal Gia { get; set; }
    public int StatusId { get; set; }
    public byte DataStatusId { get; set; }
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
}

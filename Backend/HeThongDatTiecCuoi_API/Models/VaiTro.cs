namespace HeThongDatTiecCuoi_API.Models;

public sealed class VaiTro
{
    public int VaiTroID { get; set; }
    public string TenVaiTro { get; set; } = string.Empty;
    public ICollection<NguoiDung> NguoiDungs { get; set; } = new List<NguoiDung>();
}

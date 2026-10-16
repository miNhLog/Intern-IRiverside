using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongDatTiecCuoi_API.Models;

public sealed class Hall
{
    public int HallId { get; set; }
    public string HallCode { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    /// <summary>Số khách tối thiểu (cột SucChuaToiThieu). NULL nghĩa là không giới hạn dưới.</summary>
    public int? MinimumCapacity { get; set; }
    /// <summary>Số khách tối đa (cột SucChuaToiDa).</summary>
    public int MaximumCapacity { get; set; }
    public decimal RentalPrice { get; set; }
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public byte DataStatusId { get; set; }
    public Status Status { get; set; } = null!;
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<HallSchedule> Schedules { get; set; } = [];
    public ICollection<ImageAsset> Images { get; set; } = [];
    public ICollection<HallManagerAssignment> HallManagerAssignments { get; set; } = [];

    // Quy chuẩn 1 bàn = 10 khách. SucChuaToiThieu/SucChuaToiDa lưu SỐ KHÁCH.
    // NULL fallback về 150 khách (15 bàn) theo quy chuẩn đồng bộ Recommendation.
    [NotMapped]
    public int CapacityMinGuests => MinimumCapacity ?? 150;
    [NotMapped]
    public int CapacityMaxGuests => MaximumCapacity;
    [NotMapped]
    public int CapacityMinTables => CapacityMinGuests / 10;
    [NotMapped]
    public int CapacityMaxTables => CapacityMaxGuests / 10;
}

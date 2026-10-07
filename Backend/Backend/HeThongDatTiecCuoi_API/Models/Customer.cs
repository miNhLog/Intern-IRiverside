namespace HeThongDatTiecCuoi_API.Models;

public sealed class Customer
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte DataStatusId { get; set; }

    public User? User { get; set; }
    public DataStatus DataStatus { get; set; } = null!;
    public ICollection<Menu> Menus { get; set; } = [];
}

namespace HeThongDatTiecCuoi_API.Models;

public sealed class Role
{
    public byte RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = new List<User>();
}

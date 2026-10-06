namespace HeThongDatTiecCuoi_API.Models;

public sealed class Status
{
    public int StatusId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string StatusGroup { get; set; } = string.Empty;
    public string? Description { get; set; }
}

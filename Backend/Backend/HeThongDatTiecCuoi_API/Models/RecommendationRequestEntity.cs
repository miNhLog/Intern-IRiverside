namespace HeThongDatTiecCuoi_API.Models;

public sealed class RecommendationRequestEntity
{
    public long RecommendationRequestId { get; set; }
    public int? CustomerId { get; set; }
    public decimal ExpectedBudget { get; set; }
    public int GuestCount { get; set; }
    public DateTime DesiredDate { get; set; }
    public string DesiredShift { get; set; } = string.Empty;
    public string DesiredStyle { get; set; } = string.Empty;
    public string? ServiceNeeds { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer? Customer { get; set; }
}

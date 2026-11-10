namespace HeThongDatTiecCuoi_API.Options;

public sealed class ReviewQrOptions
{
    public const string SectionName = "ReviewQr";
    public bool PaymentGateEnabled { get; set; }
    public string PublicBaseUrl { get; set; } = "https://localhost:7002";
    public int CommentMaxLength { get; set; } = 1000;
}

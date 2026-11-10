namespace HeThongDatTiecCuoi_API.Options;

public sealed class PayOSOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public bool IsDemoMode { get; set; } = true;
    public int DemoScaleRatio { get; set; } = 10000;
    public string ReturnUrl { get; set; } = "https://localhost:7102/Payment/Success";
    public string CancelUrl { get; set; } = "https://localhost:7102/Payment/Cancel";
    public string ScheduleChangeReturnUrl { get; set; } = "https://localhost:7102/ScheduleChange/PaymentResult";
    public string ScheduleChangeCancelUrl { get; set; } = "https://localhost:7102/ScheduleChange/PaymentCancelled";
}

namespace HeThongDatTiecCuoi_API.Constants.StatusCodes;

public static class BookingStatusCodes
{
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Preparing = "PREPARING";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All =
    [
        Pending,
        Confirmed,
        Preparing,
        Completed,
        Cancelled
    ];

    public static readonly string[] OccupyingSchedule =
    [
        Pending,
        Confirmed,
        Preparing
    ];
}

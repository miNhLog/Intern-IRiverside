namespace HeThongDatTiecCuoi_API.Services;

public sealed class PolicyStoreException : Exception
{
    public PolicyStoreException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static PolicyStoreException Unavailable(Exception? innerException = null) =>
        new(
            "The current policy file could not be read or written.",
            StatusCodes.Status503ServiceUnavailable,
            innerException);

    public static PolicyStoreException Conflict() =>
        new(
            "The policy was updated by another request. Reload it and try again.",
            StatusCodes.Status409Conflict);
}

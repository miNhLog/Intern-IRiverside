namespace HeThongDatTiecCuoi_API.Services;

public sealed class ServiceResult<T>
{
    private ServiceResult(bool succeeded, T? value, string? error, int statusCode)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    public static ServiceResult<T> Success(T value, int statusCode = StatusCodes.Status200OK) =>
        new(true, value, null, statusCode);

    public static ServiceResult<T> Failure(string error, int statusCode) =>
        new(false, default, error, statusCode);
}

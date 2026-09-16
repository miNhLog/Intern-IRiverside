using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_API.Services;

public enum ImageDomain
{
    GoiDecor,
    DichVu
}

public interface IImageStorage
{
    Task<string> SaveAsync(
        IFormFile file,
        ImageDomain domain,
        CancellationToken cancellationToken);

    Task<bool> DeleteIfManagedAsync(
        string? publicPath,
        CancellationToken cancellationToken);
}

public sealed class ImageStorageValidationException : Exception
{
    public ImageStorageValidationException(string message)
        : base(message)
    {
    }
}

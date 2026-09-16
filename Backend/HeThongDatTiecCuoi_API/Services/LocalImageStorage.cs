using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class LocalImageStorage : IImageStorage
{
    private static readonly byte[] PngSignature =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    ];

    private readonly ImageStorageOptions _options;
    private readonly ILogger<LocalImageStorage> _logger;
    private readonly string _rootPath;

    public LocalImageStorage(
        IOptions<ImageStorageOptions> options,
        ILogger<LocalImageStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
        _rootPath = Path.GetFullPath(_options.RootPath);

        Directory.CreateDirectory(_rootPath);
        EnsureWritable(_rootPath);
    }

    public async Task<string> SaveAsync(
        IFormFile file,
        ImageDomain domain,
        CancellationToken cancellationToken)
    {
        ValidateFileMetadata(file);

        await using var source = file.OpenReadStream();
        await using var input = new MemoryStream();
        await source.CopyToAsync(input, cancellationToken);
        var bytes = input.ToArray();

        ValidateSignature(bytes, Path.GetExtension(file.FileName));

        input.Position = 0;
        using var image = await LoadAndValidateImageAsync(input, cancellationToken);
        image.Mutate(context => context.AutoOrient());
        RemoveMetadata(image);

        var domainSegment = GetDomainSegment(domain);
        var uploadDirectory = Path.Combine(_rootPath, domainSegment, "uploads");
        Directory.CreateDirectory(uploadDirectory);

        var id = Guid.NewGuid().ToString("N");
        var tempPath = Path.Combine(uploadDirectory, $"{id}.tmp");
        var finalPath = Path.Combine(uploadDirectory, $"{id}.webp");
        var publicPath = $"{_options.PublicBasePath.TrimEnd('/')}/{domainSegment}/uploads/{id}.webp";

        try
        {
            await image.SaveAsWebpAsync(
                tempPath,
                new WebpEncoder { Quality = 82 },
                cancellationToken);

            await FlushFileAsync(tempPath, cancellationToken);
            File.Move(tempPath, finalPath);

            var hash = await ComputeHashAsync(finalPath, cancellationToken);
            _logger.LogInformation(
                "Stored managed image {Domain} at {PublicPath}; bytes {Bytes}, dimensions {Width}x{Height}, SHA256 {Sha256}",
                domain,
                publicPath,
                new FileInfo(finalPath).Length,
                image.Width,
                image.Height,
                hash);

            return publicPath;
        }
        catch
        {
            DeleteFileQuietly(tempPath);
            DeleteFileQuietly(finalPath);
            throw;
        }
    }

    public Task<bool> DeleteIfManagedAsync(
        string? publicPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolveManagedPath(publicPath, out var fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(true);
            }

            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not delete managed image at {PublicPath}", publicPath);
            return Task.FromResult(false);
        }
    }

    private static void ValidateFileMetadata(IFormFile file)
    {
        if (file.Length <= 0 || file.Length >= ImageStorageOptions.MaxFileBytes)
        {
            throw new ImageStorageValidationException(
                "Hình ảnh phải có dung lượng lớn hơn 0 và nhỏ hơn 5 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType.ToLowerInvariant();
        var isAllowed = extension switch
        {
            ".jpg" or ".jpeg" => contentType == "image/jpeg",
            ".png" => contentType == "image/png",
            ".webp" => contentType == "image/webp",
            _ => false
        };

        if (!isAllowed)
        {
            throw new ImageStorageValidationException(
                "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP với MIME tương ứng.");
        }
    }

    private static void ValidateSignature(byte[] bytes, string extension)
    {
        var normalizedExtension = extension.ToLowerInvariant();
        var valid = normalizedExtension switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3
                && bytes[0] == 0xFF
                && bytes[1] == 0xD8
                && bytes[2] == 0xFF,
            ".png" => StartsWith(bytes, PngSignature),
            ".webp" => bytes.Length >= 12
                && bytes[0] == (byte)'R'
                && bytes[1] == (byte)'I'
                && bytes[2] == (byte)'F'
                && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W'
                && bytes[9] == (byte)'E'
                && bytes[10] == (byte)'B'
                && bytes[11] == (byte)'P',
            _ => false
        };

        if (!valid)
        {
            throw new ImageStorageValidationException(
                "Nội dung tệp không khớp với định dạng hình ảnh đã chọn.");
        }
    }

    private static async Task<Image<Rgba32>> LoadAndValidateImageAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);

            if (image.Width <= 0
                || image.Height <= 0
                || image.Width > ImageStorageOptions.MaxDimension
                || image.Height > ImageStorageOptions.MaxDimension
                || (long)image.Width * image.Height > ImageStorageOptions.MaxPixels)
            {
                throw new ImageStorageValidationException(
                    "Kích thước hình ảnh vượt quá giới hạn cho phép 8192x8192 và 40 triệu điểm ảnh.");
            }

            if (image.Frames.Count > 1)
            {
                throw new ImageStorageValidationException(
                    "Không hỗ trợ hình ảnh động hoặc tệp có nhiều frame.");
            }

            return image.Clone();
        }
        catch (ImageStorageValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidImageContentException or UnknownImageFormatException)
        {
            throw new ImageStorageValidationException(
                "Không thể giải mã nội dung hình ảnh hợp lệ.");
        }
    }

    private static void RemoveMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
    }

    private static Task FlushFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            4096,
            FileOptions.SequentialScan);
        stream.Flush(true);
        return Task.CompletedTask;
    }

    private static async Task<string> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private bool TryResolveManagedPath(string? publicPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(publicPath)
            || publicPath.Contains("..", StringComparison.Ordinal)
            || Uri.TryCreate(publicPath, UriKind.Absolute, out _))
        {
            return false;
        }

        var prefix = _options.PublicBasePath.TrimEnd('/') + "/";
        if (!publicPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = publicPath[prefix.Length..];
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || parts[1] != "uploads"
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[2]), "N", out _)
            || Path.GetExtension(parts[2]) != ".webp"
            || parts[0] is not ("goi-decor" or "dich-vu"))
        {
            return false;
        }

        fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            parts[0],
            parts[1],
            parts[2]));

        return IsWithinRoot(fullPath, _rootPath);
    }

    private static string GetDomainSegment(ImageDomain domain) =>
        domain switch
        {
            ImageDomain.GoiDecor => "goi-decor",
            ImageDomain.DichVu => "dich-vu",
            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
        };

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length));

    private static bool IsWithinRoot(string path, string root)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureWritable(string path)
    {
        var probePath = Path.Combine(path, $".write-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(probePath))
            {
            }
        }
        finally
        {
            DeleteFileQuietly(probePath);
        }
    }

    private static void DeleteFileQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort after a failed write or database operation.
        }
    }
}

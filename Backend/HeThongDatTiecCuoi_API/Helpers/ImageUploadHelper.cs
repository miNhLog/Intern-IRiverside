using Microsoft.AspNetCore.Http;

namespace HeThongDatTiecCuoi_API.Helpers;

public static class ImageUploadHelper
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const long MaxRequestSize = MaxFileSize + 64 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "Vui lòng chọn tệp ảnh.";
        if (file.Length > MaxFileSize)
            return "Ảnh không được vượt quá 5 MB.";

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.TryGetValue(extension, out var expectedContentType))
            return "Chỉ hỗ trợ ảnh JPG, JPEG, PNG hoặc WEBP.";
        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase) &&
            !(extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
              file.ContentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)))
            return "Định dạng nội dung của tệp ảnh không hợp lệ.";

        return null;
    }

    public static async Task<string> SaveAsync(
        IFormFile file,
        string webRootPath,
        string folder,
        string entityKey,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(webRootPath, "uploads", folder);
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{entityKey}-{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(directory, fileName);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(output, cancellationToken);
        DeleteManagedFiles(directory, entityKey, destination);

        return $"/uploads/{folder}/{fileName}";
    }

    public static string? FindManagedUrl(string webRootPath, string folder, string entityKey)
    {
        var directory = Path.Combine(webRootPath, "uploads", folder);
        if (!Directory.Exists(directory)) return null;

        var path = Directory.EnumerateFiles(directory, $"{entityKey}-*", SearchOption.TopDirectoryOnly)
            .Where(file => AllowedExtensions.ContainsKey(Path.GetExtension(file)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return path is null ? null : $"/uploads/{folder}/{Path.GetFileName(path)}";
    }

    public static void DeleteManagedUrl(string? imageUrl, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
        var uploadRoot = Path.GetFullPath(Path.Combine(webRootPath, "uploads")) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private static void DeleteManagedFiles(string directory, string entityKey, string keepPath)
    {
        foreach (var path in Directory.EnumerateFiles(directory, $"{entityKey}-*", SearchOption.TopDirectoryOnly))
        {
            if (!path.Equals(keepPath, StringComparison.OrdinalIgnoreCase) &&
                AllowedExtensions.ContainsKey(Path.GetExtension(path)))
                File.Delete(path);
        }
    }
}

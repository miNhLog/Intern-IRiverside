using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.DecorService;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/decor-packages")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class DecorPackagesController : ControllerBase
{
    private const int DefaultPageSize = 6;
    private const int MaximumPageSize = 20;
    private const int MaximumKeywordLength = 200;

    private readonly ApplicationDbContext _context;

    public DecorPackagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicDecorPackages(
        [FromQuery] string? keyword,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new { message = "Số trang không hợp lệ." });

        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
            return BadRequest(new { message = "Số trang vượt quá giới hạn." });

        var normalizedKeyword = keyword?.Trim();
        if (normalizedKeyword?.Length > MaximumKeywordLength)
            return BadRequest(new { message = "Từ khóa tìm kiếm vượt quá độ dài cho phép." });

        var normalizedSort = string.IsNullOrWhiteSpace(sort)
            ? "name-asc"
            : sort.Trim().ToLowerInvariant();

        if (normalizedSort is not ("name-asc" or "price-asc" or "price-desc"))
            return BadRequest(new { message = "Tùy chọn sắp xếp không hợp lệ." });

        var query = GetPublicPackages();

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(package =>
                package.TenGoi.Contains(normalizedKeyword) ||
                package.PhongCach.Contains(normalizedKeyword));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var orderedQuery = normalizedSort switch
        {
            "price-asc" => query.OrderBy(package => package.Gia)
                .ThenBy(package => package.TenGoi)
                .ThenBy(package => package.GoiTrangTriID),

            "price-desc" => query.OrderByDescending(package => package.Gia)
                .ThenBy(package => package.TenGoi)
                .ThenBy(package => package.GoiTrangTriID),

            _ => query.OrderBy(package => package.TenGoi)
                .ThenBy(package => package.GoiTrangTriID)
        };

        var items = await orderedQuery
            .Skip((int)offset)
            .Take(pageSize)
            .Select(package => new PublicDecorPackageDto
            {
                DecorationPackageId = package.GoiTrangTriID,
                PackageName = package.TenGoi,
                Style = package.PhongCach,
                Description = package.MoTa,
                Price = package.Gia,
                ImageUrl = package.Images
                    .Where(image => image.IsPrimary &&
                        image.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .OrderBy(image => image.SortOrder)
                    .ThenBy(image => image.ImageId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            item.ImageUrl = ToSafePublicImageUrl(item.ImageUrl);

        return Ok(new PublicDecorPackageListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize)
        });
    }

    [HttpGet("public/{decorationPackageId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicDecorPackage(
        int decorationPackageId,
        CancellationToken cancellationToken)
    {
        var package = await GetPublicPackages()
            .Where(item => item.GoiTrangTriID == decorationPackageId)
            .Select(item => new PublicDecorPackageDto
            {
                DecorationPackageId = item.GoiTrangTriID,
                PackageName = item.TenGoi,
                Style = item.PhongCach,
                Description = item.MoTa,
                Price = item.Gia,
                ImageUrl = item.Images
                    .Where(image => image.IsPrimary &&
                        image.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .OrderBy(image => image.SortOrder)
                    .ThenBy(image => image.ImageId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (package is null)
            return NotFound(new { message = "Không tìm thấy gói trang trí." });

        package.ImageUrl = ToSafePublicImageUrl(package.ImageUrl);

        return Ok(package);
    }

    private IQueryable<DecorPackage> GetPublicPackages() =>
        _context.DecorPackages
            .AsNoTracking()
            .Where(package =>
                package.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                package.Status.StatusGroup == StatusGroups.DecorationPackage &&
                package.Status.StatusCode == BusinessStatusCodes.Active);

    private static string? ToSafePublicImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var value = imageUrl.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.Scheme is "http" or "https" &&
                   !string.IsNullOrWhiteSpace(absoluteUri.Host) &&
                   string.IsNullOrEmpty(absoluteUri.UserInfo)
                ? absoluteUri.ToString()
                : null;
        }

        if (value.Contains('\\') ||
            value.Contains('?') ||
            value.Contains('#') ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Any(char.IsControl))
            return null;

        var path = value.TrimStart('/');

        if (!path.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var decodedPath = Uri.UnescapeDataString(path);

        return decodedPath.Split('/').Any(segment => segment is "." or "..")
            ? null
            : value;
    }
}
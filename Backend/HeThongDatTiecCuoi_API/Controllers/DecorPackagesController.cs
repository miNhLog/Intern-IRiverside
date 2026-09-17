using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.GoiTrangTri;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/decor-packages")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class DecorPackagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;

    public DecorPackagesController(
        ApplicationDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string? keyword,
        string? style,
        string? status,
        CancellationToken cancellationToken)
    {
        keyword = NormalizeOptional(keyword);
        style = NormalizeOptional(style);
        status = NormalizeOptional(status);

        if (status is not null && !IsValidStatus(status))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái gói không hợp lệ."));
        }

        var query = _context.GoiTrangTri.AsNoTracking();

        if (keyword is not null)
        {
            query = query.Where(x =>
                x.MaGoi.Contains(keyword) ||
                x.TenGoi.Contains(keyword) ||
                (x.MoTa != null && x.MoTa.Contains(keyword)));
        }

        if (style is not null)
        {
            query = query.Where(x => x.PhongCach == style);
        }

        if (status is not null)
        {
            query = query.Where(x => x.TrangThai == status);
        }

        var danhSach = await query
            .OrderBy(x => x.GoiTrangTriID)
            .Select(x => new DecorPackageDto
            {
                PackageId = x.GoiTrangTriID,
                PackageCode = x.MaGoi,
                PackageName = x.TenGoi,
                Style = x.PhongCach,
                Description = x.MoTa,
                Price = x.Gia,
                ImageUrl = x.HinhAnh,
                Status = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return Ok(danhSach.Select(NormalizeDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var goi = await _context.GoiTrangTri
            .AsNoTracking()
            .Where(x => x.GoiTrangTriID == id)
            .Select(x => new DecorPackageDto
            {
                PackageId = x.GoiTrangTriID,
                PackageCode = x.MaGoi,
                PackageName = x.TenGoi,
                Style = x.PhongCach,
                Description = x.MoTa,
                Price = x.Gia,
                ImageUrl = x.HinhAnh,
                Status = x.TrangThai
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (goi is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy gói trang trí."));
        }

        return Ok(NormalizeDto(goi));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreateDecorPackageRequest request,
        CancellationToken cancellationToken)
    {
        var maGoi = NormalizeText(request.PackageCode);

        if (await _context.GoiTrangTri.AnyAsync(
                x => x.MaGoi == maGoi,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }

        string? imagePath = null;
        try
        {
            imagePath = request.ImageFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.ImageFile,
                    ImageDomain.GoiDecor,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        var goi = new GoiTrangTri
        {
            MaGoi = maGoi,
            TenGoi = NormalizeText(request.PackageName),
            PhongCach = NormalizeOptional(request.Style),
            MoTa = NormalizeOptional(request.Description),
            Gia = request.Price,
            HinhAnh = imagePath,
            TrangThai = "Áp dụng"
        };

        _context.GoiTrangTri.Add(goi);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await DeleteQuietlyAsync(imagePath, cancellationToken);
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }
        catch
        {
            await DeleteQuietlyAsync(imagePath, cancellationToken);
            throw;
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = goi.GoiTrangTriID },
            ToDto(goi));
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] UpdateDecorPackageRequest request,
        CancellationToken cancellationToken)
    {
        var goi = await _context.GoiTrangTri
            .FirstOrDefaultAsync(x => x.GoiTrangTriID == id, cancellationToken);

        if (goi is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy gói trang trí."));
        }

        var maGoi = NormalizeText(request.PackageCode);

        if (await _context.GoiTrangTri.AnyAsync(
                x => x.MaGoi == maGoi && x.GoiTrangTriID != id,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }

        if (request.ImageFile is not null && request.RemoveImage)
        {
            return BadRequest(new ApiErrorResponse(
                "Không thể vừa tải ảnh mới vừa gỡ ảnh hiện tại.",
                new Dictionary<string, string[]>
                {
                    ["imageFile"] = ["Chỉ chọn một trong hai thao tác tải mới hoặc gỡ ảnh."]
                }));
        }

        var oldImagePath = goi.HinhAnh;
        string? newImagePath = null;
        try
        {
            newImagePath = request.ImageFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.ImageFile,
                    ImageDomain.GoiDecor,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        goi.MaGoi = maGoi;
        goi.TenGoi = NormalizeText(request.PackageName);
        goi.PhongCach = NormalizeOptional(request.Style);
        goi.MoTa = NormalizeOptional(request.Description);
        goi.Gia = request.Price;
        goi.HinhAnh = newImagePath ?? (request.RemoveImage ? null : oldImagePath);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await DeleteQuietlyAsync(newImagePath, cancellationToken);
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }
        catch
        {
            await DeleteQuietlyAsync(newImagePath, cancellationToken);
            throw;
        }

        if (!string.Equals(oldImagePath, goi.HinhAnh, StringComparison.Ordinal))
        {
            await DeleteQuietlyAsync(oldImagePath, cancellationToken);
        }

        return Ok(ToDto(goi));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateDecorPackageStatusRequest request,
        CancellationToken cancellationToken)
    {
        var goi = await _context.GoiTrangTri
            .FirstOrDefaultAsync(x => x.GoiTrangTriID == id, cancellationToken);

        if (goi is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy gói trang trí."));
        }

        if (!IsValidStatus(request.Status))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái gói chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        goi.TrangThai = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(goi));
    }

    private static DecorPackageDto ToDto(GoiTrangTri goi) => new()
    {
        PackageId = goi.GoiTrangTriID,
        PackageCode = NormalizeText(goi.MaGoi),
        PackageName = NormalizeText(goi.TenGoi),
        Style = NormalizeOptional(goi.PhongCach),
        Description = NormalizeOptional(goi.MoTa),
        Price = goi.Gia,
        ImageUrl = NormalizeOptional(goi.HinhAnh),
        Status = NormalizeText(goi.TrangThai)
    };

    private static DecorPackageDto NormalizeDto(DecorPackageDto goi) => new()
    {
        PackageId = goi.PackageId,
        PackageCode = NormalizeText(goi.PackageCode),
        PackageName = NormalizeText(goi.PackageName),
        Style = NormalizeOptional(goi.Style),
        Description = NormalizeOptional(goi.Description),
        Price = goi.Price,
        ImageUrl = NormalizeOptional(goi.ImageUrl),
        Status = NormalizeText(goi.Status)
    };

    private static string NormalizeText(string value) =>
        value.Normalize(NormalizationForm.FormC);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Normalize(NormalizationForm.FormC);

    private static bool IsValidStatus(string value) =>
        value is "Áp dụng" or "Ngừng áp dụng";

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ApiErrorResponse ToImageError(ImageStorageValidationException exception) =>
        new(
            exception.Message,
            new Dictionary<string, string[]>
            {
                ["imageFile"] = [exception.Message]
            });

    private async Task DeleteQuietlyAsync(
        string? imagePath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            await _imageStorage.DeleteIfManagedAsync(imagePath, cancellationToken);
        }
    }
}

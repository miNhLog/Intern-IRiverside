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
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class GoiTrangTriController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;

    public GoiTrangTriController(
        ApplicationDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string? tuKhoa,
        string? phongCach,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        tuKhoa = NormalizeOptional(tuKhoa);
        phongCach = NormalizeOptional(phongCach);
        trangThai = NormalizeOptional(trangThai);

        if (trangThai is not null && !IsValidStatus(trangThai))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái gói không hợp lệ."));
        }

        var query = _context.GoiTrangTri.AsNoTracking();

        if (tuKhoa is not null)
        {
            query = query.Where(x =>
                x.MaGoi.Contains(tuKhoa) ||
                x.TenGoi.Contains(tuKhoa) ||
                (x.MoTa != null && x.MoTa.Contains(tuKhoa)));
        }

        if (phongCach is not null)
        {
            query = query.Where(x => x.PhongCach == phongCach);
        }

        if (trangThai is not null)
        {
            query = query.Where(x => x.TrangThai == trangThai);
        }

        var danhSach = await query
            .OrderBy(x => x.GoiTrangTriID)
            .Select(x => new GoiTrangTriDto
            {
                GoiTrangTriID = x.GoiTrangTriID,
                MaGoi = x.MaGoi,
                TenGoi = x.TenGoi,
                PhongCach = x.PhongCach,
                MoTa = x.MoTa,
                Gia = x.Gia,
                HinhAnh = x.HinhAnh,
                TrangThai = x.TrangThai
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
            .Select(x => new GoiTrangTriDto
            {
                GoiTrangTriID = x.GoiTrangTriID,
                MaGoi = x.MaGoi,
                TenGoi = x.TenGoi,
                PhongCach = x.PhongCach,
                MoTa = x.MoTa,
                Gia = x.Gia,
                HinhAnh = x.HinhAnh,
                TrangThai = x.TrangThai
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
        [FromForm] TaoGoiTrangTriRequest request,
        CancellationToken cancellationToken)
    {
        var maGoi = NormalizeText(request.MaGoi);

        if (await _context.GoiTrangTri.AnyAsync(
                x => x.MaGoi == maGoi,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }

        string? imagePath = null;
        try
        {
            imagePath = request.HinhAnhFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.HinhAnhFile,
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
            TenGoi = NormalizeText(request.TenGoi),
            PhongCach = NormalizeOptional(request.PhongCach),
            MoTa = NormalizeOptional(request.MoTa),
            Gia = request.Gia,
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
        [FromForm] CapNhatGoiTrangTriRequest request,
        CancellationToken cancellationToken)
    {
        var goi = await _context.GoiTrangTri
            .FirstOrDefaultAsync(x => x.GoiTrangTriID == id, cancellationToken);

        if (goi is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy gói trang trí."));
        }

        var maGoi = NormalizeText(request.MaGoi);

        if (await _context.GoiTrangTri.AnyAsync(
                x => x.MaGoi == maGoi && x.GoiTrangTriID != id,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã gói đã tồn tại."));
        }

        if (request.HinhAnhFile is not null && request.XoaHinhAnh)
        {
            return BadRequest(new ApiErrorResponse(
                "Không thể vừa tải ảnh mới vừa gỡ ảnh hiện tại.",
                new Dictionary<string, string[]>
                {
                    ["HinhAnhFile"] = ["Chỉ chọn một trong hai thao tác tải mới hoặc gỡ ảnh."]
                }));
        }

        var oldImagePath = goi.HinhAnh;
        string? newImagePath = null;
        try
        {
            newImagePath = request.HinhAnhFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.HinhAnhFile,
                    ImageDomain.GoiDecor,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        goi.MaGoi = maGoi;
        goi.TenGoi = NormalizeText(request.TenGoi);
        goi.PhongCach = NormalizeOptional(request.PhongCach);
        goi.MoTa = NormalizeOptional(request.MoTa);
        goi.Gia = request.Gia;
        goi.HinhAnh = newImagePath ?? (request.XoaHinhAnh ? null : oldImagePath);

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

    [HttpPatch("{id:int}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThai(
        int id,
        [FromBody] CapNhatTrangThaiGoiTrangTriRequest request,
        CancellationToken cancellationToken)
    {
        var goi = await _context.GoiTrangTri
            .FirstOrDefaultAsync(x => x.GoiTrangTriID == id, cancellationToken);

        if (goi is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy gói trang trí."));
        }

        if (!IsValidStatus(request.TrangThai))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái gói chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        goi.TrangThai = request.TrangThai;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(goi));
    }

    private static GoiTrangTriDto ToDto(GoiTrangTri goi) => new()
    {
        GoiTrangTriID = goi.GoiTrangTriID,
        MaGoi = NormalizeText(goi.MaGoi),
        TenGoi = NormalizeText(goi.TenGoi),
        PhongCach = NormalizeOptional(goi.PhongCach),
        MoTa = NormalizeOptional(goi.MoTa),
        Gia = goi.Gia,
        HinhAnh = NormalizeOptional(goi.HinhAnh),
        TrangThai = NormalizeText(goi.TrangThai)
    };

    private static GoiTrangTriDto NormalizeDto(GoiTrangTriDto goi) => new()
    {
        GoiTrangTriID = goi.GoiTrangTriID,
        MaGoi = NormalizeText(goi.MaGoi),
        TenGoi = NormalizeText(goi.TenGoi),
        PhongCach = NormalizeOptional(goi.PhongCach),
        MoTa = NormalizeOptional(goi.MoTa),
        Gia = goi.Gia,
        HinhAnh = NormalizeOptional(goi.HinhAnh),
        TrangThai = NormalizeText(goi.TrangThai)
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
                ["HinhAnhFile"] = [exception.Message]
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

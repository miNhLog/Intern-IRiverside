using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.DichVu;
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
public sealed class DichVuController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;

    public DichVuController(
        ApplicationDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string? tuKhoa,
        string? loaiDichVu,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        tuKhoa = NormalizeOptional(tuKhoa);
        loaiDichVu = NormalizeOptional(loaiDichVu);
        trangThai = NormalizeOptional(trangThai);

        if (trangThai is not null && !IsValidStatus(trangThai))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái dịch vụ không hợp lệ."));
        }

        var query = _context.DichVu.AsNoTracking();

        if (tuKhoa is not null)
        {
            query = query.Where(x =>
                x.MaDichVu.Contains(tuKhoa) ||
                x.TenDichVu.Contains(tuKhoa) ||
                (x.MoTa != null && x.MoTa.Contains(tuKhoa)));
        }

        if (loaiDichVu is not null)
        {
            query = query.Where(x => x.LoaiDichVu == loaiDichVu);
        }

        if (trangThai is not null)
        {
            query = query.Where(x => x.TrangThai == trangThai);
        }

        var danhSach = await query
            .OrderBy(x => x.DichVuID)
            .Select(x => new DichVuDto
            {
                DichVuID = x.DichVuID,
                MaDichVu = x.MaDichVu,
                TenDichVu = x.TenDichVu,
                LoaiDichVu = x.LoaiDichVu,
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
        var dichVu = await _context.DichVu
            .AsNoTracking()
            .Where(x => x.DichVuID == id)
            .Select(x => new DichVuDto
            {
                DichVuID = x.DichVuID,
                MaDichVu = x.MaDichVu,
                TenDichVu = x.TenDichVu,
                LoaiDichVu = x.LoaiDichVu,
                MoTa = x.MoTa,
                Gia = x.Gia,
                HinhAnh = x.HinhAnh,
                TrangThai = x.TrangThai
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dichVu is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy dịch vụ."));
        }

        return Ok(NormalizeDto(dichVu));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] TaoDichVuRequest request,
        CancellationToken cancellationToken)
    {
        var maDichVu = NormalizeText(request.MaDichVu);

        if (await _context.DichVu.AnyAsync(
                x => x.MaDichVu == maDichVu,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
        }

        string? imagePath = null;
        try
        {
            imagePath = request.HinhAnhFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.HinhAnhFile,
                    ImageDomain.DichVu,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        var dichVu = new DichVu
        {
            MaDichVu = maDichVu,
            TenDichVu = NormalizeText(request.TenDichVu),
            LoaiDichVu = NormalizeOptional(request.LoaiDichVu),
            MoTa = NormalizeOptional(request.MoTa),
            Gia = request.Gia,
            HinhAnh = imagePath,
            TrangThai = "Áp dụng"
        };

        _context.DichVu.Add(dichVu);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await DeleteQuietlyAsync(imagePath, cancellationToken);
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
        }
        catch
        {
            await DeleteQuietlyAsync(imagePath, cancellationToken);
            throw;
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = dichVu.DichVuID },
            ToDto(dichVu));
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] CapNhatDichVuRequest request,
        CancellationToken cancellationToken)
    {
        var dichVu = await _context.DichVu
            .FirstOrDefaultAsync(x => x.DichVuID == id, cancellationToken);

        if (dichVu is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy dịch vụ."));
        }

        var maDichVu = NormalizeText(request.MaDichVu);

        if (await _context.DichVu.AnyAsync(
                x => x.MaDichVu == maDichVu && x.DichVuID != id,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
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

        var oldImagePath = dichVu.HinhAnh;
        string? newImagePath = null;
        try
        {
            newImagePath = request.HinhAnhFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.HinhAnhFile,
                    ImageDomain.DichVu,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        dichVu.MaDichVu = maDichVu;
        dichVu.TenDichVu = NormalizeText(request.TenDichVu);
        dichVu.LoaiDichVu = NormalizeOptional(request.LoaiDichVu);
        dichVu.MoTa = NormalizeOptional(request.MoTa);
        dichVu.Gia = request.Gia;
        dichVu.HinhAnh = newImagePath ?? (request.XoaHinhAnh ? null : oldImagePath);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await DeleteQuietlyAsync(newImagePath, cancellationToken);
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
        }
        catch
        {
            await DeleteQuietlyAsync(newImagePath, cancellationToken);
            throw;
        }

        if (!string.Equals(oldImagePath, dichVu.HinhAnh, StringComparison.Ordinal))
        {
            await DeleteQuietlyAsync(oldImagePath, cancellationToken);
        }

        return Ok(ToDto(dichVu));
    }

    [HttpPatch("{id:int}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThai(
        int id,
        [FromBody] CapNhatTrangThaiDichVuRequest request,
        CancellationToken cancellationToken)
    {
        var dichVu = await _context.DichVu
            .FirstOrDefaultAsync(x => x.DichVuID == id, cancellationToken);

        if (dichVu is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy dịch vụ."));
        }

        if (!IsValidStatus(request.TrangThai))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái dịch vụ chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        dichVu.TrangThai = request.TrangThai;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(dichVu));
    }

    private static DichVuDto ToDto(DichVu dichVu) => new()
    {
        DichVuID = dichVu.DichVuID,
        MaDichVu = NormalizeText(dichVu.MaDichVu),
        TenDichVu = NormalizeText(dichVu.TenDichVu),
        LoaiDichVu = NormalizeOptional(dichVu.LoaiDichVu),
        MoTa = NormalizeOptional(dichVu.MoTa),
        Gia = dichVu.Gia,
        HinhAnh = NormalizeOptional(dichVu.HinhAnh),
        TrangThai = NormalizeText(dichVu.TrangThai)
    };

    private static DichVuDto NormalizeDto(DichVuDto dichVu) => new()
    {
        DichVuID = dichVu.DichVuID,
        MaDichVu = NormalizeText(dichVu.MaDichVu),
        TenDichVu = NormalizeText(dichVu.TenDichVu),
        LoaiDichVu = NormalizeOptional(dichVu.LoaiDichVu),
        MoTa = NormalizeOptional(dichVu.MoTa),
        Gia = dichVu.Gia,
        HinhAnh = NormalizeOptional(dichVu.HinhAnh),
        TrangThai = NormalizeText(dichVu.TrangThai)
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

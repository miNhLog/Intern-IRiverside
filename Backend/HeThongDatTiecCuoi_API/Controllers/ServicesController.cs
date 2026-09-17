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
[Route("api/services")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;

    public ServicesController(
        ApplicationDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string? keyword,
        string? serviceType,
        string? status,
        CancellationToken cancellationToken)
    {
        keyword = NormalizeOptional(keyword);
        serviceType = NormalizeOptional(serviceType);
        status = NormalizeOptional(status);

        if (status is not null && !IsValidStatus(status))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái dịch vụ không hợp lệ."));
        }

        var query = _context.DichVu.AsNoTracking();

        if (keyword is not null)
        {
            query = query.Where(x =>
                x.MaDichVu.Contains(keyword) ||
                x.TenDichVu.Contains(keyword) ||
                (x.MoTa != null && x.MoTa.Contains(keyword)));
        }

        if (serviceType is not null)
        {
            query = query.Where(x => x.LoaiDichVu == serviceType);
        }

        if (status is not null)
        {
            query = query.Where(x => x.TrangThai == status);
        }

        var danhSach = await query
            .OrderBy(x => x.DichVuID)
            .Select(x => new ServiceDto
            {
                ServiceId = x.DichVuID,
                ServiceCode = x.MaDichVu,
                ServiceName = x.TenDichVu,
                ServiceType = x.LoaiDichVu,
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
        var dichVu = await _context.DichVu
            .AsNoTracking()
            .Where(x => x.DichVuID == id)
            .Select(x => new ServiceDto
            {
                ServiceId = x.DichVuID,
                ServiceCode = x.MaDichVu,
                ServiceName = x.TenDichVu,
                ServiceType = x.LoaiDichVu,
                Description = x.MoTa,
                Price = x.Gia,
                ImageUrl = x.HinhAnh,
                Status = x.TrangThai
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
        [FromForm] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var maDichVu = NormalizeText(request.ServiceCode);

        if (await _context.DichVu.AnyAsync(
                x => x.MaDichVu == maDichVu,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
        }

        string? imagePath = null;
        try
        {
            imagePath = request.ImageFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.ImageFile,
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
            TenDichVu = NormalizeText(request.ServiceName),
            LoaiDichVu = NormalizeOptional(request.ServiceType),
            MoTa = NormalizeOptional(request.Description),
            Gia = request.Price,
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
        [FromForm] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var dichVu = await _context.DichVu
            .FirstOrDefaultAsync(x => x.DichVuID == id, cancellationToken);

        if (dichVu is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy dịch vụ."));
        }

        var maDichVu = NormalizeText(request.ServiceCode);

        if (await _context.DichVu.AnyAsync(
                x => x.MaDichVu == maDichVu && x.DichVuID != id,
                cancellationToken))
        {
            return Conflict(new ApiErrorResponse("Mã dịch vụ đã tồn tại."));
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

        var oldImagePath = dichVu.HinhAnh;
        string? newImagePath = null;
        try
        {
            newImagePath = request.ImageFile is null
                ? null
                : await _imageStorage.SaveAsync(
                    request.ImageFile,
                    ImageDomain.DichVu,
                    cancellationToken);
        }
        catch (ImageStorageValidationException exception)
        {
            return BadRequest(ToImageError(exception));
        }

        dichVu.MaDichVu = maDichVu;
        dichVu.TenDichVu = NormalizeText(request.ServiceName);
        dichVu.LoaiDichVu = NormalizeOptional(request.ServiceType);
        dichVu.MoTa = NormalizeOptional(request.Description);
        dichVu.Gia = request.Price;
        dichVu.HinhAnh = newImagePath ?? (request.RemoveImage ? null : oldImagePath);

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

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateServiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var dichVu = await _context.DichVu
            .FirstOrDefaultAsync(x => x.DichVuID == id, cancellationToken);

        if (dichVu is null)
        {
            return NotFound(new ApiErrorResponse("Không tìm thấy dịch vụ."));
        }

        if (!IsValidStatus(request.Status))
        {
            return BadRequest(new ApiErrorResponse(
                "Trạng thái dịch vụ chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        dichVu.TrangThai = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(dichVu));
    }

    private static ServiceDto ToDto(DichVu dichVu) => new()
    {
        ServiceId = dichVu.DichVuID,
        ServiceCode = NormalizeText(dichVu.MaDichVu),
        ServiceName = NormalizeText(dichVu.TenDichVu),
        ServiceType = NormalizeOptional(dichVu.LoaiDichVu),
        Description = NormalizeOptional(dichVu.MoTa),
        Price = dichVu.Gia,
        ImageUrl = NormalizeOptional(dichVu.HinhAnh),
        Status = NormalizeText(dichVu.TrangThai)
    };

    private static ServiceDto NormalizeDto(ServiceDto dichVu) => new()
    {
        ServiceId = dichVu.ServiceId,
        ServiceCode = NormalizeText(dichVu.ServiceCode),
        ServiceName = NormalizeText(dichVu.ServiceName),
        ServiceType = NormalizeOptional(dichVu.ServiceType),
        Description = NormalizeOptional(dichVu.Description),
        Price = dichVu.Price,
        ImageUrl = NormalizeOptional(dichVu.ImageUrl),
        Status = NormalizeText(dichVu.Status)
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

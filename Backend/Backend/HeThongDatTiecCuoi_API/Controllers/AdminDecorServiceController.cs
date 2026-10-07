using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.DecorService;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/decor-service")]
[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class AdminDecorServiceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public AdminDecorServiceController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet("decor")]
    public async Task<IActionResult> GetDecorList()
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var list = await _context.DecorPackages
            .AsNoTracking()
            .Where(item => item.DataStatusId == existingId)
            .OrderBy(item => item.MaGoi)
            .Select(item => new DecorPackageDto
            {
                DecorationPackageId = item.GoiTrangTriID,
                PackageCode = item.MaGoi,
                PackageName = item.TenGoi,
                Style = item.PhongCach,
                Description = item.MoTa,
                Price = item.Gia,
                StatusCode = item.Status.StatusCode,
                StatusName = item.Status.StatusName,
                ImageUrl = item.Images
                    .Where(image => image.IsPrimary && image.DataStatusId == existingId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new { success = true, data = list });
    }

    [HttpPost("decor")]
    public async Task<IActionResult> CreateDecor([FromBody] DecorPackageRequest request)
    {
        var code = request.PackageCode.Trim();
        if (await _context.DecorPackages.AnyAsync(item => item.MaGoi == code))
            return Conflict(new { success = false, message = "Mã gói trang trí đã tồn tại." });

        var statusId = await ResolveEditableStatusIdAsync(StatusGroups.DecorationPackage, request.StatusCode);
        if (statusId == null)
            return BadRequest(new { success = false, message = "Trạng thái chỉ được phép là ACTIVE hoặc INACTIVE." });

        var decor = new DecorPackage
        {
            MaGoi = code,
            TenGoi = request.PackageName.Trim(),
            PhongCach = request.Style.Trim(),
            MoTa = NormalizeOptional(request.Description),
            Gia = request.Price,
            StatusId = statusId.Value,
            DataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing)
        };

        _context.DecorPackages.Add(decor);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Thêm gói decor thành công!" });
    }

    [HttpPut("decor/{id:int}")]
    public async Task<IActionResult> UpdateDecor(int id, [FromBody] DecorPackageRequest request)
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var decor = await _context.DecorPackages
            .SingleOrDefaultAsync(item => item.GoiTrangTriID == id && item.DataStatusId == existingId);
        if (decor == null)
            return NotFound(new { success = false, message = "Không tìm thấy gói decor." });

        var code = request.PackageCode.Trim();
        if (await _context.DecorPackages.AnyAsync(item => item.GoiTrangTriID != id && item.MaGoi == code))
            return Conflict(new { success = false, message = "Mã gói trang trí đã tồn tại." });

        var statusId = await ResolveEditableStatusIdAsync(StatusGroups.DecorationPackage, request.StatusCode);
        if (statusId == null)
            return BadRequest(new { success = false, message = "Trạng thái chỉ được phép là ACTIVE hoặc INACTIVE." });

        decor.MaGoi = code;
        decor.TenGoi = request.PackageName.Trim();
        decor.PhongCach = request.Style.Trim();
        decor.MoTa = NormalizeOptional(request.Description);
        decor.Gia = request.Price;
        decor.StatusId = statusId.Value;
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Cập nhật gói decor thành công!" });
    }

    [HttpGet("service")]
    public async Task<IActionResult> GetServiceList()
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var list = await _context.ServiceItems
            .AsNoTracking()
            .Where(item => item.DataStatusId == existingId)
            .OrderBy(item => item.MaDichVu)
            .Select(item => new ServiceItemDto
            {
                ServiceId = item.DichVuID,
                ServiceCode = item.MaDichVu,
                ServiceName = item.TenDichVu,
                ServiceType = item.LoaiDichVu,
                Description = item.MoTa,
                Price = item.Gia,
                StatusCode = item.Status.StatusCode,
                StatusName = item.Status.StatusName
            })
            .ToListAsync();

        return Ok(new { success = true, data = list });
    }

    [HttpPost("service")]
    public async Task<IActionResult> CreateService([FromBody] ServiceItemRequest request)
    {
        var code = request.ServiceCode.Trim();
        if (await _context.ServiceItems.AnyAsync(item => item.MaDichVu == code))
            return Conflict(new { success = false, message = "Mã dịch vụ đã tồn tại." });

        var statusId = await ResolveEditableStatusIdAsync(StatusGroups.Service, request.StatusCode);
        if (statusId == null)
            return BadRequest(new { success = false, message = "Trạng thái chỉ được phép là ACTIVE hoặc INACTIVE." });

        var service = new ServiceItem
        {
            MaDichVu = code,
            TenDichVu = request.ServiceName.Trim(),
            LoaiDichVu = NormalizeOptional(request.ServiceType),
            MoTa = NormalizeOptional(request.Description),
            Gia = request.Price,
            StatusId = statusId.Value,
            DataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing)
        };

        _context.ServiceItems.Add(service);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Thêm dịch vụ thành công!" });
    }

    [HttpPut("service/{id:int}")]
    public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceItemRequest request)
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var service = await _context.ServiceItems
            .SingleOrDefaultAsync(item => item.DichVuID == id && item.DataStatusId == existingId);
        if (service == null)
            return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        var code = request.ServiceCode.Trim();
        if (await _context.ServiceItems.AnyAsync(item => item.DichVuID != id && item.MaDichVu == code))
            return Conflict(new { success = false, message = "Mã dịch vụ đã tồn tại." });

        var statusId = await ResolveEditableStatusIdAsync(StatusGroups.Service, request.StatusCode);
        if (statusId == null)
            return BadRequest(new { success = false, message = "Trạng thái chỉ được phép là ACTIVE hoặc INACTIVE." });

        service.MaDichVu = code;
        service.TenDichVu = request.ServiceName.Trim();
        service.LoaiDichVu = NormalizeOptional(request.ServiceType);
        service.MoTa = NormalizeOptional(request.Description);
        service.Gia = request.Price;
        service.StatusId = statusId.Value;
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Cập nhật dịch vụ thành công!" });
    }

    private async Task<int?> ResolveEditableStatusIdAsync(string group, string? requestedCode)
    {
        var code = string.IsNullOrWhiteSpace(requestedCode)
            ? BusinessStatusCodes.Active
            : requestedCode.Trim().ToUpperInvariant();
        if (code is not (BusinessStatusCodes.Active or BusinessStatusCodes.Inactive))
            return null;
        return await _statusService.GetStatusIdAsync(group, code);
    }

    private async Task<byte> GetDataStatusIdAsync(string code) =>
        await _context.DataStatuses
            .Where(item => item.DataStatusCode == code)
            .Select(item => item.DataStatusId)
            .SingleAsync();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

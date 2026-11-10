using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.DecorService;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using HeThongDatTiecCuoi_API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/decor-service")]
[ApiController]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class AdminDecorServiceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAiCatalogSyncService? _catalogSyncService;

    public AdminDecorServiceController(
        ApplicationDbContext context,
        IStatusService statusService,
        IWebHostEnvironment environment,
        IAiCatalogSyncService? catalogSyncService = null)
    {
        _context = context;
        _statusService = statusService;
        _environment = environment;
        _catalogSyncService = catalogSyncService;
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

        foreach (var item in list)
            item.ImageUrl = ToAbsoluteImageUrl(item.ImageUrl);

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

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.DecorPackages.Add(decor);
        await _context.SaveChangesAsync();
        AddCatalogAudit(AuditActions.DecorCreated, AuditEntityNames.DecorPackage, decor.GoiTrangTriID, null,
            new { decor.MaGoi, decor.TenGoi, decor.PhongCach, decor.Gia, request.StatusCode }, $"Tạo gói trang trí {decor.MaGoi}.");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        _catalogSyncService?.SyncCatalogInBackground();
        return Ok(new { success = true, id = decor.GoiTrangTriID, message = "Thêm gói decor thành công!" });
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

        var oldData = new { decor.MaGoi, decor.TenGoi, decor.PhongCach, decor.Gia, decor.StatusId };
        decor.MaGoi = code;
        decor.TenGoi = request.PackageName.Trim();
        decor.PhongCach = request.Style.Trim();
        decor.MoTa = NormalizeOptional(request.Description);
        decor.Gia = request.Price;
        decor.StatusId = statusId.Value;
        AddCatalogAudit(AuditActions.DecorUpdated, AuditEntityNames.DecorPackage, decor.GoiTrangTriID, oldData,
            new { decor.MaGoi, decor.TenGoi, decor.PhongCach, decor.Gia, decor.StatusId }, $"Cập nhật gói trang trí {decor.MaGoi}.");
        await _context.SaveChangesAsync();
        _catalogSyncService?.SyncCatalogInBackground();
        return Ok(new { success = true, id = decor.GoiTrangTriID, message = "Cập nhật gói decor thành công!" });
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

        foreach (var item in list)
            item.ImageUrl = ToAbsoluteImageUrl(ImageUploadHelper.FindManagedUrl(
                _environment.WebRootPath, "dich-vu", item.ServiceId.ToString()));

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

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.ServiceItems.Add(service);
        await _context.SaveChangesAsync();
        AddCatalogAudit(AuditActions.ServiceCreated, AuditEntityNames.Service, service.DichVuID, null,
            new { service.MaDichVu, service.TenDichVu, service.LoaiDichVu, service.Gia, request.StatusCode }, $"Tạo dịch vụ {service.MaDichVu}.");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        _catalogSyncService?.SyncCatalogInBackground();
        return Ok(new { success = true, id = service.DichVuID, message = "Thêm dịch vụ thành công!" });
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

        var oldData = new { service.MaDichVu, service.TenDichVu, service.LoaiDichVu, service.Gia, service.StatusId };
        service.MaDichVu = code;
        service.TenDichVu = request.ServiceName.Trim();
        service.LoaiDichVu = NormalizeOptional(request.ServiceType);
        service.MoTa = NormalizeOptional(request.Description);
        service.Gia = request.Price;
        service.StatusId = statusId.Value;
        AddCatalogAudit(AuditActions.ServiceUpdated, AuditEntityNames.Service, service.DichVuID, oldData,
            new { service.MaDichVu, service.TenDichVu, service.LoaiDichVu, service.Gia, service.StatusId }, $"Cập nhật dịch vụ {service.MaDichVu}.");
        await _context.SaveChangesAsync();
        _catalogSyncService?.SyncCatalogInBackground();
        return Ok(new { success = true, id = service.DichVuID, message = "Cập nhật dịch vụ thành công!" });
    }

    [HttpPost("decor/{id:int}/image")]
    [RequestSizeLimit(ImageUploadHelper.MaxRequestSize)]
    public async Task<IActionResult> UploadDecorImage(int id, [FromForm] IFormFile file)
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var decor = await _context.DecorPackages
            .Include(item => item.Images.Where(image => image.DataStatusId == existingId))
            .SingleOrDefaultAsync(item => item.GoiTrangTriID == id && item.DataStatusId == existingId);
        if (decor is null) return NotFound(new { success = false, message = "Không tìm thấy gói decor." });

        var validationError = ImageUploadHelper.Validate(file);
        if (validationError is not null)
            return BadRequest(new { success = false, message = validationError });

        var oldUrl = decor.Images.FirstOrDefault(image => image.IsPrimary)?.ImagePath;
        var imageUrl = await ImageUploadHelper.SaveAsync(
            file, _environment.WebRootPath, "goi-decor", id.ToString(), HttpContext.RequestAborted);
        var primary = decor.Images.FirstOrDefault(image => image.IsPrimary);
        if (primary is null)
        {
            primary = new ImageAsset
            {
                DecorationPackageId = id,
                IsPrimary = true,
                SortOrder = 1,
                DataStatusId = existingId
            };
            _context.ImageAssets.Add(primary);
        }
        primary.ImagePath = imageUrl;
        await _context.SaveChangesAsync();
        ImageUploadHelper.DeleteManagedUrl(oldUrl, _environment.WebRootPath);
        return Ok(new { success = true, imageUrl = ToAbsoluteImageUrl(imageUrl), message = "Tải ảnh gói decor thành công." });
    }

    [HttpPost("service/{id:int}/image")]
    [RequestSizeLimit(ImageUploadHelper.MaxRequestSize)]
    public async Task<IActionResult> UploadServiceImage(int id, [FromForm] IFormFile file)
    {
        var existingId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        if (!await _context.ServiceItems.AnyAsync(item => item.DichVuID == id && item.DataStatusId == existingId))
            return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        var validationError = ImageUploadHelper.Validate(file);
        if (validationError is not null)
            return BadRequest(new { success = false, message = validationError });

        var imageUrl = await ImageUploadHelper.SaveAsync(
            file, _environment.WebRootPath, "dich-vu", id.ToString(), HttpContext.RequestAborted);
        return Ok(new { success = true, imageUrl = ToAbsoluteImageUrl(imageUrl), message = "Tải ảnh dịch vụ thành công." });
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

    private string? ToAbsoluteImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute)) return absolute.ToString();
        return $"{Request.Scheme}://{Request.Host}/{imageUrl.TrimStart('/')}";
    }

    private void AddCatalogAudit(string action, string entityName, long entityId, object? oldData, object? newData, string notes)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (int?)null;
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId, Action = action, EntityName = entityName, EntityId = entityId,
            OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData is null ? null : JsonSerializer.Serialize(newData),
            Timestamp = DateTime.Now, Outcome = AuditOutcomes.Success, Notes = notes
        });
    }
}

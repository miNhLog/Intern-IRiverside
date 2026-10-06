using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.DTOs.Hall;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/halls")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class HallController : ControllerBase
{
    private static readonly string[] ValidStatuses =
    [
        HallStatusCodes.Active,
        HallStatusCodes.Maintenance,
        HallStatusCodes.Inactive
    ];

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public HallController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicHalls(
        [FromQuery] string? keyword,
        [FromQuery] string? capacity,
        [FromQuery] string? priceRange,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 20);

        if (!TryGetCapacityRange(capacity, out var minimumCapacity, out var maximumCapacity))
            return BadRequest(new { message = "Khoảng sức chứa không hợp lệ." });

        if (!TryGetPriceRange(priceRange, out var minimumPrice, out var maximumPrice))
            return BadRequest(new { message = "Khoảng giá thuê không hợp lệ." });

        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "name-asc" : sort.Trim().ToLowerInvariant();
        if (normalizedSort is not ("name-asc" or "price-asc" or "price-desc" or "capacity-desc"))
            return BadRequest(new { message = "Tùy chọn sắp xếp không hợp lệ." });

        var query = _context.Halls.AsNoTracking()
            .Where(hall =>
                hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                hall.Status.StatusGroup == StatusGroups.Hall &&
                hall.Status.StatusCode == HallStatusCodes.Active);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(hall => hall.HallName.Contains(term) || hall.HallCode.Contains(term));
        }

        if (minimumCapacity.HasValue)
        {
            var lower = minimumCapacity.Value;
            query = maximumCapacity.HasValue
                ? query.Where(hall => hall.MaximumCapacity >= lower
                    && (hall.MinimumCapacity ?? 0) <= maximumCapacity.Value)
                : query.Where(hall => hall.MaximumCapacity >= lower);
        }

        if (minimumPrice.HasValue)
        {
            var lower = minimumPrice.Value;
            query = maximumPrice.HasValue
                ? query.Where(hall => hall.RentalPrice >= lower && hall.RentalPrice <= maximumPrice.Value)
                : query.Where(hall => hall.RentalPrice >= lower);
        }

        query = normalizedSort switch
        {
            "price-asc" => query.OrderBy(hall => hall.RentalPrice).ThenBy(hall => hall.HallName),
            "price-desc" => query.OrderByDescending(hall => hall.RentalPrice).ThenBy(hall => hall.HallName),
            "capacity-desc" => query.OrderByDescending(hall => hall.MaximumCapacity).ThenBy(hall => hall.HallName),
            _ => query.OrderBy(hall => hall.HallName)
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(hall => new PublicHallDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                MinimumCapacity = hall.MinimumCapacity,
                MaximumCapacity = hall.MaximumCapacity,
                RentalPrice = hall.RentalPrice,
                Description = hall.Description,
                ImageUrl = hall.Images
                    .Where(image => image.IsPrimary &&
                        image.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(new PublicHallListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        });
    }

    [HttpGet("public/{hallId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicHall(
        int hallId,
        CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.AsNoTracking()
            .Where(item => item.HallId == hallId
                && item.DataStatus.DataStatusCode == DataStatusCodes.Existing
                && item.Status.StatusGroup == StatusGroups.Hall
                && item.Status.StatusCode == HallStatusCodes.Active)
            .Select(item => new PublicHallDetailDto
            {
                HallId = item.HallId,
                HallCode = item.HallCode,
                HallName = item.HallName,
                MinimumCapacity = item.MinimumCapacity,
                MaximumCapacity = item.MaximumCapacity,
                RentalPrice = item.RentalPrice,
                Description = item.Description,
                ImageUrl = item.Images
                    .Where(image => image.IsPrimary &&
                        image.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault(),
                Status = item.Status.StatusCode,
                StatusName = item.Status.StatusName
            })
            .SingleOrDefaultAsync(cancellationToken);

        return hall is null
            ? NotFound(new { message = "Không tìm thấy sảnh." })
            : Ok(hall);
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeaturedHalls(CancellationToken cancellationToken)
    {
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall =>
                hall.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                hall.Status.StatusGroup == StatusGroups.Hall &&
                hall.Status.StatusCode == HallStatusCodes.Active)
            .OrderBy(hall => hall.HallId)
            .Take(3)
            .Select(hall => new HallDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                MinimumCapacity = hall.MinimumCapacity,
                MaximumCapacity = hall.MaximumCapacity,
                RentalPrice = hall.RentalPrice,
                Description = hall.Description,
                ImageUrl = hall.Images
                    .Where(image => image.IsPrimary &&
                        image.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault(),
                Status = hall.Status.StatusCode,
                StatusName = hall.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(halls);
    }

    [HttpGet]
    public async Task<IActionResult> GetHalls(CancellationToken cancellationToken)
    {
        var halls = await _context.Halls.AsNoTracking()
            .Include(hall => hall.Status)
            .Include(hall => hall.Images.Where(image =>
                image.IsPrimary &&
                image.DataStatus.DataStatusCode == DataStatusCodes.Existing))
            .Where(hall => hall.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(hall => hall.HallId)
            .ToListAsync(cancellationToken);
        return Ok(halls.Select(hall => ToDto(hall)));
    }

    [HttpGet("{hallId:int}")]
    public async Task<IActionResult> GetHall(int hallId, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.AsNoTracking()
            .Include(item => item.Status)
            .Include(item => item.Images.Where(image =>
                image.IsPrimary &&
                image.DataStatus.DataStatusCode == DataStatusCodes.Existing))
            .Where(item => item.HallId == hallId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .SingleOrDefaultAsync(cancellationToken);
        return hall is null
            ? NotFound(new { message = "Không tìm thấy sảnh tiệc." })
            : Ok(ToDto(hall));
    }

    [HttpPost]
    public async Task<IActionResult> CreateHall(
        [FromBody] CreateHallRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.HallCode, request.HallName, request.MinimumCapacity,
            request.MaximumCapacity, request.RentalPrice);
        if (error is not null) return BadRequest(new { message = error });

        if (await _context.Halls.AnyAsync(
                hall => hall.HallCode == request.HallCode.Trim(), cancellationToken))
            return Conflict(new { message = "Mã sảnh đã tồn tại." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });
        var existingDataStatus = await GetDataStatusAsync(DataStatusCodes.Existing, cancellationToken);

        var hall = new Hall
        {
            HallCode = request.HallCode.Trim(),
            HallName = request.HallName.Trim(),
            MinimumCapacity = request.MinimumCapacity,
            MaximumCapacity = request.MaximumCapacity,
            RentalPrice = request.RentalPrice,
            Description = request.Description?.Trim(),
            StatusId = status.StatusId,
            DataStatusId = existingDataStatus.DataStatusId
        };
        _context.Halls.Add(hall);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetHall), new { hallId = hall.HallId }, ToDto(hall, status));
    }

    [HttpPut("{hallId:int}")]
    public async Task<IActionResult> UpdateHall(
        int hallId,
        [FromBody] UpdateHallRequest request,
        CancellationToken cancellationToken)
    {
        var hall = await _context.Halls
            .Include(item => item.Status)
            .Include(item => item.Images.Where(image =>
                image.IsPrimary &&
                image.DataStatus.DataStatusCode == DataStatusCodes.Existing))
            .SingleOrDefaultAsync(item =>
                item.HallId == hallId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });

        var error = Validate(request.HallCode, request.HallName, request.MinimumCapacity,
            request.MaximumCapacity, request.RentalPrice);
        if (error is not null) return BadRequest(new { message = error });

        if (await _context.Halls.AnyAsync(item =>
                item.HallCode == request.HallCode.Trim() && item.HallId != hallId,
                cancellationToken))
            return Conflict(new { message = "Mã sảnh đã tồn tại." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });

        hall.HallCode = request.HallCode.Trim();
        hall.HallName = request.HallName.Trim();
        hall.MinimumCapacity = request.MinimumCapacity;
        hall.MaximumCapacity = request.MaximumCapacity;
        hall.RentalPrice = request.RentalPrice;
        hall.Description = request.Description?.Trim();
        hall.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(hall, status));
    }

    [HttpPatch("{hallId:int}/status")]
    public async Task<IActionResult> UpdateHallStatus(
        int hallId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var hall = await _context.Halls
            .Include(item => item.Images.Where(image =>
                image.IsPrimary &&
                image.DataStatus.DataStatusCode == DataStatusCodes.Existing))
            .SingleOrDefaultAsync(
                item => item.HallId == hallId &&
                    item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });

        hall.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Cập nhật trạng thái thành công.", hall = ToDto(hall, status) });
    }

    [HttpDelete("{hallId:int}")]
    public async Task<IActionResult> DeleteHall(int hallId, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.Include(item => item.Status)
            .SingleOrDefaultAsync(item =>
                item.HallId == hallId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });
        if (hall.Status.StatusCode == HallStatusCodes.Active)
            return Conflict(new { message = "Không thể xóa sảnh đang hoạt động. Hãy chuyển sảnh sang trạng thái Ngừng hoạt động trước." });

        var deletedDataStatus = await GetDataStatusAsync(DataStatusCodes.Deleted, cancellationToken);
        hall.DataStatusId = deletedDataStatus.DataStatusId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Xóa sảnh thành công." });
    }

    private async Task<Status?> ResolveStatusAsync(string? code, CancellationToken cancellationToken)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !ValidStatuses.Contains(normalized)) return null;
        return await _statusService.GetStatusAsync(StatusGroups.Hall, normalized, cancellationToken);
    }

    private async Task<DataStatus> GetDataStatusAsync(
        string code,
        CancellationToken cancellationToken) =>
        await _context.DataStatuses.SingleOrDefaultAsync(
            status => status.DataStatusCode == code,
            cancellationToken) ?? throw new InvalidOperationException(
            $"Thiếu trạng thái dữ liệu {code}.");

    private static bool TryGetCapacityRange(string? value, out int? minimum, out int? maximum)
    {
        (int? Minimum, int? Maximum) range = value?.Trim().ToLowerInvariant() switch
        {
            null or "" => (null, null),
            "under-200" => (0, 199),
            "200-300" => (200, 300),
            "300-500" => (300, 500),
            "500-700" => (500, 700),
            "over-700" => (701, null),
            _ => (-1, -1)
        };
        (minimum, maximum) = range;
        return minimum != -1;
    }

    private static bool TryGetPriceRange(string? value, out decimal? minimum, out decimal? maximum)
    {
        (decimal? Minimum, decimal? Maximum) range = value?.Trim().ToLowerInvariant() switch
        {
            null or "" => (null, null),
            "under-20000000" => (0m, 19999999.99m),
            "20000000-30000000" => (20000000m, 30000000m),
            "30000000-40000000" => (30000000m, 40000000m),
            "40000000-50000000" => (40000000m, 50000000m),
            "over-50000000" => (50000000.01m, null),
            _ => (-1m, -1m)
        };
        (minimum, maximum) = range;
        return minimum != -1m;
    }

    private static string? Validate(string hallCode, string hallName, int? minimumCapacity,
        int maximumCapacity, decimal rentalPrice)
    {
        if (string.IsNullOrWhiteSpace(hallCode)) return "Mã sảnh không được để trống.";
        if (string.IsNullOrWhiteSpace(hallName)) return "Tên sảnh không được để trống.";
        if (maximumCapacity <= 0) return "Sức chứa tối đa phải lớn hơn 0.";
        if (minimumCapacity.HasValue && minimumCapacity.Value > maximumCapacity)
            return "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa.";
        return rentalPrice < 0 ? "Giá thuê không hợp lệ." : null;
    }

    private static HallDto ToDto(Hall hall, Status? status = null) => new()
    {
        HallId = hall.HallId,
        HallCode = hall.HallCode,
        HallName = hall.HallName,
        MinimumCapacity = hall.MinimumCapacity,
        MaximumCapacity = hall.MaximumCapacity,
        RentalPrice = hall.RentalPrice,
        Description = hall.Description,
        ImageUrl = hall.Images
            .Where(image => image.IsPrimary)
            .OrderBy(image => image.SortOrder)
            .Select(image => image.ImagePath)
            .FirstOrDefault(),
        Status = (status ?? hall.Status).StatusCode,
        StatusName = (status ?? hall.Status).StatusName
    };
}

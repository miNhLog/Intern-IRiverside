using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.DTOs.Dish;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/dishes")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class DishController : ControllerBase
{
    private static readonly string[] ValidCategories =
    [
        "Món khai vị",
        "Món súp",
        "Món chính",
        "Món tráng miệng"
    ];

    private static readonly string[] ValidStatuses =
    [
        DishStatusCodes.Active,
        DishStatusCodes.Inactive
    ];

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IStatusService _statusService;

    public DishController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IStatusService statusService)
    {
        _context = context;
        _environment = environment;
        _statusService = statusService;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicDishes(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var query = _context.Dishes.AsNoTracking()
            .Where(dish =>
                dish.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                dish.Status.StatusGroup == StatusGroups.Dish &&
                dish.Status.StatusCode == DishStatusCodes.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(dish =>
                dish.DishCode.Contains(normalizedKeyword) ||
                dish.DishName.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(dish => dish.Category == normalizedCategory);
        }

        var dishes = await query
            .OrderBy(dish => dish.Category)
            .ThenBy(dish => dish.DishCode)
            .Select(dish => new DishDto
            {
                DishId = dish.DishId,
                DishCode = dish.DishCode,
                DishName = dish.DishName,
                Category = dish.Category,
                Price = dish.Price,
                ImageUrl = dish.ImageUrl,
                Status = dish.Status.StatusCode,
                StatusName = dish.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(dishes);
    }

    [HttpGet]
    public async Task<IActionResult> GetDishes(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _context.Dishes.AsNoTracking()
            .Where(dish =>
                dish.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                dish.Status.StatusGroup == StatusGroups.Dish)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(dish =>
                dish.DishCode.Contains(normalizedKeyword) ||
                dish.DishName.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(dish => dish.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(dish => dish.Status.StatusCode == normalizedStatus);
        }

        var dishes = await query
            .OrderBy(dish => dish.DishCode)
            .Select(dish => new DishDto
            {
                DishId = dish.DishId,
                DishCode = dish.DishCode,
                DishName = dish.DishName,
                Category = dish.Category,
                Price = dish.Price,
                ImageUrl = dish.ImageUrl,
                Status = dish.Status.StatusCode,
                StatusName = dish.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(dishes);
    }

    [HttpGet("management")]
    public async Task<IActionResult> GetManagementDishes(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedCategory = category?.Trim();
        var normalizedStatus = status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedCategory) && !ValidCategories.Contains(normalizedCategory))
            return BadRequest(new { message = "Nhóm món không hợp lệ." });
        if (!string.IsNullOrWhiteSpace(normalizedStatus) && !ValidStatuses.Contains(normalizedStatus))
            return BadRequest(new { message = "Trạng thái món ăn không hợp lệ." });

        var allDishes = _context.Dishes.AsNoTracking().Where(dish =>
            dish.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
            dish.Status.StatusGroup == StatusGroups.Dish);
        var statistics = await allDishes.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Active = group.Count(dish => dish.Status.StatusCode == DishStatusCodes.Active),
            Inactive = group.Count(dish => dish.Status.StatusCode == DishStatusCodes.Inactive)
        }).SingleOrDefaultAsync(cancellationToken);

        var filtered = allDishes;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            filtered = filtered.Where(dish => dish.DishCode.Contains(term) || dish.DishName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(normalizedCategory)) filtered = filtered.Where(dish => dish.Category == normalizedCategory);
        if (!string.IsNullOrWhiteSpace(normalizedStatus)) filtered = filtered.Where(dish => dish.Status.StatusCode == normalizedStatus);

        var totalItems = await filtered.CountAsync(cancellationToken);
        var items = await filtered.OrderBy(dish => dish.DishCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(dish => new ManagerDishListItemDto
            {
                DishId = dish.DishId, DishCode = dish.DishCode, DishName = dish.DishName,
                Category = dish.Category, Price = dish.Price, ImageUrl = dish.ImageUrl,
                Status = dish.Status.StatusCode, StatusName = dish.Status.StatusName,
                MenuUsageCount = dish.MenuDishes.Count(link =>
                    link.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                    link.Menu.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            }).ToListAsync(cancellationToken);

        return Ok(new ManagerDishListResponseDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            TotalDishes = statistics?.Total ?? 0, ActiveDishes = statistics?.Active ?? 0,
            InactiveDishes = statistics?.Inactive ?? 0, CategoryCount = ValidCategories.Length
        });
    }

    [HttpGet("{dishId:int}/management-detail")]
    public async Task<IActionResult> GetManagementDetail(int dishId, CancellationToken cancellationToken)
    {
        var dish = await _context.Dishes.AsNoTracking()
            .Where(item => item.DishId == dishId && item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Select(item => new ManagerDishDetailDto
            {
                DishId = item.DishId, DishCode = item.DishCode, DishName = item.DishName,
                Category = item.Category, Price = item.Price, ImageUrl = item.ImageUrl,
                Status = item.Status.StatusCode, StatusName = item.Status.StatusName
            }).SingleOrDefaultAsync(cancellationToken);
        if (dish is null) return NotFound(new { message = "Không tìm thấy món ăn." });

        dish.Menus = await _context.MenuDishes.AsNoTracking()
            .Where(link => link.DishId == dishId &&
                link.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
                link.Menu.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(link => link.Menu.MenuCode)
            .Select(link => new DishMenuUsageDto
            {
                MenuId = link.MenuId, MenuCode = link.Menu.MenuCode, MenuName = link.Menu.MenuName,
                MenuType = link.Menu.MenuType, Status = link.Menu.Status.StatusCode
            }).ToListAsync(cancellationToken);
        dish.UsageCount = dish.Menus.Count;
        return Ok(dish);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDish(
        [FromBody] CreateDishRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DishName))
        {
            return BadRequest(new { message = "Tên món không được để trống." });
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !ValidCategories.Contains(request.Category.Trim()))
        {
            return BadRequest(new { message = "Nhóm món không hợp lệ." });
        }

        if (request.DishName.Trim().Length > 200 || request.Category.Trim().Length > 100)
        {
            return BadRequest(new { message = "Tên hoặc nhóm món vượt quá độ dài cho phép." });
        }

        if (request.Price < 0)
        {
            return BadRequest(new { message = "Giá món không hợp lệ." });
        }

        var dishCodes = await _context.Dishes
            .AsNoTracking()
            .Select(dish => dish.DishCode)
            .ToListAsync(cancellationToken);

        var highestNumber = dishCodes
            .Where(code =>
                code.StartsWith("MA") &&
                int.TryParse(code[2..], out _))
            .Select(code => int.Parse(code[2..]))
            .DefaultIfEmpty(0)
            .Max();

        var activeStatus = await _statusService.GetStatusAsync(
            StatusGroups.Dish,
            DishStatusCodes.Active,
            cancellationToken) ?? throw new InvalidOperationException("Thiếu trạng thái món ăn ACTIVE.");
        var existingDataStatus = await GetDataStatusAsync(
            DataStatusCodes.Existing,
            cancellationToken);

        var dish = new Dish
        {
            DishCode = $"MA{highestNumber + 1:D3}",
            DishName = request.DishName.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            StatusId = activeStatus.StatusId,
            DataStatusId = existingDataStatus.DataStatusId
        };

        _context.Dishes.Add(dish);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/dishes/{dish.DishId}", ToDto(dish, activeStatus));
    }

    [HttpPut("{dishId:int}")]
    public async Task<IActionResult> UpdateDish(
        int dishId,
        [FromBody] UpdateDishRequest request,
        CancellationToken cancellationToken)
    {
        var dish = await _context.Dishes
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item =>
                item.DishId == dishId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        if (string.IsNullOrWhiteSpace(request.DishName))
        {
            return BadRequest(new { message = "Tên món không được để trống." });
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !ValidCategories.Contains(request.Category.Trim()))
        {
            return BadRequest(new { message = "Nhóm món không hợp lệ." });
        }

        if (request.DishName.Trim().Length > 200 || request.Category.Trim().Length > 100)
        {
            return BadRequest(new { message = "Tên hoặc nhóm món vượt quá độ dài cho phép." });
        }

        if (request.Price < 0)
        {
            return BadRequest(new { message = "Giá món không hợp lệ." });
        }

        dish.DishName = request.DishName.Trim();
        dish.Category = request.Category.Trim();
        dish.Price = request.Price;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(dish));
    }

    [HttpPatch("{dishId:int}/status")]
    public async Task<IActionResult> UpdateDishStatus(
        int dishId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var dish = await _context.Dishes
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item =>
                item.DishId == dishId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        var statusCode = request.Status?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(statusCode) || !ValidStatuses.Contains(statusCode))
        {
            return BadRequest(new { message = "Trạng thái món ăn không hợp lệ." });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.Dish,
            statusCode,
            cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái món ăn chưa được cấu hình." });
        }

        dish.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(dish, status));
    }

    [HttpPost("{dishId:int}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        int dishId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var dish = await _context.Dishes
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item =>
                item.DishId == dishId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ảnh." });
        }

        const long maximumFileSize = 5 * 1024 * 1024;
        if (file.Length > maximumFileSize)
        {
            return BadRequest(new { message = "Ảnh không được vượt quá 5 MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP." });
        }

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (string.IsNullOrWhiteSpace(file.ContentType) || !allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(new { message = "Định dạng nội dung ảnh không hợp lệ." });
        }

        var header = new byte[12];
        await using (var input = file.OpenReadStream())
        {
            var bytesRead = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (!HasValidImageSignature(extension, header, bytesRead))
            {
                return BadRequest(new { message = "Nội dung tệp không khớp với định dạng ảnh." });
            }
        }

        var imageDirectory = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "mon-an");
        Directory.CreateDirectory(imageDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(imageDirectory, fileName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
            var oldImageUrl = dish.ImageUrl;
            dish.ImageUrl = $"/uploads/mon-an/{fileName}";
            await _context.SaveChangesAsync(cancellationToken);
            DeleteExistingImage(oldImageUrl, imageDirectory);
        }
        catch
        {
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            throw;
        }
        return Ok(ToDto(dish));
    }

    [HttpDelete("{dishId:int}/image")]
    public async Task<IActionResult> DeleteImage(
        int dishId,
        CancellationToken cancellationToken)
    {
        var dish = await _context.Dishes
            .FirstOrDefaultAsync(item =>
                item.DishId == dishId &&
                item.DataStatus.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        if (string.IsNullOrWhiteSpace(dish.ImageUrl))
        {
            return BadRequest(new { message = "Món ăn hiện chưa có hình ảnh." });
        }

        var imageDirectory = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "mon-an");
        var oldImageUrl = dish.ImageUrl;
        dish.ImageUrl = null;

        await _context.SaveChangesAsync(cancellationToken);
        DeleteExistingImage(oldImageUrl, imageDirectory);

        return Ok(new
        {
            message = "Xóa hình ảnh món ăn thành công.",
            dish.DishId,
            dish.DishCode,
            dish.DishName,
            dish.ImageUrl
        });
    }

    private static DishDto ToDto(Dish dish, Status? status = null) => new()
    {
        DishId = dish.DishId,
        DishCode = dish.DishCode,
        DishName = dish.DishName,
        Category = dish.Category,
        Price = dish.Price,
        ImageUrl = dish.ImageUrl,
        Status = (status ?? dish.Status).StatusCode,
        StatusName = (status ?? dish.Status).StatusName
    };

    private async Task<DataStatus> GetDataStatusAsync(
        string code,
        CancellationToken cancellationToken) =>
        await _context.DataStatuses.SingleOrDefaultAsync(
            status => status.DataStatusCode == code,
            cancellationToken) ?? throw new InvalidOperationException(
            $"Thiếu trạng thái dữ liệu {code}.");

    private static void DeleteExistingImage(string? imageUrl, string imageDirectory)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        var existingFilePath = Path.Combine(imageDirectory, Path.GetFileName(imageUrl));
        if (System.IO.File.Exists(existingFilePath))
        {
            System.IO.File.Delete(existingFilePath);
        }
    }

    private static bool HasValidImageSignature(string extension, byte[] header, int bytesRead) => extension switch
    {
        ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        ".png" => bytesRead >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytesRead >= 12 &&
            header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}

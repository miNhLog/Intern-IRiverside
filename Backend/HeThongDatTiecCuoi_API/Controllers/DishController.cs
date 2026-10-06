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
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
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

        var imageDirectory = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "mon-an");
        Directory.CreateDirectory(imageDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(imageDirectory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        DeleteExistingImage(dish.ImageUrl, imageDirectory);
        dish.ImageUrl = $"/uploads/mon-an/{fileName}";

        await _context.SaveChangesAsync(cancellationToken);
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
        DeleteExistingImage(dish.ImageUrl, imageDirectory);
        dish.ImageUrl = null;

        await _context.SaveChangesAsync(cancellationToken);

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
}

using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
using HeThongDatTiecCuoi_WEB.Models.ManagerDish;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/mon-an")]
public sealed class ManagerDishController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public ManagerDishController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? category, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var token = Token(); if (token is null) return RedirectToAction("Login", "Auth");
        var result = await _api.GetManagerDishesAsync(keyword, category, status, page, 10, token, cancellationToken);
        var model = result.Value ?? new ManagerDishPageViewModel { ErrorMessage = result.Error ?? "Không thể tải danh sách món ăn." };
        model.Keyword = keyword; model.Category = category; model.Status = status;
        return View(model);
    }

    [HttpGet("{dishId:int}/chi-tiet")]
    public async Task<IActionResult> Detail(int dishId, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.GetManagerDishDetailAsync(dishId, token, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("them")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDishForm model, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        if (!ModelState.IsValid) { TempData["Error"] = "Vui lòng kiểm tra thông tin món ăn."; return RedirectToAction(nameof(Index)); }
        var result = await _api.CreateDishAsync(model, token, cancellationToken);
        if (!result.Succeeded || result.Value is null) { TempData["Error"] = result.Error ?? "Không thể tạo món ăn."; return RedirectToAction(nameof(Index)); }
        if (model.Image is { Length: > 0 })
        {
            await using var stream = model.Image.OpenReadStream();
            var upload = await _api.UploadDishImageAsync(result.Value.DishId, stream, model.Image.FileName, model.Image.ContentType, token, cancellationToken);
            if (!upload.Succeeded) { TempData["Error"] = "Đã tạo món ăn nhưng tải hình ảnh thất bại: " + upload.Error; return RedirectToAction(nameof(Index)); }
        }
        TempData["Success"] = "Thêm món ăn thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cap-nhat")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateDishForm model, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        if (!ModelState.IsValid) { TempData["Error"] = "Vui lòng kiểm tra thông tin món ăn."; return RedirectToAction(nameof(Index)); }
        var result = await _api.UpdateDishAsync(model.DishId, model, token, cancellationToken);
        if (!result.Succeeded) { TempData["Error"] = result.Error ?? "Không thể cập nhật món ăn."; return RedirectToAction(nameof(Index)); }
        if (model.NewImage is { Length: > 0 })
        {
            await using var stream = model.NewImage.OpenReadStream();
            var upload = await _api.UploadDishImageAsync(model.DishId, stream, model.NewImage.FileName, model.NewImage.ContentType, token, cancellationToken);
            if (!upload.Succeeded) { TempData["Error"] = "Đã cập nhật món ăn nhưng tải hình ảnh thất bại: " + upload.Error; return RedirectToAction(nameof(Index)); }
        }
        else if (model.DeleteImage)
        {
            var delete = await _api.DeleteDishImageAsync(model.DishId, token, cancellationToken);
            if (!delete.Succeeded) { TempData["Error"] = delete.Error ?? "Không thể xóa hình ảnh."; return RedirectToAction(nameof(Index)); }
        }
        TempData["Success"] = "Cập nhật món ăn thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{dishId:int}/trang-thai")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int dishId, string status, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.UpdateDishStatusAsync(dishId, status, token, cancellationToken);
        return result.Succeeded ? Ok(new { message = "Cập nhật trạng thái món ăn thành công." }) : BadRequest(new { message = result.Error });
    }

    private string? Token() => Request.Cookies[TokenCookie];
}

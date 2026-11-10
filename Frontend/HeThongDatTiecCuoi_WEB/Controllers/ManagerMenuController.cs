using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
using HeThongDatTiecCuoi_WEB.Models.ManagerMenu;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/thuc-don")]
public sealed class ManagerMenuController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public ManagerMenuController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var token = Token();
        if (token is null) return RedirectToAction("Login", "Auth");
        var result = await _api.GetManagerMenusAsync(keyword, status, page, 6, token, cancellationToken);
        var model = result.Value ?? new ManagerMenuPageViewModel { ErrorMessage = result.Error ?? "Không thể tải danh sách thực đơn." };
        model.Keyword = keyword;
        model.Status = status;
        return View(model);
    }

    [HttpGet("{menuId:int}/chi-tiet")]
    public async Task<IActionResult> Detail(int menuId, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.GetMenuDishesAsync(menuId, token, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("mon-co-the-them")]
    public async Task<IActionResult> AvailableDishes(CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.GetDishesAsync(null, null, DishStatusCodes.Active, token, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("them")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMenuForm model, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        model.DishIds = model.DishIds.Where(dishId => dishId > 0).Distinct().ToList();
        if (model.DishIds.Count == 0)
        {
            TempData["Error"] = "Vui lòng chọn ít nhất một món cho thực đơn.";
            return RedirectToAction(nameof(Index));
        }
        if (!ModelState.IsValid) { TempData["Error"] = "Vui lòng kiểm tra thông tin thực đơn."; return RedirectToAction(nameof(Index)); }
        var result = await _api.CreateMenuAsync(model, token, cancellationToken);
        if (result.Succeeded && result.Value is not null && model.Image is { Length: > 0 })
        {
            await using var stream = model.Image.OpenReadStream();
            var upload = await _api.UploadMenuImageAsync(result.Value.MenuId, stream,
                model.Image.FileName, model.Image.ContentType, token, cancellationToken);
            TempData[upload.Succeeded ? "Success" : "Error"] = upload.Succeeded
                ? $"Đã thêm thực đơn gồm {model.DishIds.Count} món và ảnh đại diện."
                : "Đã thêm thực đơn nhưng tải ảnh thất bại: " + upload.Error;
        }
        else
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? $"Đã thêm thực đơn gồm {model.DishIds.Count} món."
                : result.Error;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cap-nhat")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateMenuForm model, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.UpdateMenuAsync(model.MenuId, model, token, cancellationToken);
        if (result.Succeeded && model.NewImage is { Length: > 0 })
        {
            await using var stream = model.NewImage.OpenReadStream();
            var upload = await _api.UploadMenuImageAsync(model.MenuId, stream,
                model.NewImage.FileName, model.NewImage.ContentType, token, cancellationToken);
            TempData[upload.Succeeded ? "Success" : "Error"] = upload.Succeeded
                ? "Cập nhật thực đơn và ảnh đại diện thành công."
                : "Đã cập nhật thực đơn nhưng tải ảnh thất bại: " + upload.Error;
        }
        else
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Cập nhật thực đơn thành công." : result.Error;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{menuId:int}/trang-thai")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int menuId, string status, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.UpdateMenuStatusAsync(menuId, status, token, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Cập nhật trạng thái thành công." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("them-mon")][ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAdd(BatchAddMenuDishesForm model, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.BatchAddMenuDishesAsync(model.MenuId, model.DishIds, token, cancellationToken);
        return result.Succeeded ? Ok(new { message = "Đã thêm món vào thực đơn." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{menuId:int}/bo-mon/{dishId:int}")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int menuId, int dishId, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.RemoveDishFromMenuAsync(menuId, dishId, token, cancellationToken);
        return result.Succeeded ? Ok(new { message = "Đã bỏ món khỏi thực đơn." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{menuId:int}/sap-xep")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int menuId, [FromForm] List<int> dishIds, CancellationToken cancellationToken)
    {
        var token = Token(); if (token is null) return Unauthorized();
        var result = await _api.ReorderMenuDishesAsync(menuId, dishIds, token, cancellationToken);
        return result.Succeeded ? Ok(new { message = "Đã lưu thứ tự món." }) : BadRequest(new { message = result.Error });
    }

    private string? Token() => Request.Cookies[TokenCookie];
}

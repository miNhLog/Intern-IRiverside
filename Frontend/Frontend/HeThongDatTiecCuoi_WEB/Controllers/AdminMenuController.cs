using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class AdminMenuController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminMenuController(
        IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? menuKeyword,
        string? menuStatus,
        string? dishKeyword,
        string? category,
        string? dishStatus,
        int? menuId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var model = new MenuManagementViewModel
        {
            MenuKeyword = menuKeyword,
            MenuStatus = menuStatus,
            DishKeyword = dishKeyword,
            Category = category,
            DishStatus = dishStatus,
            SelectedMenuId = menuId
        };

        var menuResult =
            await _apiClient.GetMenusAsync(
                menuKeyword,
                menuStatus,
                accessToken,
                cancellationToken);

        if (menuResult.Succeeded &&
            menuResult.Value is not null)
        {
            model.Menus =
                menuResult.Value;
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                menuResult.Error
                ?? "Không thể tải danh sách thực đơn.");
        }

        var dishResult =
            await _apiClient.GetDishesAsync(
                dishKeyword,
                category,
                dishStatus,
                accessToken,
                cancellationToken);

        if (dishResult.Succeeded &&
            dishResult.Value is not null)
        {
            model.Dishes =
                dishResult.Value;

            if (string.IsNullOrWhiteSpace(category))
            {
                model.DishesForCounts = dishResult.Value;
            }
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                dishResult.Error
                ?? "Không thể tải danh sách món ăn.");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var dishCountResult =
                await _apiClient.GetDishesAsync(
                    dishKeyword,
                    null,
                    dishStatus,
                    accessToken,
                    cancellationToken);

            if (dishCountResult.Succeeded &&
                dishCountResult.Value is not null)
            {
                model.DishesForCounts =
                    dishCountResult.Value;
            }
        }

        // Danh sách riêng cho dropdown thêm món.
        var activeDishResult =
            await _apiClient.GetDishesAsync(
                null,
                null,
                DishStatusCodes.Active,
                accessToken,
                cancellationToken);

        if (activeDishResult.Succeeded &&
            activeDishResult.Value is not null)
        {
            model.ActiveDishes =
                activeDishResult.Value;
        }

        if (!model.SelectedMenuId.HasValue)
        {
            model.SelectedMenuId =
                model.Menus
                    .FirstOrDefault()
                    ?.MenuId;
        }

        if (model.SelectedMenuId.HasValue)
        {
            var menuDishResult =
                await _apiClient.GetMenuDishesAsync(
                    model.SelectedMenuId.Value,
                    accessToken,
                    cancellationToken);

            if (menuDishResult.Succeeded &&
                menuDishResult.Value is not null)
            {
                model.MenuDishes =
                    menuDishResult.Value;
            }
            else if (!menuDishResult.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    menuDishResult.Error
                    ?? "Không thể tải chi tiết thực đơn.");
            }
        }

        return View(model);
    }


    // =========================================================
    // CRUD THỰC ĐƠN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMenu(
        CreateMenuForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Vui lòng kiểm tra lại thông tin thực đơn.";

            return RedirectToAction(nameof(Index));
        }

        var result =
            await _apiClient.CreateMenuAsync(
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded ||
            result.Value is null)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể tạo thực đơn.";

            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] =
            "Thêm thực đơn thành công.";

        return RedirectToAction(
            nameof(Index),
            new
            {
                menuId = result.Value.MenuId
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMenu(
        UpdateMenuForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.UpdateMenuAsync(
                model.MenuId,
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể cập nhật thực đơn.";
        }
        else
        {
            TempData["Success"] =
                "Cập nhật thực đơn thành công.";
        }

        return RedirectToAction(
            nameof(Index),
            new
            {
                menuId = model.MenuId
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMenuStatus(
        int menuId,
        string status,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.UpdateMenuStatusAsync(
                menuId,
                status,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Cập nhật trạng thái thực đơn thành công."
                : result.Error
                  ?? "Không thể cập nhật trạng thái thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { menuId });
    }


    // =========================================================
    // CRUD MÓN ĂN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDish(
        CreateDishForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Vui lòng kiểm tra lại thông tin món ăn.";

            return RedirectToAction(nameof(Index));
        }

        var result =
            await _apiClient.CreateDishAsync(
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded ||
            result.Value is null)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể thêm món ăn.";

            return RedirectToAction(nameof(Index));
        }

        if (model.Image is not null &&
            model.Image.Length > 0)
        {
            await using var stream =
                model.Image.OpenReadStream();

            var uploadResult =
                await _apiClient.UploadDishImageAsync(
                    result.Value.DishId,
                    stream,
                    model.Image.FileName,
                    model.Image.ContentType,
                    accessToken,
                    cancellationToken);

            if (!uploadResult.Succeeded)
            {
                TempData["Error"] =
                    "Đã tạo món nhưng tải ảnh thất bại: "
                    + uploadResult.Error;

                return RedirectToAction(nameof(Index));
            }
        }

        TempData["Success"] =
            "Thêm món ăn thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDish(
        UpdateDishForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.UpdateDishAsync(
                model.DishId,
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể cập nhật món ăn.";

            return RedirectToAction(nameof(Index));
        }

        if (model.NewImage is not null &&
            model.NewImage.Length > 0)
        {
            await using var stream =
                model.NewImage.OpenReadStream();

            var uploadResult =
                await _apiClient.UploadDishImageAsync(
                    model.DishId,
                    stream,
                    model.NewImage.FileName,
                    model.NewImage.ContentType,
                    accessToken,
                    cancellationToken);

            if (!uploadResult.Succeeded)
            {
                TempData["Error"] =
                    "Đã sửa thông tin món nhưng tải ảnh thất bại: "
                    + uploadResult.Error;

                return RedirectToAction(nameof(Index));
            }
        }
        else if (model.DeleteImage)
        {
            var deleteImageResult =
                await _apiClient.DeleteDishImageAsync(
                    model.DishId,
                    accessToken,
                    cancellationToken);

            if (!deleteImageResult.Succeeded)
            {
                TempData["Error"] =
                    deleteImageResult.Error
                    ?? "Không thể xóa hình ảnh món ăn.";

                return RedirectToAction(nameof(Index));
            }
        }

        TempData["Success"] =
            "Cập nhật món ăn thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDishStatus(
        int dishId,
        string status,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.UpdateDishStatusAsync(
                dishId,
                status,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Cập nhật trạng thái món ăn thành công."
                : result.Error
                  ?? "Không thể cập nhật trạng thái món ăn.";

        return RedirectToAction(nameof(Index));
    }


    // =========================================================
    // CHI TIẾT THỰC ĐƠN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDishToMenu(
        int menuId,
        int dishId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.AddDishToMenuAsync(
                menuId,
                dishId,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Thêm món vào thực đơn thành công."
                : result.Error
                  ?? "Không thể thêm món vào thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { menuId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDishFromMenu(
        int menuId,
        int dishId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.RemoveDishFromMenuAsync(
                menuId,
                dishId,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Bỏ món khỏi thực đơn thành công."
                : result.Error
                  ?? "Không thể bỏ món khỏi thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { menuId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderMenuDishes(
        int menuId,
        string dishIds,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var ids =
            (dishIds ?? string.Empty)
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                    int.TryParse(x, out var id)
                        ? id
                        : 0)
                .Where(x => x > 0)
                .ToList();

        if (ids.Count == 0)
        {
            TempData["Error"] =
                "Danh sách sắp xếp không hợp lệ.";

            return RedirectToAction(
                nameof(Index),
                new { menuId });
        }

        var result =
            await _apiClient.ReorderMenuDishesAsync(
                menuId,
                ids,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Sắp xếp món ăn thành công."
                : result.Error
                  ?? "Không thể sắp xếp món ăn.";

        return RedirectToAction(
            nameof(Index),
            new { menuId });
    }


    // =========================================================
    // HELPER
    // =========================================================

    private string? GetAccessToken() =>
        Request.Cookies[ApiTokenCookie];
}

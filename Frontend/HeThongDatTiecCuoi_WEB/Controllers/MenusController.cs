using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Menus;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("menus")]
public sealed class MenusController : Controller
{
    private const int PageSize = 6;
    private readonly IRiversideApiClient _apiClient;

    public MenusController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? keyword,
        string? priceRange,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Index", "AdminMenu");
        }

        if (User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction("Dashboard", "Home");
        }

        page = Math.Max(1, page);
        sort = string.IsNullOrWhiteSpace(sort) ? "name-asc" : sort;

        var result = await _apiClient.GetPublicMenusAsync(
            keyword,
            priceRange,
            sort,
            page,
            PageSize,
            cancellationToken);

        var response = result.Value;
        if (response is not null && response.TotalPages > 0 && page > response.TotalPages)
        {
            return RedirectToAction(nameof(Index), new
            {
                keyword,
                priceRange,
                sort,
                page = response.TotalPages
            });
        }

        return View(new MenuListViewModel
        {
            Menus = response?.Items ?? [],
            Keyword = keyword?.Trim(),
            PriceRange = priceRange,
            Sort = sort,
            Page = response?.Page ?? page,
            PageSize = response?.PageSize ?? PageSize,
            TotalItems = response?.TotalItems ?? 0,
            TotalPages = response?.TotalPages ?? 0,
            ErrorMessage = result.Succeeded
                ? null
                : result.Error ?? "Không thể tải danh sách thực đơn. Vui lòng thử lại sau."
        });
    }

    [HttpGet("{menuId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        int menuId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Index", "AdminMenu");
        }

        if (User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction("Dashboard", "Home");
        }

        var result = await _apiClient.GetPublicMenuDetailAsync(menuId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View(new MenuDetailViewModel { IsNotFound = true });
            }

            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new MenuDetailViewModel
            {
                ErrorMessage = "Không thể tải thông tin thực đơn. Vui lòng thử lại sau."
            });
        }

        var menu = result.Value;
        return View(new MenuDetailViewModel
        {
            MenuId = menu.MenuId,
            MenuCode = menu.MenuCode,
            MenuName = menu.MenuName,
            Description = menu.Description,
            PricePerTable = menu.PricePerTable,
            DishCount = menu.DishCount,
            Dishes = menu.Dishes
        });
    }
}

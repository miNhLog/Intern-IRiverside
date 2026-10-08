using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Halls;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("halls")]
public sealed class HallsController : Controller
{
    private const int PageSize = 6;
    private readonly IRiversideApiClient _apiClient;

    public HallsController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? keyword,
        string? capacity,
        string? priceRange,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (User.IsInRole(RoleNames.Admin))
            return RedirectToAction("Dashboard", "AdminAccount");

        if (User.IsInRole(RoleNames.Coordinator))
            return RedirectToAction("Dashboard", "Home");

        page = Math.Max(1, page);
        sort = string.IsNullOrWhiteSpace(sort) ? "name-asc" : sort;
        var result = await _apiClient.GetPublicHallsAsync(
            keyword, capacity, priceRange, sort, page, PageSize, cancellationToken);

        var response = result.Value;
        if (response is not null && response.TotalPages > 0 && page > response.TotalPages)
        {
            return RedirectToAction(nameof(Index), new
            {
                keyword,
                capacity,
                priceRange,
                sort,
                page = response.TotalPages
            });
        }

        return View(new HallListViewModel
        {
            Halls = response?.Items ?? [],
            Keyword = keyword?.Trim(),
            Capacity = capacity,
            PriceRange = priceRange,
            Sort = sort,
            Page = response?.Page ?? page,
            PageSize = response?.PageSize ?? PageSize,
            TotalItems = response?.TotalItems ?? 0,
            TotalPages = response?.TotalPages ?? 0,
            ErrorMessage = result.Succeeded
                ? null
                : result.Error ?? "Không thể tải danh sách sảnh. Vui lòng thử lại sau."
        });
    }

    [HttpGet("{hallId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        int hallId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Admin))
            return RedirectToAction("Dashboard", "AdminAccount");

        if (User.IsInRole(RoleNames.Coordinator))
            return RedirectToAction("Dashboard", "Home");

        var result = await _apiClient.GetPublicHallAsync(hallId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View(new HallDetailViewModel { IsNotFound = true });
            }

            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new HallDetailViewModel
            {
                ErrorMessage = "Không thể tải thông tin sảnh. Vui lòng thử lại sau."
            });
        }

        var hall = result.Value;
        return View(new HallDetailViewModel
        {
            HallId = hall.HallId,
            HallCode = hall.HallCode,
            HallName = hall.HallName,
            MinimumCapacity = hall.MinimumCapacity,
            MaximumCapacity = hall.MaximumCapacity,
            RentalPrice = hall.RentalPrice,
            Description = hall.Description,
            ImageUrl = hall.ImageUrl,
            Status = hall.Status,
            StatusName = hall.StatusName
        });
    }
}

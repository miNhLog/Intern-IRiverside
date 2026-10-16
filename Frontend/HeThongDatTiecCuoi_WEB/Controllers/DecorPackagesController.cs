using HeThongDatTiecCuoi_WEB.Models.DecorPackages;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("decor-packages")]
public sealed class DecorPackagesController : Controller
{
    private const int PageSize = 6;
    private readonly IRiversideApiClient _apiClient;

    public DecorPackagesController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? keyword,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        sort = string.IsNullOrWhiteSpace(sort) ? "name-asc" : sort;
        var result = await _apiClient.GetPublicDecorPackagesAsync(
            keyword, sort, page, PageSize, cancellationToken);

        var response = result.Value;
        if (response is not null && response.TotalPages > 0 && page > response.TotalPages)
        {
            return RedirectToAction(nameof(Index), new
            {
                keyword,
                sort,
                page = response.TotalPages
            });
        }

        return View(new DecorPackageListViewModel
        {
            Packages = response?.Items ?? [],
            Keyword = keyword?.Trim(),
            Sort = sort,
            Page = response?.Page ?? page,
            PageSize = response?.PageSize ?? PageSize,
            TotalItems = response?.TotalItems ?? 0,
            TotalPages = response?.TotalPages ?? 0,
            ErrorMessage = result.Succeeded
                ? null
                : result.Error ?? "Không thể tải danh sách gói trang trí. Vui lòng thử lại sau."
        });
    }

    [HttpGet("{decorationPackageId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        int decorationPackageId,
        CancellationToken cancellationToken)
    {
        var result = await _apiClient.GetPublicDecorPackageAsync(
            decorationPackageId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View(new DecorPackageDetailViewModel { IsNotFound = true });
            }

            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new DecorPackageDetailViewModel
            {
                ErrorMessage = "Không thể tải thông tin gói trang trí. Vui lòng thử lại sau."
            });
        }

        var package = result.Value;
        return View(new DecorPackageDetailViewModel
        {
            DecorationPackageId = package.DecorationPackageId,
            PackageName = package.PackageName,
            Style = package.Style,
            Description = package.Description,
            Price = package.Price,
            ImageUrl = package.ImageUrl
        });
    }
}

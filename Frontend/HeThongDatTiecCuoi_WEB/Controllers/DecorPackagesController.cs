using HeThongDatTiecCuoi_WEB.Models.DecorPackages;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("decor-packages")]
public sealed class DecorPackagesController : Controller
{
    private const int PageSize = 20;
    private readonly IRiversideApiClient _apiClient;

    public DecorPackagesController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        // Load every API page so the deck includes the complete catalogue.
        var result = await _apiClient.GetPublicDecorPackagesAsync(
            null, "name-asc", 1, PageSize, cancellationToken);
        var response = result.Value;
        var packages = response?.Items.ToList() ?? [];

        if (result.Succeeded && response is not null)
        {
            for (var page = 2; page <= response.TotalPages; page++)
            {
                result = await _apiClient.GetPublicDecorPackagesAsync(
                    null, "name-asc", page, PageSize, cancellationToken);
                if (!result.Succeeded || result.Value is null) break;
                packages.AddRange(result.Value.Items);
            }
        }

        return View(new DecorPackageListViewModel
        {
            Packages = packages,
            PageSize = PageSize,
            TotalItems = packages.Count,
            TotalPages = packages.Count > 0 ? 1 : 0,
            ErrorMessage = result.Succeeded && result.Value is not null
                ? null
                : result.Error ??
                  "Không thể tải danh sách gói trang trí. Vui lòng thử lại sau."
        });
    }

    [HttpGet("{decorationPackageId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        int decorationPackageId,
        CancellationToken cancellationToken)
    {
        var result = await _apiClient.GetPublicDecorPackageAsync(
            decorationPackageId,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;

                return View(new DecorPackageDetailViewModel
                {
                    IsNotFound = true
                });
            }

            Response.StatusCode =
                StatusCodes.Status503ServiceUnavailable;

            return View(new DecorPackageDetailViewModel
            {
                ErrorMessage =
                    "Không thể tải thông tin gói trang trí. Vui lòng thử lại sau."
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

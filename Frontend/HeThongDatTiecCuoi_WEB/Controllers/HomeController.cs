using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Home;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public HomeController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(
        bool publicSite = false,
        CancellationToken cancellationToken = default)
    {
        if (!publicSite && User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        if (!publicSite && User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _apiClient.GetFeaturedHallsAsync(cancellationToken);
        return View(new HomeViewModel
        {
            FeaturedHalls = result.Succeeded && result.Value is not null
                ? result.Value
                : [],
            FeaturedHallsUnavailable = !result.Succeeded
        });
    }


    [Authorize]
    public IActionResult Dashboard()
    {
        // Admin truy cập trực tiếp /Home/Dashboard
        // được đưa về trang tổng quan hệ thống.
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Dashboard", "AdminAccount");
        }

        return View();
    }

    [Authorize(Roles = RoleNames.Manager)]
    public IActionResult HallManagerAssignments()
    {
        return View();
    }

    [Authorize(Roles = RoleNames.HallManager)]
    public IActionResult CoordinationAssignments()
    {
        return View();
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public IActionResult AssignedParties()
    {
        return View();
    }

    [Authorize(Roles = RoleNames.Coordinator)]
    public IActionResult AssignedPartyDetail(int bookingId)
    {
        ViewData["BookingId"] = bookingId;
        return View();
    }


    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

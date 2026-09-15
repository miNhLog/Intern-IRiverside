using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (User.IsInRole("Quản trị viên"))
        {
            return RedirectToAction("Index", "AdminSanh");
        }

        return RedirectToAction(nameof(Dashboard));
    }


    [Authorize]
    public IActionResult Dashboard()
    {
        // Admin truy cập trực tiếp /Home/Dashboard
        // cũng chuyển về trang quản lý sảnh
        if (User.IsInRole("Quản trị viên"))
        {
            return RedirectToAction("Index", "AdminSanh");
        }

        return View();
    }


    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
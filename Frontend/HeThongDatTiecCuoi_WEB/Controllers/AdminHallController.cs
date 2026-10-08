using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;
using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
public sealed class AdminHallController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminHallController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("admin/quan-ly-sanh")]
    public async Task<IActionResult> Index(
        DateTime? date,
        int? hallId,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        // Nếu mất token, xóa phiên đăng nhập cũ để tránh AuthController đá ngược lại
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete(ApiTokenCookie);
            return RedirectToAction("Login", "Auth");
        }

        var referenceDate = (date ?? DateTime.Today).Date;
        var daysSinceMonday =
            (7 + ((int)referenceDate.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        var weekStartDate = referenceDate.AddDays(-daysSinceMonday);

        var hallsResult = await _apiClient.GetHallsAsync(
            accessToken,
            cancellationToken);

        var scheduleResult = await _apiClient.GetWeeklyHallSchedulesAsync(
            weekStartDate,
            hallId,
            accessToken,
            cancellationToken);

        var model = new HallManagementViewModel
        {
            StartDate = weekStartDate,
            HallId = hallId
        };

        if (hallsResult.Succeeded && hallsResult.Value is not null)
        {
            model.Halls = hallsResult.Value;
        }
        else
        {
            model.ErrorMessage = hallsResult.Error
                ?? "Không thể tải danh sách sảnh.";
        }

        if (scheduleResult.Succeeded && scheduleResult.Value is not null)
        {
            model.WeeklySchedule = scheduleResult.Value;
        }
        else
        {
            model.ErrorMessage ??= scheduleResult.Error
                ?? "Không thể tải lịch sảnh.";
        }

        return View(model);
    }

    [HttpPost("admin/quan-ly-sanh/lich/{hallScheduleId:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHallScheduleStatus(
        int hallScheduleId,
        string status,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (status != HallScheduleStatusCodes.Available &&
            status != HallScheduleStatusCodes.Locked)
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái lịch không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateHallScheduleStatusAsync(
            hallScheduleId,
            status,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error ?? "Không thể cập nhật lịch sảnh."
            });
        }

        return Json(new
        {
            success = true,
            message = "Cập nhật trạng thái lịch thành công."
        });
    }

    [HttpPost("admin/quan-ly-sanh/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHall(
        HallDto model,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (string.IsNullOrWhiteSpace(model.HallCode))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập mã sảnh."
            });
        }

        if (string.IsNullOrWhiteSpace(model.HallName))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập tên sảnh."
            });
        }

        if (model.MaximumCapacity <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (model.MinimumCapacity.HasValue &&
            model.MinimumCapacity.Value > model.MaximumCapacity)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (model.RentalPrice < 0)
        {
            return Json(new
            {
                success = false,
                message = "Giá thuê không hợp lệ."
            });
        }

        model.Status = HallStatusCodes.Active;

        var result = await _apiClient.CreateHallAsync(
            model,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error ?? "Không thể thêm sảnh."
            });
        }

        return Json(new
        {
            success = true,
            message = "Thêm sảnh tiệc thành công."
        });
    }

    [HttpPost("admin/quan-ly-sanh/sua/{hallId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHall(
        int hallId,
        HallDto model,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (string.IsNullOrWhiteSpace(model.HallCode))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập mã sảnh."
            });
        }

        if (string.IsNullOrWhiteSpace(model.HallName))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập tên sảnh."
            });
        }

        if (model.MaximumCapacity <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (model.MinimumCapacity.HasValue &&
            model.MinimumCapacity.Value > model.MaximumCapacity)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (model.RentalPrice < 0)
        {
            return Json(new
            {
                success = false,
                message = "Giá thuê không hợp lệ."
            });
        }

        model.HallId = hallId;

        var result = await _apiClient.UpdateHallAsync(
            hallId,
            model,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error ?? "Không thể cập nhật sảnh."
            });
        }

        return Json(new
        {
            success = true,
            message = "Cập nhật sảnh thành công."
        });
    }

    [HttpPost("admin/quan-ly-sanh/status/{hallId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateHallStatus(
        int hallId,
        string status,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        var validStatuses = new[]
        {
            HallStatusCodes.Active,
            HallStatusCodes.Maintenance,
            HallStatusCodes.Inactive
        };

        if (!validStatuses.Contains(status))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateHallStatusAsync(
            hallId,
            status,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error ?? "Không thể cập nhật trạng thái sảnh."
            });
        }

        return Json(new
        {
            success = true,
            message = "Cập nhật trạng thái sảnh thành công."
        });
    }

    [HttpPost("admin/quan-ly-sanh/xoa/{hallId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHall(
        int hallId,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        var result = await _apiClient.DeleteHallAsync(
            hallId,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error ?? "Không thể xóa sảnh."
            });
        }

        return Json(new
        {
            success = true,
            message = "Xóa sảnh thành công."
        });
    }
}

using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("admin/quan-ly-tai-khoan")]
public sealed class AdminAccountController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminAccountController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? keyword,
        int? roleId,
        string? status,
        bool? mustChangePassword,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var rolesResult = await _apiClient.GetRolesAsync(
            accessToken,
            cancellationToken);

        var accountsResult = await _apiClient.GetAccountsAsync(
            accessToken,
            keyword,
            roleId,
            status,
            mustChangePassword,
            cancellationToken);

        var adminRoleId = rolesResult.Value?
            .FirstOrDefault(role => role.RoleName == RoleNames.Admin)?
            .RoleId;
        var adminResult = adminRoleId.HasValue
            ? await _apiClient.GetAccountsAsync(
                accessToken,
                null,
                adminRoleId,
                null,
                null,
                cancellationToken)
            : null;

        var model = new AccountManagementViewModel
        {
            Keyword = keyword,
            RoleId = roleId,
            Status = status,
            MustChangePassword = mustChangePassword,
            AdminAccount = adminResult?.Succeeded == true
                ? adminResult.Value?.FirstOrDefault(account => account.RoleName == RoleNames.Admin)
                : null,
            Accounts = accountsResult.Succeeded && accountsResult.Value is not null
                ? accountsResult.Value.Where(account =>
                    account.RoleName == RoleNames.Manager ||
                    account.RoleName == RoleNames.HallManager ||
                    account.RoleName == RoleNames.Coordinator).ToList()
                : new List<AccountDto>(),
            Roles = rolesResult.Succeeded && rolesResult.Value is not null
                ? rolesResult.Value
                : new List<RoleDto>()
        };

        if (!accountsResult.Succeeded)
        {
            model.ErrorMessage = accountsResult.Error
                ?? "Không thể tải danh sách tài khoản.";
        }
        else if (!rolesResult.Succeeded)
        {
            model.ErrorMessage = rolesResult.Error
                ?? "Không thể tải danh sách vai trò.";
        }
        else if (adminResult is not null && !adminResult.Succeeded)
        {
            model.ErrorMessage = adminResult.Error
                ?? "Không thể tải tài khoản Quản trị viên.";
        }

        return View(model);
    }

    [HttpGet("tong-quan")]
    public async Task<IActionResult> Dashboard(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var dashboardResult = await _apiClient.GetSystemDashboardAsync(
            accessToken,
            fromDate,
            toDate,
            cancellationToken);

        if (dashboardResult.Succeeded && dashboardResult.Value is not null)
        {
            return View(dashboardResult.Value);
        }

        var today = DateTime.Today;
        return View(new SystemDashboardViewModel
        {
            FromDate = fromDate?.Date ?? new DateTime(today.Year, today.Month, 1),
            ToDate = toDate?.Date ?? new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            ErrorMessage = dashboardResult.Error ?? "Không thể tải dữ liệu tổng quan hệ thống."
        });
    }

    [HttpPost("them-nhan-vien")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(
        [FromForm] CreateEmployeeAccountRequest model,
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

        var result = await _apiClient.CreateEmployeeAccountAsync(
            model,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể tạo tài khoản nhân viên."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Tạo tài khoản nhân viên thành công."
        });
    }

    [HttpPost("sua-quan-tri-vien/{userId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdministrator(
        int userId,
        [FromForm] UpdateAdministratorAccountRequest model,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        }

        if (userId <= 0 || string.IsNullOrWhiteSpace(model.Email))
        {
            return Json(new { success = false, message = "Thông tin Quản trị viên không hợp lệ." });
        }

        var result = await _apiClient.UpdateAdministratorAccountAsync(
            userId,
            model,
            accessToken,
            cancellationToken);

        return Json(new
        {
            success = result.Succeeded,
            message = result.Succeeded
                ? result.Value?.Message ?? "Cập nhật Quản trị viên thành công."
                : result.Error ?? "Không thể cập nhật Quản trị viên."
        });
    }

    [HttpPost("doi-mat-khau-quan-tri-vien")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAdministratorPassword(
        [FromForm] ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        }

        if (!ModelState.IsValid)
        {
            var validationMessage = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            return Json(new
            {
                success = false,
                message = validationMessage ?? "Thông tin mật khẩu không hợp lệ."
            });
        }

        var result = await _apiClient.ChangePasswordAsync(
            model,
            accessToken,
            cancellationToken);

        return Json(new
        {
            success = result.Succeeded,
            message = result.Succeeded
                ? result.Value?.Message ?? "Đổi mật khẩu thành công."
                : result.Error ?? "Không thể đổi mật khẩu."
        });
    }

    [HttpPost("sua-nhan-vien/{userId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployee(
        int userId,
        [FromForm] UpdateEmployeeAccountRequest model,
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

        if (userId <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản nhân viên không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateEmployeeAccountAsync(
            userId,
            model,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể cập nhật thông tin nhân viên."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Cập nhật nhân viên thành công."
        });
    }

    [HttpPost("trang-thai/{userId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccountStatus(
        int userId,
        [FromForm] string status,
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

        if (userId <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không hợp lệ."
            });
        }

        var validStatuses = new[]
        {
            AccountStatusCodes.Active,
            AccountStatusCodes.Locked
        };

        if (!validStatuses.Contains(status))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateAccountStatusAsync(
            userId,
            status,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể cập nhật trạng thái tài khoản."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Cập nhật trạng thái tài khoản thành công."
        });
    }

    [HttpPost("trang-thai-nhan-vien/{userId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployeeStatus(
        int userId,
        [FromForm] string status,
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

        if (userId <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Nhân viên không hợp lệ."
            });
        }

        var validStatuses = new[]
        {
            EmployeeStatusCodes.Active,
            EmployeeStatusCodes.OnLeave,
            EmployeeStatusCodes.Terminated
        };

        if (!validStatuses.Contains(status))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái nhân viên không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateEmployeeStatusAsync(
            userId,
            status,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể cập nhật trạng thái nhân viên."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Cập nhật trạng thái nhân viên thành công."
        });
    }

    [HttpPost("gui-lien-ket-dat-lai-mat-khau/{userId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPasswordResetLink(
        int userId,
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

        if (userId <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không hợp lệ."
            });
        }

        var result = await _apiClient.SendPasswordResetLinkAsync(
            userId,
            accessToken,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể gửi liên kết đặt lại mật khẩu."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Đã gửi liên kết đặt lại mật khẩu."
        });
    }
}

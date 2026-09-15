using HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = "Quản trị viên")]
[Route("admin/quan-ly-tai-khoan")]
public sealed class AdminTaiKhoanController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminTaiKhoanController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }


    // GET:
    // /admin/quan-ly-tai-khoan
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? tuKhoa,
        int? vaiTroId,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction(
                "Login",
                "Auth"
            );
        }


        // ==========================================
        // 1. LẤY DANH SÁCH TÀI KHOẢN
        // ==========================================

        var taiKhoanResult =
            await _apiClient.GetDanhSachTaiKhoanAsync(
                accessToken,
                tuKhoa,
                vaiTroId,
                trangThai,
                cancellationToken
            );


        // ==========================================
        // 2. LẤY DANH SÁCH VAI TRÒ
        // ==========================================

        var vaiTroResult =
            await _apiClient.GetDanhSachVaiTroAsync(
                accessToken,
                cancellationToken
            );


        // ==========================================
        // 3. TẠO VIEW MODEL
        // ==========================================

        var model = new QuanLyTaiKhoanViewModel
        {
            TuKhoa = tuKhoa,

            VaiTroID = vaiTroId,

            TrangThai = trangThai,

            DanhSachTaiKhoan =
                taiKhoanResult.Succeeded &&
                taiKhoanResult.Value != null
                    ? taiKhoanResult.Value
                    : new List<TaiKhoanDto>(),

            DanhSachVaiTro =
                vaiTroResult.Succeeded &&
                vaiTroResult.Value != null
                    ? vaiTroResult.Value
                    : new List<VaiTroDto>()
        };


        // ==========================================
        // 4. XỬ LÝ LỖI
        // ==========================================

        if (!taiKhoanResult.Succeeded)
        {
            model.Loi =
                taiKhoanResult.Error
                ?? "Không thể tải danh sách tài khoản.";
        }
        else if (!vaiTroResult.Succeeded)
        {
            model.Loi =
                vaiTroResult.Error
                ?? "Không thể tải danh sách vai trò.";
        }


        return View(model);
    }
    // POST:
    // /admin/quan-ly-tai-khoan/them-nhan-vien
    [HttpPost("them-nhan-vien")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemNhanVien(
        [FromForm] TaoTaiKhoanNhanVienRequest model,
        CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }


        var result =
            await _apiClient.TaoTaiKhoanNhanVienAsync(
                model,
                accessToken,
                cancellationToken
            );


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
    // POST:
    // /admin/quan-ly-tai-khoan/sua-nhan-vien/{id}
    [HttpPost("sua-nhan-vien/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaNhanVien(
        int id,
        [FromForm] CapNhatTaiKhoanNhanVienRequest model,
        CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }


        if (id <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản nhân viên không hợp lệ."
            });
        }


        var result =
            await _apiClient.CapNhatTaiKhoanNhanVienAsync(
                id,
                model,
                accessToken,
                cancellationToken
            );


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
    // POST:
    // /admin/quan-ly-tai-khoan/trang-thai/{id}
    [HttpPost("trang-thai/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThai(
        int id,
        [FromForm] string trangThai,
        CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (id <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không hợp lệ."
            });
        }

        var trangThaiHopLe = new[]
        {
        "Hoạt động",
        "Tạm khóa",
        "Ngừng hoạt động"
    };

        if (!trangThaiHopLe.Contains(trangThai))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        var result =
            await _apiClient.CapNhatTrangThaiTaiKhoanAsync(
                id,
                trangThai,
                accessToken,
                cancellationToken
            );

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
    [HttpPost("trang-thai-nhan-vien/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThaiNhanVien(
    int id,
    [FromForm] string trangThai,
    CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (id <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Nhân viên không hợp lệ."
            });
        }

        var trangThaiHopLe = new[]
        {
        "Đang làm việc",
        "Tạm nghỉ",
        "Đã nghỉ việc"
    };

        if (!trangThaiHopLe.Contains(trangThai))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái nhân viên không hợp lệ."
            });
        }

        var result =
            await _apiClient.CapNhatTrangThaiNhanVienAsync(
                id,
                trangThai,
                accessToken,
                cancellationToken
            );

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

    [HttpPost("dat-lai-mat-khau/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DatLaiMatKhau(
    int id,
    [FromForm] string matKhauMoi,
    CancellationToken cancellationToken)
    {
        var accessToken =
            Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(new
            {
                success = false,
                message = "Phiên đăng nhập đã hết hạn."
            });
        }

        if (id <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không hợp lệ."
            });
        }

        if (string.IsNullOrWhiteSpace(matKhauMoi))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập mật khẩu mới."
            });
        }

        var model = new DatLaiMatKhauRequest
        {
            MatKhauMoi = matKhauMoi
        };

        var result =
            await _apiClient.DatLaiMatKhauAsync(
                id,
                model,
                accessToken,
                cancellationToken
            );

        if (!result.Succeeded)
        {
            return Json(new
            {
                success = false,
                message = result.Error
                    ?? "Không thể đặt lại mật khẩu."
            });
        }

        return Json(new
        {
            success = true,
            message = result.Value?.Message
                ?? "Đặt lại mật khẩu thành công."
        });
    }

}
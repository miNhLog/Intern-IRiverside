using HeThongDatTiecCuoi_WEB.Models.AdminSanh;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = "Quản trị viên")]
public sealed class AdminSanhController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminSanhController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // GET: /admin/quan-ly-sanh
    [HttpGet("admin/quan-ly-sanh")]
    public async Task<IActionResult> Index(
        DateTime? ngay,
        int? sanhTiecId,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        // Nếu không truyền ngày thì lấy ngày hiện tại
        var ngayThamChieu = (ngay ?? DateTime.Today).Date;

        // Tìm thứ Hai của tuần đang chọn
        var soNgayLui =
            (7 + ((int)ngayThamChieu.DayOfWeek - (int)DayOfWeek.Monday)) % 7;

        var ngayBatDauTuan = ngayThamChieu.AddDays(-soNgayLui);

        // Lấy danh sách sảnh
        var ketQuaSanh = await _apiClient.GetDanhSachSanhAsync(
            accessToken,
            cancellationToken);

        // Lấy lịch sảnh theo tuần
        var ketQuaLich = await _apiClient.GetLichSanhTheoTuanAsync(
            ngayBatDauTuan,
            sanhTiecId,
            accessToken,
            cancellationToken);

        var model = new QuanLySanhViewModel
        {
            NgayBatDau = ngayBatDauTuan,
            SanhTiecId = sanhTiecId
        };

        if (ketQuaSanh.Succeeded && ketQuaSanh.Value is not null)
        {
            model.DanhSachSanh = ketQuaSanh.Value;
        }
        else
        {
            model.ErrorMessage = ketQuaSanh.Error
                ?? "Không thể tải danh sách sảnh.";
        }

        if (ketQuaLich.Succeeded && ketQuaLich.Value is not null)
        {
            model.LichTuan = ketQuaLich.Value;
        }
        else
        {
            model.ErrorMessage ??= ketQuaLich.Error
                ?? "Không thể tải lịch sảnh.";
        }

        return View(model);
    }
    [HttpPost("admin/quan-ly-sanh/lich/{id}/trang-thai")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTrangThaiLich(
    int id,
    string trangThai,
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

        if (trangThai != "Trống" && trangThai != "Tạm khóa")
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái lịch không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateTrangThaiLichAsync(
            id,
            trangThai,
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
    public async Task<IActionResult> CreateSanh(
    SanhTiecDto model,
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

        if (string.IsNullOrWhiteSpace(model.MaSanh))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập mã sảnh."
            });
        }

        if (string.IsNullOrWhiteSpace(model.TenSanh))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập tên sảnh."
            });
        }

        if (model.SucChuaToiDa <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (model.SucChuaToiThieu.HasValue &&
            model.SucChuaToiThieu.Value > model.SucChuaToiDa)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (model.GiaThue < 0)
        {
            return Json(new
            {
                success = false,
                message = "Giá thuê không hợp lệ."
            });
        }

        model.TrangThai = "Hoạt động";

        var result = await _apiClient.CreateSanhAsync(
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
    [HttpPost("admin/quan-ly-sanh/sua/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSanh(
    int id,
    SanhTiecDto model,
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

        if (string.IsNullOrWhiteSpace(model.MaSanh))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập mã sảnh."
            });
        }

        if (string.IsNullOrWhiteSpace(model.TenSanh))
        {
            return Json(new
            {
                success = false,
                message = "Vui lòng nhập tên sảnh."
            });
        }

        if (model.SucChuaToiDa <= 0)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (model.SucChuaToiThieu.HasValue &&
            model.SucChuaToiThieu.Value > model.SucChuaToiDa)
        {
            return Json(new
            {
                success = false,
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (model.GiaThue < 0)
        {
            return Json(new
            {
                success = false,
                message = "Giá thuê không hợp lệ."
            });
        }

        model.SanhTiecID = id;

        var result = await _apiClient.UpdateSanhAsync(
            id,
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
    [HttpPost("admin/quan-ly-sanh/trang-thai/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTrangThaiSanh(
    int id,
    string trangThai,
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

        var trangThaiHopLe = new[]
        {
        "Hoạt động",
        "Bảo trì",
        "Ngừng hoạt động"
    };

        if (!trangThaiHopLe.Contains(trangThai))
        {
            return Json(new
            {
                success = false,
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        var result = await _apiClient.UpdateTrangThaiSanhAsync(
            id,
            trangThai,
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
    [HttpPost("admin/quan-ly-sanh/xoa/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSanh(
    int id,
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

        var result = await _apiClient.DeleteSanhAsync(
            id,
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
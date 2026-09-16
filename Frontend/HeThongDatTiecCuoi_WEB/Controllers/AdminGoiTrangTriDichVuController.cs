using HeThongDatTiecCuoi_WEB.Models.AdminGoiTrangTriDichVu;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = "Quản trị viên")]
[Route("admin/quan-ly-goi-trang-tri-dich-vu")]
public sealed class AdminGoiTrangTriDichVuController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private const string DefaultTab = "goi-trang-tri";

    private readonly IRiversideApiClient _apiClient;

    public AdminGoiTrangTriDichVuController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? tab,
        string? goiTuKhoa,
        string? goiPhongCach,
        string? goiTrangThai,
        string? dichVuTuKhoa,
        string? dichVuLoai,
        string? dichVuTrangThai,
        CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var model = new QuanLyGoiTrangTriDichVuViewModel
        {
            ActiveTab = NormalizeTab(tab),
            GoiTuKhoa = goiTuKhoa,
            GoiPhongCach = goiPhongCach,
            GoiTrangThai = goiTrangThai,
            DichVuTuKhoa = dichVuTuKhoa,
            DichVuLoai = dichVuLoai,
            DichVuTrangThai = dichVuTrangThai
        };

        var goiResult = await _apiClient.GetDanhSachGoiTrangTriAsync(
            accessToken,
            goiTuKhoa,
            goiPhongCach,
            goiTrangThai,
            cancellationToken);
        var dichVuResult = await _apiClient.GetDanhSachDichVuAsync(
            accessToken,
            dichVuTuKhoa,
            dichVuLoai,
            dichVuTrangThai,
            cancellationToken);

        if (goiResult.Succeeded && goiResult.Value is not null)
        {
            model.DanhSachGoiTrangTri = goiResult.Value;
        }
        else
        {
            model.LoiGoiTrangTri = goiResult.Error ?? "Không thể tải danh sách gói trang trí.";
        }

        if (dichVuResult.Succeeded && dichVuResult.Value is not null)
        {
            model.DanhSachDichVu = dichVuResult.Value;
        }
        else
        {
            model.LoiDichVu = dichVuResult.Error ?? "Không thể tải danh sách dịch vụ.";
        }

        return View(model);
    }

    [HttpGet("goi-trang-tri/{id:int}")]
    public async Task<IActionResult> GetGoiTrangTri(int id, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.GetGoiTrangTriByIdAsync(id, accessToken, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Json(Success(result.Value))
            : Json(Failure(result.Error ?? "Không thể tải gói trang trí.", result.Errors));
    }

    [HttpPost("goi-trang-tri/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemGoiTrangTri(
        [FromForm] GoiTrangTriFormModel model,
        CancellationToken cancellationToken)
    {
        ValidateImage(model.HinhAnhFile);
        if (!ModelState.IsValid)
        {
            return Json(Failure("Dữ liệu gói trang trí chưa hợp lệ.", ModelStateErrors()));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.CreateGoiTrangTriAsync(model, accessToken, cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Thêm gói trang trí thành công."))
            : Json(Failure(result.Error ?? "Không thể thêm gói trang trí.", result.Errors));
    }

    [HttpPost("goi-trang-tri/sua/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaGoiTrangTri(
        int id,
        [FromForm] GoiTrangTriFormModel model,
        CancellationToken cancellationToken)
    {
        ValidateImage(model.HinhAnhFile);
        ValidateImageOperation(model.HinhAnhFile, model.XoaHinhAnh);
        if (id <= 0 || !ModelState.IsValid)
        {
            return Json(Failure("Dữ liệu gói trang trí chưa hợp lệ.", ModelStateErrors()));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.UpdateGoiTrangTriAsync(id, model, accessToken, cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Cập nhật gói trang trí thành công."))
            : Json(Failure(result.Error ?? "Không thể cập nhật gói trang trí.", result.Errors));
    }

    [HttpPost("goi-trang-tri/trang-thai/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThaiGoiTrangTri(
        int id,
        [FromForm] string trangThai,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || !IsValidStatus(trangThai))
        {
            return Json(Failure("Trạng thái gói chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.UpdateTrangThaiGoiTrangTriAsync(
            id,
            trangThai,
            accessToken,
            cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Cập nhật trạng thái gói thành công."))
            : Json(Failure(result.Error ?? "Không thể cập nhật trạng thái gói.", result.Errors));
    }

    [HttpGet("dich-vu/{id:int}")]
    public async Task<IActionResult> GetDichVu(int id, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.GetDichVuByIdAsync(id, accessToken, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Json(Success(result.Value))
            : Json(Failure(result.Error ?? "Không thể tải dịch vụ.", result.Errors));
    }

    [HttpPost("dich-vu/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemDichVu(
        [FromForm] DichVuFormModel model,
        CancellationToken cancellationToken)
    {
        ValidateImage(model.HinhAnhFile);
        if (!ModelState.IsValid)
        {
            return Json(Failure("Dữ liệu dịch vụ chưa hợp lệ.", ModelStateErrors()));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.CreateDichVuAsync(model, accessToken, cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Thêm dịch vụ thành công."))
            : Json(Failure(result.Error ?? "Không thể thêm dịch vụ.", result.Errors));
    }

    [HttpPost("dich-vu/sua/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaDichVu(
        int id,
        [FromForm] DichVuFormModel model,
        CancellationToken cancellationToken)
    {
        ValidateImage(model.HinhAnhFile);
        ValidateImageOperation(model.HinhAnhFile, model.XoaHinhAnh);
        if (id <= 0 || !ModelState.IsValid)
        {
            return Json(Failure("Dữ liệu dịch vụ chưa hợp lệ.", ModelStateErrors()));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.UpdateDichVuAsync(id, model, accessToken, cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Cập nhật dịch vụ thành công."))
            : Json(Failure(result.Error ?? "Không thể cập nhật dịch vụ.", result.Errors));
    }

    [HttpPost("dich-vu/trang-thai/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThaiDichVu(
        int id,
        [FromForm] string trangThai,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || !IsValidStatus(trangThai))
        {
            return Json(Failure("Trạng thái dịch vụ chỉ được là Áp dụng hoặc Ngừng áp dụng."));
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Json(Failure("Phiên đăng nhập đã hết hạn."));
        }

        var result = await _apiClient.UpdateTrangThaiDichVuAsync(
            id,
            trangThai,
            accessToken,
            cancellationToken);
        return result.Succeeded
            ? Json(Success(result.Value, "Cập nhật trạng thái dịch vụ thành công."))
            : Json(Failure(result.Error ?? "Không thể cập nhật trạng thái dịch vụ.", result.Errors));
    }

    private static string NormalizeTab(string? tab) =>
        tab is "goi-trang-tri" or "dich-vu" ? tab : DefaultTab;

    private static bool IsValidStatus(string? status) =>
        status is "Áp dụng" or "Ngừng áp dụng";

    private Dictionary<string, string[]> ModelStateErrors() =>
        ModelState
            .Where(pair => pair.Value?.Errors.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray());

    private void ValidateImage(IFormFile? file)
    {
        if (file is null)
        {
            return;
        }

        if (file.Length <= 0 || file.Length >= 5 * 1024 * 1024)
        {
            ModelState.AddModelError("HinhAnhFile", "Hình ảnh phải có dung lượng nhỏ hơn 5 MB.");
            return;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType.ToLowerInvariant();
        var matches = extension switch
        {
            ".jpg" or ".jpeg" => contentType == "image/jpeg",
            ".png" => contentType == "image/png",
            ".webp" => contentType == "image/webp",
            _ => false
        };

        if (!matches)
        {
            ModelState.AddModelError(
                "HinhAnhFile",
                "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP với MIME tương ứng.");
        }
    }

    private void ValidateImageOperation(IFormFile? file, bool removeImage)
    {
        if (file is not null && removeImage)
        {
            ModelState.AddModelError(
                "HinhAnhFile",
                "Chỉ chọn một trong hai thao tác tải ảnh mới hoặc gỡ ảnh hiện tại.");
        }
    }

    private static object Success(object? value, string? message = null) => new
    {
        success = true,
        message,
        value
    };

    private static object Failure(
        string message,
        Dictionary<string, string[]>? errors = null) => new
        {
            success = false,
            message,
            errors
        };
}

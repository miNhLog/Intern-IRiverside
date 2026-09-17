using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.AdminSanh;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;
using HeThongDatTiecCuoi_WEB.Models.AdminGoiTrangTriDichVu;
using Microsoft.AspNetCore.Http;

public sealed class RiversideApiClient : IRiversideApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public RiversideApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiCallResult<AuthResponseDto>> LoginAsync(
        LoginViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/login", new
        {
            identifier = model.DinhDanh,
            password = model.MatKhau,
            accountType = model.LoaiTaiKhoan,
            rememberMe = model.GhiNhoDangNhap
        }, null, cancellationToken);

    public Task<ApiCallResult<AuthResponseDto>> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/register", new
        {
            fullName = model.HoTen,
            phoneNumber = model.SoDienThoai,
            model.Email,
            password = model.MatKhau,
            confirmPassword = model.XacNhanMatKhau,
            agreeToTerms = model.DongYDieuKhoan
        }, null, cancellationToken);

    public Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<CurrentUserDto>(HttpMethod.Get, "api/auth/me", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<SanhTiecDto>>> GetDanhSachSanhAsync(
    string accessToken,
    CancellationToken cancellationToken) =>
    SendAsync<List<SanhTiecDto>>(
        HttpMethod.Get,
        "api/banquet-halls",
        null,
        accessToken,
        cancellationToken);


    public Task<ApiCallResult<SanhTiecDto>> GetSanhByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<SanhTiecDto>(
            HttpMethod.Get,
            $"api/banquet-halls/{id}",
            null,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<SanhTiecDto>> CreateSanhAsync(
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<SanhTiecDto>(
            HttpMethod.Post,
            "api/banquet-halls",
            model,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<SanhTiecDto>> UpdateSanhAsync(
        int id,
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<SanhTiecDto>(
            HttpMethod.Put,
            $"api/banquet-halls/{id}",
            model,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiSanhAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/banquet-halls/{id}/status",
            trangThai,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<ActionResponseDto>> DeleteSanhAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Delete,
            $"api/banquet-halls/{id}",
            null,
            accessToken,
            cancellationToken);
    public Task<ApiCallResult<LichSanhTuanDto>> GetLichSanhTheoTuanAsync(
    DateTime ngayBatDau,
    int? sanhTiecId,
    string accessToken,
    CancellationToken cancellationToken)
    {
        var url =
            $"api/hall-schedules/week?startDate={ngayBatDau:yyyy-MM-dd}";

        if (sanhTiecId.HasValue)
        {
            url += $"&hallId={sanhTiecId.Value}";
        }

        return SendAsync<LichSanhTuanDto>(
            HttpMethod.Get,
            url,
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiLichAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/hall-schedules/{id}/status",
            trangThai,
            accessToken,
            cancellationToken);

    // ======================================================
    // ADMIN - QUẢN LÝ TÀI KHOẢN
    // ======================================================

    public Task<ApiCallResult<List<TaiKhoanDto>>> GetDanhSachTaiKhoanAsync(
        string accessToken,
        string? tuKhoa,
        int? vaiTroId,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            query.Add(
                $"keyword={Uri.EscapeDataString(tuKhoa)}"
            );
        }

        if (vaiTroId.HasValue)
        {
            query.Add(
                $"roleId={vaiTroId.Value}"
            );
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query.Add(
                $"status={Uri.EscapeDataString(trangThai)}"
            );
        }

        var uri = "api/admin/accounts";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendAsync<List<TaiKhoanDto>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<List<VaiTroDto>>> GetDanhSachVaiTroAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        return SendAsync<List<VaiTroDto>>(
            HttpMethod.Get,
            "api/admin/accounts/roles",
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<ThaoTacTaiKhoanResponse>>
        TaoTaiKhoanNhanVienAsync(
            TaoTaiKhoanNhanVienRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<ThaoTacTaiKhoanResponse>(
            HttpMethod.Post,
            "api/admin/accounts/staff",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<ThaoTacTaiKhoanResponse>>
        CapNhatTaiKhoanNhanVienAsync(
            int id,
            CapNhatTaiKhoanNhanVienRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<ThaoTacTaiKhoanResponse>(
            HttpMethod.Put,
            $"api/admin/accounts/staff/{id}",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<ThaoTacTaiKhoanResponse>>
        CapNhatTrangThaiTaiKhoanAsync(
            int id,
            string trangThai,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<ThaoTacTaiKhoanResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/{id}/status",
            trangThai,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<ThaoTacTaiKhoanResponse>>
        DatLaiMatKhauAsync(
            int id,
            DatLaiMatKhauRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<ThaoTacTaiKhoanResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/{id}/reset-password",
            model,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<List<GoiTrangTriDto>>> GetDanhSachGoiTrangTriAsync(
        string accessToken,
        string? tuKhoa,
        string? phongCach,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQuery(query, "keyword", tuKhoa);
        AddQuery(query, "style", phongCach);
        AddQuery(query, "status", trangThai);

        return SendAsync<List<GoiTrangTriDto>>(
            HttpMethod.Get,
            BuildUri("api/decor-packages", query),
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<GoiTrangTriDto>> GetGoiTrangTriByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<GoiTrangTriDto>(HttpMethod.Get, $"api/decor-packages/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<GoiTrangTriDto>> CreateGoiTrangTriAsync(
        GoiTrangTriFormModel model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendMultipartAsync<GoiTrangTriDto>(
            HttpMethod.Post,
            "api/decor-packages",
            accessToken,
            new Dictionary<string, string?>
            {
                ["packageCode"] = model.MaGoi,
                ["packageName"] = model.TenGoi,
                ["style"] = model.PhongCach,
                ["description"] = model.MoTa,
                ["price"] = model.Gia.ToString(CultureInfo.InvariantCulture)
            },
            model.HinhAnhFile,
            false,
            cancellationToken);

    public Task<ApiCallResult<GoiTrangTriDto>> UpdateGoiTrangTriAsync(
        int id,
        GoiTrangTriFormModel model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendMultipartAsync<GoiTrangTriDto>(
            HttpMethod.Put,
            $"api/decor-packages/{id}",
            accessToken,
            new Dictionary<string, string?>
            {
                ["packageCode"] = model.MaGoi,
                ["packageName"] = model.TenGoi,
                ["style"] = model.PhongCach,
                ["description"] = model.MoTa,
                ["price"] = model.Gia.ToString(CultureInfo.InvariantCulture)
            },
            model.HinhAnhFile,
            model.XoaHinhAnh,
            cancellationToken);

    public Task<ApiCallResult<GoiTrangTriDto>> UpdateTrangThaiGoiTrangTriAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<GoiTrangTriDto>(
            HttpMethod.Patch,
            $"api/decor-packages/{id}/status",
            new { status = trangThai },
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<List<DichVuDto>>> GetDanhSachDichVuAsync(
        string accessToken,
        string? tuKhoa,
        string? loaiDichVu,
        string? trangThai,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQuery(query, "keyword", tuKhoa);
        AddQuery(query, "serviceType", loaiDichVu);
        AddQuery(query, "status", trangThai);

        return SendAsync<List<DichVuDto>>(
            HttpMethod.Get,
            BuildUri("api/services", query),
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<DichVuDto>> GetDichVuByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<DichVuDto>(HttpMethod.Get, $"api/services/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<DichVuDto>> CreateDichVuAsync(
        DichVuFormModel model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendMultipartAsync<DichVuDto>(
            HttpMethod.Post,
            "api/services",
            accessToken,
            new Dictionary<string, string?>
            {
                ["serviceCode"] = model.MaDichVu,
                ["serviceName"] = model.TenDichVu,
                ["serviceType"] = model.LoaiDichVu,
                ["description"] = model.MoTa,
                ["price"] = model.Gia.ToString(CultureInfo.InvariantCulture)
            },
            model.HinhAnhFile,
            false,
            cancellationToken);

    public Task<ApiCallResult<DichVuDto>> UpdateDichVuAsync(
        int id,
        DichVuFormModel model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendMultipartAsync<DichVuDto>(
            HttpMethod.Put,
            $"api/services/{id}",
            accessToken,
            new Dictionary<string, string?>
            {
                ["serviceCode"] = model.MaDichVu,
                ["serviceName"] = model.TenDichVu,
                ["serviceType"] = model.LoaiDichVu,
                ["description"] = model.MoTa,
                ["price"] = model.Gia.ToString(CultureInfo.InvariantCulture)
            },
            model.HinhAnhFile,
            model.XoaHinhAnh,
            cancellationToken);

    public Task<ApiCallResult<DichVuDto>> UpdateTrangThaiDichVuAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<DichVuDto>(
            HttpMethod.Patch,
            $"api/services/{id}/status",
            new { status = trangThai },
            accessToken,
            cancellationToken);

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static string BuildUri(string path, List<string> query) =>
        query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";

    private async Task<ApiCallResult<T>> SendMultipartAsync<T>(
        HttpMethod method,
        string uri,
        string accessToken,
        IReadOnlyDictionary<string, string?> fields,
        IFormFile? file,
        bool removeImage,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value ?? string.Empty), field.Key);
        }

        content.Add(new StringContent(removeImage.ToString().ToLowerInvariant()), "removeImage");

        if (file is not null)
        {
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType);
            content.Add(fileContent, "imageFile", Path.GetFileName(file.FileName));
        }

        return await SendAsync<T>(method, uri, content, accessToken, cancellationToken);
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is HttpContent httpContent)
            {
                request.Content = httpContent;
            }
            else if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return value is null
                    ? ApiCallResult<T>.Failure("API trả về dữ liệu rỗng.")
                    : ApiCallResult<T>.Success(value);
            }

            try
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, cancellationToken);
                return ApiCallResult<T>.Failure(
                    error?.Message ?? "Yêu cầu không thành công.",
                    error?.Errors);
            }
            catch (JsonException)
            {
                return ApiCallResult<T>.Failure("Yêu cầu không thành công.");
            }
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Failure("Backend API phản hồi quá lâu.");
        }
    }
    public async Task<ApiCallResult<ThaoTacTaiKhoanResponse>>
    CapNhatTrangThaiNhanVienAsync(
        int nguoiDungId,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken)
    {
        return await SendAsync<ThaoTacTaiKhoanResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/staff/{nguoiDungId}/status",
            trangThai,
            accessToken,
            cancellationToken
        );
    }
}

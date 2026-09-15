using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.AdminSanh;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;

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
            model.DinhDanh,
            model.MatKhau,
            model.LoaiTaiKhoan,
            model.GhiNhoDangNhap
        }, null, cancellationToken);

    public Task<ApiCallResult<AuthResponseDto>> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/register", new
        {
            model.HoTen,
            model.SoDienThoai,
            model.Email,
            model.MatKhau,
            model.XacNhanMatKhau,
            model.DongYDieuKhoan
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
        "api/SanhTiec",
        null,
        accessToken,
        cancellationToken);


    public Task<ApiCallResult<SanhTiecDto>> GetSanhByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<SanhTiecDto>(
            HttpMethod.Get,
            $"api/SanhTiec/{id}",
            null,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<SanhTiecDto>> CreateSanhAsync(
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<SanhTiecDto>(
            HttpMethod.Post,
            "api/SanhTiec",
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
            $"api/SanhTiec/{id}",
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
            $"api/SanhTiec/{id}/trang-thai",
            trangThai,
            accessToken,
            cancellationToken);


    public Task<ApiCallResult<ActionResponseDto>> DeleteSanhAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Delete,
            $"api/SanhTiec/{id}",
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
            $"api/LichSanh/tuan?ngayBatDau={ngayBatDau:yyyy-MM-dd}";

        if (sanhTiecId.HasValue)
        {
            url += $"&sanhTiecId={sanhTiecId.Value}";
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
            $"api/LichSanh/{id}/trang-thai",
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
                $"tuKhoa={Uri.EscapeDataString(tuKhoa)}"
            );
        }

        if (vaiTroId.HasValue)
        {
            query.Add(
                $"vaiTroId={vaiTroId.Value}"
            );
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query.Add(
                $"trangThai={Uri.EscapeDataString(trangThai)}"
            );
        }

        var uri = "api/AdminTaiKhoan";

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
            "api/AdminTaiKhoan/vai-tro",
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
            "api/AdminTaiKhoan/nhan-vien",
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
            $"api/AdminTaiKhoan/nhan-vien/{id}",
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
            $"api/AdminTaiKhoan/{id}/trang-thai",
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
            $"api/AdminTaiKhoan/{id}/dat-lai-mat-khau",
            model,
            accessToken,
            cancellationToken);
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
            if (body is not null)
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

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, cancellationToken);
            return ApiCallResult<T>.Failure(error?.Message ?? "Yêu cầu không thành công.");
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
            $"api/AdminTaiKhoan/nhan-vien/{nguoiDungId}/trang-thai",
            trangThai,
            accessToken,
            cancellationToken
        );
    }
}

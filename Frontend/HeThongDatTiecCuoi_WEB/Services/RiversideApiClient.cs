using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Common;
using HeThongDatTiecCuoi_WEB.Models.Halls;
using HeThongDatTiecCuoi_WEB.Models.Menus;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
using HeThongDatTiecCuoi_WEB.Models.AdminReport;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using Microsoft.AspNetCore.Mvc;

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
            model.Identifier,
            model.Password,
            model.RememberMe
        }, null, cancellationToken);

    public Task<ApiCallResult<AuthResponseDto>> LoginWithGoogleAsync(
        string accessToken,
        bool rememberMe,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/google-login", new
        {
            AccessToken = accessToken,
            RememberMe = rememberMe
        }, null, cancellationToken);

    public Task<ApiCallResult<MessageResponseDto>> ChangePasswordAsync(ChangePasswordViewModel model, string accessToken, CancellationToken cancellationToken) =>
        SendAsync<MessageResponseDto>(HttpMethod.Post, "api/auth/change-password", model, accessToken, cancellationToken);

    public Task<ApiCallResult<MessageResponseDto>> ForgotPasswordAsync(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<MessageResponseDto>(HttpMethod.Post, "api/auth/forgot-password", new
        {
            model.Email
        }, null, cancellationToken);

    public Task<ApiCallResult<MessageResponseDto>> ResetPasswordAsync(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<MessageResponseDto>(HttpMethod.Post, "api/auth/reset-password", new
        {
            model.Token,
            model.NewPassword,
            model.ConfirmPassword
        }, null, cancellationToken);

    public Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<CurrentUserDto>(HttpMethod.Get, "api/auth/me", null, accessToken, cancellationToken);

    public async Task<ApiCallResult<List<HallDto>>> GetFeaturedHallsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendAsync<List<HallDto>>(
                HttpMethod.Get, "api/halls/featured", null, null, cancellationToken);

            if (result.Value is not null && _httpClient.BaseAddress is not null)
            {
                foreach (var hall in result.Value)
                {
                    if (string.IsNullOrWhiteSpace(hall.ImageUrl))
                    {
                        continue;
                    }

                    if (Uri.TryCreate(hall.ImageUrl, UriKind.Absolute, out var absoluteImageUrl))
                    {
                        if (absoluteImageUrl.Scheme is not ("http" or "https"))
                        {
                            hall.ImageUrl = null;
                        }
                    }
                    else
                    {
                        hall.ImageUrl = Uri.TryCreate(
                            _httpClient.BaseAddress,
                            hall.ImageUrl.TrimStart('/'),
                            out var resolvedImageUrl)
                            ? resolvedImageUrl.ToString()
                            : null;
                    }
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<List<HallDto>>.Failure("API trả về dữ liệu sảnh không hợp lệ.");
        }
    }

    public async Task<ApiCallResult<PublicHallListResponse>> GetPublicHallsAsync(
        string? keyword,
        string? capacity,
        string? priceRange,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 20)}"
        };

        AddQueryParameter(query, "keyword", keyword);
        AddQueryParameter(query, "capacity", capacity);
        AddQueryParameter(query, "priceRange", priceRange);
        AddQueryParameter(query, "sort", sort);

        try
        {
            var result = await SendAsync<PublicHallListResponse>(
                HttpMethod.Get, $"api/halls/public?{string.Join('&', query)}", null, null, cancellationToken);

            if (result.Value is not null)
            {
                foreach (var hall in result.Value.Items)
                    hall.ImageUrl = ResolvePublicImageUrl(hall.ImageUrl);
            }

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<PublicHallListResponse>.Failure("API trả về dữ liệu sảnh không hợp lệ.");
        }
    }

    public async Task<ApiCallResult<PublicHallDetailDto>> GetPublicHallAsync(
        int hallId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendAsync<PublicHallDetailDto>(
                HttpMethod.Get, $"api/halls/public/{hallId}", null, null, cancellationToken);

            if (result.Value is not null)
                result.Value.ImageUrl = ResolvePublicImageUrl(result.Value.ImageUrl);

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<PublicHallDetailDto>.Failure(
                "API trả về dữ liệu sảnh không hợp lệ.");
        }
    }

    public async Task<ApiCallResult<PublicMenuListResponse>> GetPublicMenusAsync(
        string? keyword,
        string? priceRange,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 20)}"
        };

        AddQueryParameter(query, "keyword", keyword);
        AddQueryParameter(query, "priceRange", priceRange);
        AddQueryParameter(query, "sort", sort);

        try
        {
            return await SendAsync<PublicMenuListResponse>(
                HttpMethod.Get,
                $"api/menus/public?{string.Join('&', query)}",
                null,
                null,
                cancellationToken);
        }
        catch (JsonException)
        {
            return ApiCallResult<PublicMenuListResponse>.Failure(
                "API trả về dữ liệu thực đơn không hợp lệ.");
        }
    }

    public async Task<ApiCallResult<PublicMenuDetailDto>> GetPublicMenuDetailAsync(
        int menuId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendAsync<PublicMenuDetailDto>(
                HttpMethod.Get,
                $"api/menus/public/{menuId}",
                null,
                null,
                cancellationToken);

            if (result.Value is not null)
            {
                foreach (var dish in result.Value.Dishes)
                {
                    dish.ImageUrl = ResolvePublicImageUrl(dish.ImageUrl);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<PublicMenuDetailDto>.Failure(
                "API trả về dữ liệu chi tiết thực đơn không hợp lệ.");
        }
    }

    public Task<ApiCallResult<List<HallDto>>> GetHallsAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<List<HallDto>>(
            HttpMethod.Get,
            "api/halls",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> GetHallByIdAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Get,
            $"api/halls/{hallId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> CreateHallAsync(
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Post,
            "api/halls",
            model,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> UpdateHallAsync(
        int hallId,
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Put,
            $"api/halls/{hallId}",
            model,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<ActionResponseDto>> UpdateHallStatusAsync(
        int hallId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/halls/{hallId}/status",
            request,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ActionResponseDto>> DeleteHallAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Delete,
            $"api/halls/{hallId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<WeeklyHallScheduleDto>> GetWeeklyHallSchedulesAsync(
        DateTime startDate,
        int? hallId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url =
            $"api/hall-schedules/weekly?startDate={startDate:yyyy-MM-dd}";

        if (hallId.HasValue)
        {
            url += $"&hallId={hallId.Value}";
        }

        return SendAsync<WeeklyHallScheduleDto>(
            HttpMethod.Get,
            url,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ActionResponseDto>> UpdateHallScheduleStatusAsync(
        int hallScheduleId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/hall-schedules/{hallScheduleId}/status",
            request,
            accessToken,
            cancellationToken);
    }

    // ======================================================
    // ADMIN - QUẢN LÝ TÀI KHOẢN
    // ======================================================

    public Task<ApiCallResult<List<AccountDto>>> GetAccountsAsync(
        string accessToken,
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        }

        if (roleId.HasValue)
        {
            query.Add($"roleId={roleId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        var uri = "api/admin/accounts";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendAsync<List<AccountDto>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<List<RoleDto>>> GetRolesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        return SendAsync<List<RoleDto>>(
            HttpMethod.Get,
            "api/admin/accounts/roles",
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        CreateEmployeeAccountAsync(
            CreateEmployeeAccountRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Post,
            "api/admin/accounts/employees",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        UpdateEmployeeAccountAsync(
            int userId,
            UpdateEmployeeAccountRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Put,
            $"api/admin/accounts/employees/{userId}",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        UpdateAccountStatusAsync(
            int userId,
            string status,
            string accessToken,
            CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<AccountActionResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/{userId}/status",
            request,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        SendPasswordResetLinkAsync(
            int userId,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Post,
            $"api/admin/accounts/{userId}/password-reset",
            null,
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
            return ApiCallResult<T>.Failure(
                error?.Message ?? "Yêu cầu không thành công.",
                (int)response.StatusCode);
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

    private static void AddQueryParameter(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }

    private string? ResolvePublicImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || _httpClient.BaseAddress is null)
            return null;

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteImageUrl))
            return absoluteImageUrl.Scheme is "http" or "https" ? absoluteImageUrl.ToString() : null;

        return Uri.TryCreate(_httpClient.BaseAddress, imageUrl.TrimStart('/'), out var resolvedImageUrl)
            ? resolvedImageUrl.ToString()
            : null;
    }
    public async Task<ApiCallResult<AccountActionResponse>>
    UpdateEmployeeStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return await SendAsync<AccountActionResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/employees/{userId}/status",
            request,
            accessToken,
            cancellationToken
        );
    }

    // =========================================================
    // THỰC ĐƠN
    // =========================================================

    public Task<ApiCallResult<List<MenuViewModel>>> GetMenusAsync(
        string? keyword,
        string? status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add(
                $"keyword={Uri.EscapeDataString(keyword.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add(
                $"status={Uri.EscapeDataString(status.Trim())}");
        }

        var uri = "api/menus";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendJsonAsync<List<MenuViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<MenuViewModel>> CreateMenuAsync(
        CreateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<MenuViewModel>(
            HttpMethod.Post,
            "api/menus",
            new
            {
                model.MenuName,
                model.Description,
                model.PricePerTable
            },
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<MenuViewModel>> UpdateMenuAsync(
        int menuId,
        UpdateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<MenuViewModel>(
            HttpMethod.Put,
            $"api/menus/{menuId}",
            new
            {
                model.MenuName,
                model.Description,
                model.PricePerTable
            },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> UpdateMenuStatusAsync(
        int menuId,
        string status,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Patch,
            $"api/menus/{menuId}/status",
            new { Status = status },
            accessToken,
            cancellationToken);


    // =========================================================
    // MÓN ĂN
    // =========================================================

    public Task<ApiCallResult<List<DishViewModel>>> GetDishesAsync(
        string? keyword,
        string? category,
        string? status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add(
                $"keyword={Uri.EscapeDataString(keyword.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add(
                $"category={Uri.EscapeDataString(category.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add(
                $"status={Uri.EscapeDataString(status.Trim())}");
        }

        var uri = "api/dishes";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendJsonAsync<List<DishViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<DishViewModel>> CreateDishAsync(
        CreateDishForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<DishViewModel>(
            HttpMethod.Post,
            "api/dishes",
            new
            {
                model.DishName,
                model.Category
            },
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<DishViewModel>> UpdateDishAsync(
        int dishId,
        UpdateDishForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<DishViewModel>(
            HttpMethod.Put,
            $"api/dishes/{dishId}",
            new
            {
                model.DishName,
                model.Category
            },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> UpdateDishStatusAsync(
        int dishId,
        string status,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Patch,
            $"api/dishes/{dishId}/status",
            new { Status = status },
            accessToken,
            cancellationToken);

    public async Task<ApiActionResult> UploadDishImageAsync(
        int dishId,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/dishes/{dishId}/image");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var multipart = new MultipartFormDataContent();

            using var fileContent = new StreamContent(fileStream);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                fileContent.Headers.ContentType =
                    MediaTypeHeaderValue.Parse(contentType);
            }

            multipart.Add(fileContent, "file", fileName);

            request.Content = multipart;

            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiActionResult.Success()
                : ApiActionResult.Failure(
                    await ReadErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiActionResult.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiActionResult.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    public Task<ApiActionResult> DeleteDishImageAsync(
        int dishId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Delete,
            $"api/dishes/{dishId}/image",
            null,
            accessToken,
            cancellationToken);


    // =========================================================
    // CHI TIẾT THỰC ĐƠN
    // =========================================================

    public Task<ApiCallResult<List<MenuDishViewModel>>> GetMenuDishesAsync(
        int menuId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<List<MenuDishViewModel>>(
            HttpMethod.Get,
            $"api/menus/{menuId}/dishes",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<MenuDishViewModel>> AddDishToMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<MenuDishViewModel>(
            HttpMethod.Post,
            $"api/menus/{menuId}/dishes",
            new { DishId = dishId },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> RemoveDishFromMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Delete,
            $"api/menus/{menuId}/dishes/{dishId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> ReorderMenuDishesAsync(
        int menuId,
        List<int> dishIds,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Put,
            $"api/menus/{menuId}/dishes/order",
            new { DishIds = dishIds },
            accessToken,
            cancellationToken);


    // =========================================================
    // HTTP HELPERS
    // =========================================================

    private async Task<ApiCallResult<T>> SendJsonAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(method, uri);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        accessToken);
            }

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var value =
                    await response.Content.ReadFromJsonAsync<T>(
                        JsonOptions,
                        cancellationToken);

                return value is null
                    ? ApiCallResult<T>.Failure(
                        "API trả về dữ liệu rỗng.")
                    : ApiCallResult<T>.Success(value);
            }

            return ApiCallResult<T>.Failure(
                await ReadErrorAsync(
                    response,
                    cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    private async Task<ApiActionResult> SendActionJsonAsync(
        HttpMethod method,
        string uri,
        object? body,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(method, uri);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiActionResult.Success()
                : ApiActionResult.Failure(
                    await ReadErrorAsync(
                        response,
                        cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiActionResult.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiActionResult.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error =
                await response.Content.ReadFromJsonAsync<ApiErrorDto>(
                    JsonOptions,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch
        {
            // Nếu Backend trả về body không phải JSON thì đọc text bên dưới.
        }

        var text =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return string.IsNullOrWhiteSpace(text)
            ? $"Yêu cầu không thành công ({(int)response.StatusCode})."
            : text;
    }
    public async Task<ApiCallResult<RevenueBiViewModel>> GetRevenueBiReportAsync(
    int year,
    int quarter,
    string? accessToken,
    CancellationToken cancellationToken = default)
    {
        var uri = $"api/admin/reports/revenue-bi?year={year}&quarter={quarter}";
        return await SendJsonAsync<RevenueBiViewModel>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public async Task<ApiCallResult<HallScheduleMatrixDto>> GetHallScheduleMatrixAsync(
    DateTime? startDate,
    string? accessToken,
    CancellationToken cancellationToken = default)
    {
        var uri = startDate.HasValue
            ? $"api/admin/reports/hall-matrix?startDate={startDate.Value:yyyy-MM-dd}"
            : "api/admin/reports/hall-matrix";

        return await SendJsonAsync<HallScheduleMatrixDto>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    // Quản lý đặt tiệc
    public async Task<ApiCallResult<BookingListResponseViewModel>> GetBookingsAsync(
    BookingFilterRequestViewModel filter,
    string? accessToken,
    CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
    {
        $"pageIndex={filter.PageIndex}",
        $"pageSize={filter.PageSize}"
    };

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            queryParams.Add($"keyword={Uri.EscapeDataString(filter.Keyword.Trim())}");

        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "ALL")
            queryParams.Add($"status={Uri.EscapeDataString(filter.Status)}");

        if (filter.HallId.HasValue && filter.HallId.Value > 0)
            queryParams.Add($"hallId={filter.HallId.Value}");

        if (filter.FromDate.HasValue)
            queryParams.Add($"fromDate={filter.FromDate.Value:yyyy-MM-dd}");

        if (filter.ToDate.HasValue)
            queryParams.Add($"toDate={filter.ToDate.Value:yyyy-MM-dd}");

        var uri = $"api/admin/bookings?{string.Join("&", queryParams)}";

        return await SendJsonAsync<BookingListResponseViewModel>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public async Task<ApiCallResult<BookingDetailViewModel>> GetBookingDetailAsync(
        int id,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/admin/bookings/{id}";
        return await SendJsonAsync<BookingDetailViewModel>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public async Task<ApiCallResult<dynamic>> CreateBookingAsync(
        CreateBookingRequestViewModel request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var uri = "api/admin/bookings";
        return await SendJsonAsync<dynamic>(
            HttpMethod.Post,
            uri,
            request,
            accessToken,
            cancellationToken);
    }

    public async Task<ApiCallResult<dynamic>> UpdateBookingStatusAsync(
        int id,
        UpdateBookingStatusRequestViewModel request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/admin/bookings/{id}/status";
        return await SendJsonAsync<dynamic>(
            HttpMethod.Put,
            uri,
            request,
            accessToken,
            cancellationToken);
    }

    // Hệ thống khuyến nghị
    public async Task<ApiCallResult<RecommendationResponseViewModel>> GetTop3RecommendationsAsync(
    RecommendationRequestViewModel request,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/suggest-top3", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<RecommendationResponseViewModel>.Failure($"Lỗi máy chủ ({response.StatusCode}): {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<RecommendationResponseViewModel>(cancellationToken: cancellationToken);
            return ApiCallResult<RecommendationResponseViewModel>.Success(result ?? new RecommendationResponseViewModel());
        }
        catch (Exception ex)
        {
            return ApiCallResult<RecommendationResponseViewModel>.Failure($"Không thể kết nối thuật toán khuyến nghị: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<dynamic>> GetHallAvailabilityRealtimeAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/recommendations/hall-availability?date={date:yyyy-MM-dd}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<dynamic>.Failure("Không thể lấy dữ liệu lịch sảnh từ máy chủ.");
            }
            var data = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: cancellationToken);
            return ApiCallResult<dynamic>.Success(data);
        }
        catch (Exception ex)
        {
            return ApiCallResult<dynamic>.Failure($"Lỗi: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<HallAvailabilityResponseViewModel>> GetHallAvailabilityRealtimeAsync(string dateStr, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/recommendations/hall-availability?date={dateStr}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<HallAvailabilityResponseViewModel>.Failure($"Lỗi máy chủ ({response.StatusCode}): {err}");
            }

            var data = await response.Content.ReadFromJsonAsync<HallAvailabilityResponseViewModel>(cancellationToken: cancellationToken);
            return ApiCallResult<HallAvailabilityResponseViewModel>.Success(data ?? new HallAvailabilityResponseViewModel());
        }
        catch (Exception ex)
        {
            return ApiCallResult<HallAvailabilityResponseViewModel>.Failure($"Lỗi kết nối API: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<string>> RegisterPublicBookingAsync(
     PublicBookingRequestViewModel request,
     CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/register-booking", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(err);
                    if (doc.RootElement.TryGetProperty("message", out var msgElem))
                    {
                        return ApiCallResult<string>.Failure(msgElem.GetString() ?? err);
                    }
                }
                catch { }
                return ApiCallResult<string>.Failure(err);
            }

            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: cancellationToken);
            string code = result.TryGetProperty("bookingCode", out var codeElem) ? codeElem.GetString() ?? "DT-ONLINE" : "DT-ONLINE";
            return ApiCallResult<string>.Success(code);
        }
        catch (Exception ex)
        {
            return ApiCallResult<string>.Failure($"Lỗi kết nối API: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>> GetMyBookingsAsync(string? phone, int? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(phone))
            {
                queryParams.Add($"phone={Uri.EscapeDataString(phone.Trim())}");
            }
            if (userId.HasValue && userId.Value > 0)
            {
                queryParams.Add($"userId={userId.Value}");
            }

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/my-bookings{queryString}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>.Failure("Không thể lấy danh sách đơn tiệc cưới.");
            }

            var doc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: cancellationToken);
            if (doc.TryGetProperty("data", out var dataElem))
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = System.Text.Json.JsonSerializer.Deserialize<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>(dataElem.GetRawText(), options);
                return ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>.Success(list ?? new List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>());
            }

            return ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>.Success(new List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>());
        }
        catch (Exception ex)
        {
            return ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>.Failure($"Lỗi: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<bool>> CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsync($"api/my-bookings/{bookingId}/cancel", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<bool>.Failure(err);
            }
            return ApiCallResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ApiCallResult<bool>.Failure($"Lỗi: {ex.Message}");
        }
    }
}

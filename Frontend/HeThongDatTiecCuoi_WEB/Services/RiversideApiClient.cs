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
using HeThongDatTiecCuoi_WEB.Models.RoleChangeRequest;
using HeThongDatTiecCuoi_WEB.Models.Notification;
using HeThongDatTiecCuoi_WEB.Models.Home;
using HeThongDatTiecCuoi_WEB.Models.ManagerHr;
using HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment;
using HeThongDatTiecCuoi_WEB.Models.ManagerEventCoordination;
using Microsoft.AspNetCore.Mvc;

public sealed class RiversideApiClient : IRiversideApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _httpClient;

    public RiversideApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiCallResult<EventCoordinationListResponseViewModel>> GetManagerEventCoordinationAsync(
        string accessToken, string? search, string? bookingStatus, string? coordinationStatus,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={Math.Clamp(pageSize, 1, 50)}" };
        AddQueryParameter(query, "search", search);
        AddQueryParameter(query, "bookingStatus", bookingStatus);
        AddQueryParameter(query, "coordinationStatus", coordinationStatus);
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        return SendAsync<EventCoordinationListResponseViewModel>(HttpMethod.Get,
            $"api/manager/event-coordination?{string.Join('&', query)}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<EventCoordinationDetailViewModel>> GetManagerEventCoordinationDetailAsync(
        string accessToken, int bookingId, CancellationToken cancellationToken) =>
        SendAsync<EventCoordinationDetailViewModel>(HttpMethod.Get, $"api/manager/event-coordination/{bookingId}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<EventCoordinationCalendarViewModel>> GetManagerEventCoordinationCalendarAsync(
        string accessToken, DateTime fromDate, DateTime toDate, int? hallId, int? coordinatorEmployeeId,
        bool conflictsOnly, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"fromDate={fromDate:yyyy-MM-dd}", $"toDate={toDate:yyyy-MM-dd}", $"conflictsOnly={conflictsOnly.ToString().ToLowerInvariant()}" };
        if (hallId.HasValue) query.Add($"hallId={hallId.Value}");
        if (coordinatorEmployeeId.HasValue) query.Add($"coordinatorEmployeeId={coordinatorEmployeeId.Value}");
        return SendAsync<EventCoordinationCalendarViewModel>(HttpMethod.Get,
            $"api/manager/event-coordination/calendar?{string.Join('&', query)}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<ManagerContractListResponseViewModel>> GetManagerContractsAsync(
        string accessToken, string? keyword, string? status, DateTime? fromDate, DateTime? toDate,
        int page, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}" };
        AddQueryParameter(query, "keyword", keyword);
        AddQueryParameter(query, "status", status);
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        return SendAsync<ManagerContractListResponseViewModel>(HttpMethod.Get,
            $"api/manager/contracts?{string.Join('&', query)}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<ManagerContractDetailViewModel>> GetManagerContractAsync(
        string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<ManagerContractDetailViewModel>(HttpMethod.Get, $"api/manager/contracts/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<EligibleBookingViewModel>>> GetEligibleContractBookingsAsync(
        string accessToken, string? keyword, CancellationToken cancellationToken)
    {
        var uri = "api/manager/contracts/eligible-bookings";
        if (!string.IsNullOrWhiteSpace(keyword)) uri += $"?keyword={Uri.EscapeDataString(keyword.Trim())}";
        return SendAsync<List<EligibleBookingViewModel>>(HttpMethod.Get, uri, null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<ContractActionResponseViewModel>> CreateManagerContractAsync(
        string accessToken, CreateContractViewModel model, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Post, "api/manager/contracts", model, accessToken, cancellationToken);

    public Task<ApiCallResult<ContractActionResponseViewModel>> UpdateManagerContractAsync(
        string accessToken, int id, UpdateDraftContractViewModel model, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Put, $"api/manager/contracts/{id}", model, accessToken, cancellationToken);

    public Task<ApiCallResult<ContractActionResponseViewModel>> ActivateManagerContractAsync(
        string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Post, $"api/manager/contracts/{id}/activate", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ContractActionResponseViewModel>> CompleteManagerContractAsync(
        string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Post, $"api/manager/contracts/{id}/complete", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ContractActionResponseViewModel>> CancelManagerContractAsync(
        string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Post, $"api/manager/contracts/{id}/cancel", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ManagerPaymentListResponseViewModel>> GetManagerPaymentsAsync(
        string accessToken, string? keyword, string? type, string? status, DateTime? fromDate, DateTime? toDate,
        int page, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}" };
        AddQueryParameter(query, "keyword", keyword);
        AddQueryParameter(query, "type", type);
        AddQueryParameter(query, "status", status);
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        return SendAsync<ManagerPaymentListResponseViewModel>(HttpMethod.Get,
            $"api/manager/payments?{string.Join('&', query)}", null, accessToken, cancellationToken);
    }

    public async Task<ApiCallResult<PdfDownloadViewModel>> DownloadManagerContractPdfAsync(
        string accessToken, int id, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/manager/contracts/{id}/pdf");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<PdfDownloadViewModel>.Failure(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"HopDong_{id}.pdf";
            return ApiCallResult<PdfDownloadViewModel>.Success(new PdfDownloadViewModel(
                bytes, fileName, response.Content.Headers.ContentType?.MediaType ?? "application/pdf"));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<PdfDownloadViewModel>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<PdfDownloadViewModel>.Failure("Backend API phản hồi quá lâu.");
        }
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

    public Task<ApiCallResult<FirstPasswordOtpResponseDto>> SendFirstPasswordOtpAsync(string accessToken, CancellationToken cancellationToken) =>
        SendAsync<FirstPasswordOtpResponseDto>(HttpMethod.Post, "api/auth/first-password-otp", new { }, accessToken, cancellationToken);

    public Task<ApiCallResult<PasswordResetOtpResponseDto>> SendPasswordResetOtpAsync(
        SendPasswordResetOtpViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<PasswordResetOtpResponseDto>(HttpMethod.Post, "api/auth/forgot-password", new
        {
            model.Email
        }, null, cancellationToken);

    public Task<ApiCallResult<PasswordResetOtpVerificationDto>> VerifyPasswordResetOtpAsync(
        VerifyPasswordResetOtpViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<PasswordResetOtpVerificationDto>(HttpMethod.Post, "api/auth/verify-reset-otp", new
        {
            model.Email,
            model.OtpCode
        }, null, cancellationToken);

    public Task<ApiCallResult<MessageResponseDto>> CompletePasswordResetAsync(
        CompletePasswordResetViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<MessageResponseDto>(HttpMethod.Post, "api/auth/reset-password", new
        {
            Token = model.ResetToken,
            model.NewPassword,
            model.ConfirmPassword
        }, null, cancellationToken);

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
            {
                result.Value.ImageUrl = ResolvePublicImageUrl(result.Value.ImageUrl);
                result.Value.ImageUrls = result.Value.ImageUrls
                    .Select(ResolvePublicImageUrl)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => url!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

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

    public async Task<ApiCallResult<List<PublicDishDto>>> GetPublicDishesAsync(
        string? keyword,
        string? category,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new List<string>();
            AddQueryParameter(query, "keyword", keyword);
            AddQueryParameter(query, "category", category);
            var uri = "api/dishes/public" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
            var result = await SendAsync<List<PublicDishDto>>(
                HttpMethod.Get,
                uri,
                null,
                null,
                cancellationToken);

            if (result.Value is not null && _httpClient.BaseAddress is not null)
            {
                foreach (var dish in result.Value)
                    dish.ImageUrl = ResolvePublicImageUrl(dish.ImageUrl);
            }

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<List<PublicDishDto>>.Failure(
                "API trả về dữ liệu món ăn không hợp lệ.");
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
        bool? mustChangePassword,
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

        if (mustChangePassword.HasValue)
        {
            query.Add($"mustChangePassword={mustChangePassword.Value.ToString().ToLowerInvariant()}");
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

    public Task<ApiCallResult<ManagerHrPageViewModel>> GetManagerHrAsync(string accessToken,string? search,string? role,string? status,string? assignmentStatus,int page,int pageSize,CancellationToken cancellationToken)
    { var q=new List<string>{$"page={Math.Max(1,page)}",$"pageSize={Math.Clamp(pageSize,1,50)}"}; AddQueryParameter(q,"search",search);AddQueryParameter(q,"role",role);AddQueryParameter(q,"status",status);AddQueryParameter(q,"assignmentStatus",assignmentStatus);return SendAsync<ManagerHrPageViewModel>(HttpMethod.Get,"api/manager/hr?"+string.Join('&',q),null,accessToken,cancellationToken); }
    public Task<ApiCallResult<ManagerHrActionViewModel>> AssignManagerHallsAsync(int employeeId,AssignHallsViewModel model,string accessToken,CancellationToken cancellationToken)=>SendAsync<ManagerHrActionViewModel>(HttpMethod.Post,$"api/manager/hr/employees/{employeeId}/hall-assignments",model,accessToken,cancellationToken);
    public Task<ApiCallResult<ManagerHrActionViewModel>> EndManagerHallAssignmentAsync(int assignmentId,string accessToken,CancellationToken cancellationToken)=>SendAsync<ManagerHrActionViewModel>(HttpMethod.Post,$"api/manager/hr/hall-assignments/{assignmentId}/end",null,accessToken,cancellationToken);

    public Task<ApiCallResult<ManagerDashboardViewModel>> GetManagerDashboardAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ManagerDashboardViewModel>(HttpMethod.Get, "api/manager/dashboard", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<NotificationViewModel>>> GetRecentNotificationsAsync(string accessToken, int limit, CancellationToken cancellationToken) =>
        SendAsync<List<NotificationViewModel>>(HttpMethod.Get, $"api/notifications/recent?limit={Math.Clamp(limit, 1, 5)}", null, accessToken, cancellationToken);
    public Task<ApiCallResult<NotificationCountViewModel>> GetUnreadNotificationCountAsync(string accessToken, CancellationToken cancellationToken) =>
        SendAsync<NotificationCountViewModel>(HttpMethod.Get, "api/notifications/unread-count", null, accessToken, cancellationToken);
    public Task<ApiCallResult<NotificationPageViewModel>> GetNotificationsAsync(string accessToken, int page, int pageSize, string? readStatus, CancellationToken cancellationToken)
    {
        var uri = $"api/notifications?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}";
        if (!string.IsNullOrWhiteSpace(readStatus)) uri += $"&readStatus={Uri.EscapeDataString(readStatus)}";
        return SendAsync<NotificationPageViewModel>(HttpMethod.Get, uri, null, accessToken, cancellationToken);
    }
    public Task<ApiCallResult<NotificationUpdateViewModel>> MarkNotificationReadAsync(string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<NotificationUpdateViewModel>(HttpMethod.Post, $"api/notifications/{id}/read", null, accessToken, cancellationToken);
    public Task<ApiCallResult<NotificationUpdateViewModel>> MarkAllNotificationsReadAsync(string accessToken, CancellationToken cancellationToken) =>
        SendAsync<NotificationUpdateViewModel>(HttpMethod.Post, "api/notifications/read-all", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<RoleChangeCandidateViewModel>>> GetRoleChangeCandidatesAsync(
        string accessToken, CancellationToken cancellationToken) =>
        SendAsync<List<RoleChangeCandidateViewModel>>(HttpMethod.Get,
            "api/manager/role-change-requests/candidates", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<RoleChangeRequestViewModel>>> GetManagerRoleChangeRequestsAsync(
        string accessToken, CancellationToken cancellationToken) =>
        SendAsync<List<RoleChangeRequestViewModel>>(HttpMethod.Get,
            "api/manager/role-change-requests", null, accessToken, cancellationToken);

    public Task<ApiCallResult<RoleChangeRequestViewModel>> CreateRoleChangeRequestAsync(
        CreateRoleChangeRequestViewModel model, string accessToken, CancellationToken cancellationToken) =>
        SendAsync<RoleChangeRequestViewModel>(HttpMethod.Post,
            "api/manager/role-change-requests", model, accessToken, cancellationToken);

    public Task<ApiCallResult<List<AdminRoleChangeRequestViewModel>>> GetAdminRoleChangeRequestsAsync(
        string? status, string? keyword, string accessToken, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        AddQueryParameter(query, "status", status);
        AddQueryParameter(query, "keyword", keyword);
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return SendAsync<List<AdminRoleChangeRequestViewModel>>(HttpMethod.Get,
            $"api/admin/role-change-requests{suffix}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<ActionResponseDto>> ApproveRoleChangeRequestAsync(
        int id, string accessToken, CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(HttpMethod.Post, $"api/admin/role-change-requests/{id}/approve", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ActionResponseDto>> RejectRoleChangeRequestAsync(
        int id, RejectRoleChangeRequestViewModel model, string accessToken, CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(HttpMethod.Post, $"api/admin/role-change-requests/{id}/reject", model, accessToken, cancellationToken);

    public Task<ApiCallResult<SystemDashboardViewModel>> GetSystemDashboardAsync(
        string accessToken,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        var uri = "api/admin/accounts/dashboard" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        return SendAsync<SystemDashboardViewModel>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<AuditLogPageViewModel>> GetAuditLogsAsync(
        string accessToken,
        int page,
        int pageSize,
        string? action,
        DateTime? fromDate,
        DateTime? toDate,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}"
        };
        if (!string.IsNullOrWhiteSpace(action)) query.Add($"action={Uri.EscapeDataString(action)}");
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");

        return SendAsync<AuditLogPageViewModel>(
            HttpMethod.Get,
            "api/admin/audit-logs?" + string.Join("&", query),
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
        UpdateAdministratorAccountAsync(
            int userId,
            UpdateAdministratorAccountRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Put,
            $"api/admin/accounts/administrator/{userId}",
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

    public Task<ApiCallResult<Models.ManagerMenu.ManagerMenuPageViewModel>> GetManagerMenusAsync(
        string? keyword, string? status, int page, int pageSize,
        string accessToken, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        return SendJsonAsync<Models.ManagerMenu.ManagerMenuPageViewModel>(
            HttpMethod.Get, "api/menus/management?" + string.Join("&", query), null, accessToken, cancellationToken);
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

    public Task<ApiCallResult<Models.ManagerDish.ManagerDishPageViewModel>> GetManagerDishesAsync(
        string? keyword, string? category, string? status, int page, int pageSize,
        string accessToken, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        return SendJsonAsync<Models.ManagerDish.ManagerDishPageViewModel>(HttpMethod.Get,
            "api/dishes/management?" + string.Join("&", query), null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<Models.ManagerDish.ManagerDishDetailViewModel>> GetManagerDishDetailAsync(
        int dishId, string accessToken, CancellationToken cancellationToken) =>
        SendJsonAsync<Models.ManagerDish.ManagerDishDetailViewModel>(HttpMethod.Get,
            $"api/dishes/{dishId}/management-detail", null, accessToken, cancellationToken);

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
                model.Category,
                model.Price
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
                model.Category,
                model.Price
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

    public Task<ApiActionResult> BatchAddMenuDishesAsync(
        int menuId, List<int> dishIds, string accessToken, CancellationToken cancellationToken) =>
        SendActionJsonAsync(HttpMethod.Post, $"api/menus/{menuId}/dishes/batch",
            new { DishIds = dishIds }, accessToken, cancellationToken);

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
            queryParams.Add($"statusCode={Uri.EscapeDataString(filter.Status)}");

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

    public async Task<ApiCallResult<BookingCreateOptionsViewModel>> GetBookingCreateOptionsAsync(
        DateTime eventDate, string? accessToken, CancellationToken cancellationToken = default)
    {
        return await SendJsonAsync<BookingCreateOptionsViewModel>(HttpMethod.Get,
            $"api/admin/bookings/create-options?eventDate={eventDate:yyyy-MM-dd}", null, accessToken, cancellationToken);
    }

    public async Task<ApiCallResult<CreateBookingResponseViewModel>> CreateBookingAsync(
        CreateBookingRequestViewModel request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var uri = "api/admin/bookings";
        return await SendJsonAsync<CreateBookingResponseViewModel>(
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
            // Map ViewModel (EventDate/Shift/DesiredStyle...) sang DTO Backend
            // (DesiredDate/DesiredShift/DesiredStyle...) — khớp 100% tên field,
            // tránh Backend nhận DesiredShift="" -> 400 hoặc DesiredDate=0001 -> 0 kết quả.
            var guestCount = request.GuestCount > 0
                ? request.GuestCount
                : Math.Max(1, (request.OfficialTableCount + request.SpareTableCount) * 10);
            // Quy đổi chuẩn 10 khách/bàn khi GuestCount thay đổi từ form mới.
            var officialTables = request.OfficialTableCount > 0
                ? request.OfficialTableCount
                : (int)Math.Ceiling(guestCount / 10m);
            var spareTables = Math.Max(0, request.SpareTableCount);
            var tableCount = request.TableCount > 0
                ? request.TableCount
                : officialTables + spareTables;
            var serviceNeeds = request.SelectedServiceCodes is { Count: > 0 }
                ? string.Join(",", request.SelectedServiceCodes)
                : null;
            var backendPayload = new
            {
                expectedBudget = request.ExpectedBudget,
                guestCount,
                officialTableCount = officialTables,
                spareTableCount = spareTables,
                tableCount,
                desiredDate = request.EventDate,
                desiredShift = NormalizeHallShift(request.Shift),
                desiredStyle = string.IsNullOrWhiteSpace(request.DesiredStyle)
                    ? "Hiện đại / lãng mạn"
                    : request.DesiredStyle.Trim(),
                serviceNeeds
            };
            // Chuỗi endpoint Backend chính xác: api/recommendations/suggest-top3 (heuristic C# nội bộ).
            // (Endpoint AI riêng là api/recommendations/suggest-top3-ai -> gọi FastAPI http://127.0.0.1:8000/api/v1/ai/wsm/rank)
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/suggest-top3", backendPayload, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // Bóc {message} tiếng Việt từ Backend thay vì dump raw JSON/HTML cho user.
                return ApiCallResult<RecommendationResponseViewModel>.Failure(ExtractApiMessage(errorContent, response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<RecommendationResponseViewModel>(JsonOptions, cancellationToken);
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
        // Backend trả OBJECT {queryDate, halls:[...]} (HallAvailabilityResponseDto).
        // Bản cũ từng trả ARRAY trần nên phải bóc đa hình: array | {halls} | {success,data} | {data:{halls}}.
        // Case-insensitive + [JsonPropertyName] đầy đủ; null-safe, catch trả ViewModel rỗng thay vì throw.
        var empty = new HallAvailabilityResponseViewModel { QueryDate = dateStr ?? string.Empty };
        try
        {
            var response = await _httpClient.GetAsync($"api/recommendations/hall-availability?date={dateStr}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<HallAvailabilityResponseViewModel>.Failure($"Lỗi máy chủ ({response.StatusCode}): {err}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return ApiCallResult<HallAvailabilityResponseViewModel>.Success(empty);

            var parsed = ParseHallAvailability(json, dateStr ?? string.Empty);
            return ApiCallResult<HallAvailabilityResponseViewModel>.Success(parsed ?? empty);
        }
        catch (Exception)
        {
            // Không để JsonException "could not be converted ... Path: $" văng ra modal;
            // trả ViewModel rỗng để UI render ma trận trống thay vì throw.
            return ApiCallResult<HallAvailabilityResponseViewModel>.Success(empty);
        }
    }

    private static HallAvailabilityResponseViewModel ParseHallAvailability(string json, string queryDate)
    {
        var result = new HallAvailabilityResponseViewModel { QueryDate = queryDate };
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 1. Array trần (contract cũ): [...] -> gán vào Halls.
        if (root.ValueKind == JsonValueKind.Array)
        {
            var list = JsonSerializer.Deserialize<List<HallSlotItemViewModel>>(json, JsonOptions);
            result.Halls = list ?? new List<HallSlotItemViewModel>();
            return result;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return result;

        // 2. Bọc {success, data}: bóc data trước (data có thể là array hoặc object {halls}).
        if (root.TryGetProperty("data", out var dataElem))
        {
            if (dataElem.ValueKind == JsonValueKind.Array)
            {
                var list = dataElem.Deserialize<List<HallSlotItemViewModel>>(JsonOptions);
                result.Halls = list ?? new List<HallSlotItemViewModel>();
                if (root.TryGetProperty("queryDate", out var qd) && qd.ValueKind == JsonValueKind.String)
                    result.QueryDate = qd.GetString() ?? queryDate;
                else if (dataElem.ValueKind == JsonValueKind.Object) { }
                return result;
            }
            if (dataElem.ValueKind == JsonValueKind.Object)
            {
                var nested = ParseHallAvailabilityObject(dataElem, queryDate);
                if (nested is not null)
                    return nested;
            }
            // data null -> trả rỗng.
            return result;
        }

        // 3. Object chuẩn {queryDate, halls:[...]} (kể cả PascalCase nhờ case-insensitive).
        var direct = ParseHallAvailabilityObject(root, queryDate);
        return direct ?? result;
    }

    private static HallAvailabilityResponseViewModel? ParseHallAvailabilityObject(JsonElement obj, string queryDate)
    {
        List<HallSlotItemViewModel>? halls = null;
        if (obj.TryGetProperty("halls", out var hallsElem) && hallsElem.ValueKind == JsonValueKind.Array)
            halls = hallsElem.Deserialize<List<HallSlotItemViewModel>>(JsonOptions);
        else if (obj.TryGetProperty("availabilities", out var availElem) && availElem.ValueKind == JsonValueKind.Array)
            halls = availElem.Deserialize<List<HallSlotItemViewModel>>(JsonOptions);

        string qd = queryDate;
        if (obj.TryGetProperty("queryDate", out var qdElem) && qdElem.ValueKind == JsonValueKind.String)
            qd = qdElem.GetString() ?? queryDate;

        // Nếu object không có halls/availabilities nhưng chính nó là 1 item -> coi như single? Không:
        // trả null để caller giữ rỗng thay vì ép kiểu sai gây lỗi Path $.
        if (halls is null)
            return new HallAvailabilityResponseViewModel { QueryDate = qd };

        return new HallAvailabilityResponseViewModel { QueryDate = qd, Halls = halls };
    }

    public async Task<ApiCallResult<List<AvailableDateItemViewModel>>> GetAvailableDatesAsync(
        string? from,
        string? to,
        string? shift,
        CancellationToken cancellationToken = default)
    {
        // Gọi endpoint mới api/recommendations/available-dates, JsonOptions case-insensitive,
        // try-catch trả list rỗng để modal hiện hướng dẫn thay vì vỡ.
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(from))
                query.Add($"from={Uri.EscapeDataString(from.Trim())}");
            if (!string.IsNullOrWhiteSpace(to))
                query.Add($"to={Uri.EscapeDataString(to.Trim())}");
            if (!string.IsNullOrWhiteSpace(shift))
                query.Add($"shift={Uri.EscapeDataString(shift.Trim())}");
            var uri = "api/recommendations/available-dates" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<List<AvailableDateItemViewModel>>.Success(new List<AvailableDateItemViewModel>());

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return ApiCallResult<List<AvailableDateItemViewModel>>.Success(new List<AvailableDateItemViewModel>());

            var list = JsonSerializer.Deserialize<List<AvailableDateItemViewModel>>(json, JsonOptions);
            return ApiCallResult<List<AvailableDateItemViewModel>>.Success(list ?? new List<AvailableDateItemViewModel>());
        }
        catch (Exception)
        {
            return ApiCallResult<List<AvailableDateItemViewModel>>.Success(new List<AvailableDateItemViewModel>());
        }
    }

    public async Task<ApiCallResult<string>> RegisterPublicBookingAsync(
     PublicBookingRequestViewModel request,
     CancellationToken cancellationToken = default)
    {
        try
        {
            // Đảm bảo GuestCount không bao giờ 0: fallback từ TableCount*10 trước khi gửi Backend
            // để hết lỗi 400 "Số lượng khách phải lớn hơn 0".
            if (request.GuestCount <= 0 && request.TableCount > 0)
                request.GuestCount = request.TableCount * 10;
            if (request.GuestCount <= 0 && request.HallId > 0)
                request.GuestCount = 10;
            // LỖI A: gửi hallScheduleId = 0 khi chưa có lịch để Backend auto-init
            // theo (HallId, EventDate yyyy-MM-dd, Shift). Tuyệt đối không fallback sang hallId.
            // Forward expectedBudget/desiredStyle; nếu Backend cũ thiếu field thì nhúng vào specialRequests.
            var enrichedSpecial = (request.SpecialRequests ?? string.Empty).Trim();
            if (request.ExpectedBudget.HasValue && request.ExpectedBudget.Value > 0
                && !enrichedSpecial.Contains("Ngân sách", StringComparison.OrdinalIgnoreCase))
            {
                var budgetPart = $"Ngân sách dự kiến {request.ExpectedBudget.Value:N0} đ";
                enrichedSpecial = string.IsNullOrEmpty(enrichedSpecial) ? budgetPart : $"{enrichedSpecial}; {budgetPart}";
            }
            if (!string.IsNullOrWhiteSpace(request.DesiredStyle)
                && !enrichedSpecial.Contains(request.DesiredStyle.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var stylePart = $"Phong cách mong muốn: {request.DesiredStyle.Trim()}";
                enrichedSpecial = string.IsNullOrEmpty(enrichedSpecial) ? stylePart : $"{enrichedSpecial}; {stylePart}";
            }
            if (string.IsNullOrWhiteSpace(enrichedSpecial) && request.ExpectedBudget.HasValue)
                enrichedSpecial = $"Đăng ký từ AI Recommendation: Ngân sách dự kiến {request.ExpectedBudget.Value:N0} đ";
            var backendPayload = new
            {
                userId = request.UserId,
                customerName = request.CustomerName,
                phoneNumber = request.PhoneNumber,
                email = request.Email,
                // Giữ hallScheduleId = 0 khi chưa có lịch để Backend auto-init theo
                // (HallId, EventDate yyyy-MM-dd, Shift); không fallback hallId sang đây.
                hallScheduleId = request.HallScheduleId,
                hallId = request.HallId,
                eventDate = request.EventDate,
                shift = NormalizeHallShift(request.Shift),
                menuId = request.MenuId,
                decorationPackageId = request.DecorationPackageId,
                guestCount = request.GuestCount,
                tableCount = request.TableCount,
                spareTableCount = request.SpareTableCount,
                expectedBudget = request.ExpectedBudget,
                desiredStyle = request.DesiredStyle,
                specialRequests = enrichedSpecial,
                customMenuName = request.CustomMenuName,
                customMenuPricePerTable = request.CustomMenuPricePerTable,
                customMenuItems = request.CustomMenuItems,
                serviceCodes = request.ServiceCodes
            };
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/register-booking", backendPayload, cancellationToken);
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

    // UC14: POST /api/v1/public/bookings/lookup (AllowAnonymous, không cần token).
    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>> LookupPublicBookingAsync(
        string bookingCode, string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/v1/public/bookings/lookup",
                new { bookingCode = (bookingCode ?? string.Empty).Trim().ToUpperInvariant(), phoneNumber = (phoneNumber ?? string.Empty).Trim() },
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode),
                    (int)response.StatusCode);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    var detail = System.Text.Json.JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>(
                        dataElem.GetRawText(), JsonOptions);
                    return detail is null
                        ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Failure("API trả về dữ liệu rỗng.")
                        : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Success(detail);
                }
            }
            catch (System.Text.Json.JsonException) { }
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Failure("API trả về dữ liệu tra cứu không hợp lệ.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.MyBookings.PublicBookingDetailViewModel>.Failure("Backend API phản hồi quá lâu.");
        }
    }

    // UC15: POST /api/v1/public/bookings/{id}/cancel-hold (AllowAnonymous).
    public async Task<ApiCallResult<string>> CancelPublicBookingHoldAsync(
        int bookingId, string bookingCode, string phoneNumber, string? reason, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/public/bookings/{bookingId}/cancel-hold",
                new { bookingCode = (bookingCode ?? string.Empty).Trim().ToUpperInvariant(), phoneNumber = (phoneNumber ?? string.Empty).Trim(), reason },
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = ExtractApiMessage(json, response.StatusCode);
            return response.IsSuccessStatusCode
                ? ApiCallResult<string>.Success(message)
                : ApiCallResult<string>.Failure(message, (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<string>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<string>.Failure("Backend API phản hồi quá lâu.");
        }
    }

    // UC10: POST /api/v1/ai-chat/send (AllowAnonymous) — proxy FE -> BE -> AI_Service RAG.
    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>> SendAiChatAsync(
        HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatSendViewModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/v1/ai-chat/send",
                new { message = request.Message ?? string.Empty, history = request.History ?? new List<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatMessageViewModel>() },
                JsonOptions,
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
            var dto = System.Text.Json.JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>(json, JsonOptions);
            return dto is null
                ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>.Failure("Trợ lý AI trả về dữ liệu rỗng.")
                : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>.Success(dto);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatReplyViewModel>.Failure("Backend API phản hồi quá lâu.");
        }
    }

    // UC10: POST /api/v1/ai-chat/extract (AllowAnonymous) — trích xuất thực thể hội thoại.
    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>> ExtractAiEntitiesAsync(
        List<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatMessageViewModel> history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/v1/ai-chat/extract",
                new { history = history ?? new List<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatMessageViewModel>() },
                JsonOptions,
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
            var dto = System.Text.Json.JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>(json, JsonOptions);
            return dto is null
                ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>.Failure("Trợ lý AI trả về dữ liệu rỗng.")
                : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>.Success(dto);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.AiChat.AiChatExtractResultViewModel>.Failure("Backend API phản hồi quá lâu.");
        }
    }

    /// <summary>
    /// Chuẩn hóa ca tổ chức về "Ca trưa" / "Ca tối" trước khi gửi Backend,
    /// tránh gửi chuỗi dài "Ca Tối (17:30 – 21:30)" khiến Backend không resolve được LichSanh.
    /// </summary>
    private static string NormalizeHallShift(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Ca tối";
        var s = raw.Trim();
        if (s.Contains("trưa", StringComparison.OrdinalIgnoreCase) || s.Contains("trua", StringComparison.OrdinalIgnoreCase))
            return "Ca trưa";
        if (s.Contains("tối", StringComparison.OrdinalIgnoreCase) || s.Contains("toi", StringComparison.OrdinalIgnoreCase))
            return "Ca tối";
        return s;
    }

    private static string ExtractApiMessage(string json, System.Net.HttpStatusCode status)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == System.Text.Json.JsonValueKind.String)
                        return msg.GetString() ?? $"Yêu cầu không thành công ({(int)status}).";
                    if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == System.Text.Json.JsonValueKind.String)
                        return title.GetString() ?? $"Yêu cầu không thành công ({(int)status}).";
                }
            }
            catch { }
            var trimmed = json.Trim();
            if (trimmed.Length <= 300 && !trimmed.StartsWith("{") && !trimmed.StartsWith("<"))
                return trimmed;
        }
        return $"Yêu cầu không thành công ({(int)status}).";
    }

    public Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Profile.ProfileDto>> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HeThongDatTiecCuoi_WEB.Models.Profile.ProfileDto>(HttpMethod.Get, "api/profile", null, accessToken, cancellationToken);

    public Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Profile.ProfileUpdateResponse>> UpdateProfileAsync(
        HeThongDatTiecCuoi_WEB.Models.Profile.ProfilePageViewModel model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HeThongDatTiecCuoi_WEB.Models.Profile.ProfileUpdateResponse>(HttpMethod.Put, "api/profile", new
        {
            model.Email,
            model.FullName,
            model.PhoneNumber
        }, accessToken, cancellationToken);

}

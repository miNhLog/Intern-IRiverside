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
using HeThongDatTiecCuoi_WEB.Models.Incident;
using HeThongDatTiecCuoi_WEB.Models.Coordination;
using HeThongDatTiecCuoi_WEB.Models.Review;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.DecorPackages;
using HeThongDatTiecCuoi_WEB.Models.ScheduleChange;
using HeThongDatTiecCuoi_WEB.Models.Banners;
using HeThongDatTiecCuoi_WEB.Options;
using Microsoft.Extensions.Options;

public sealed class RiversideApiClient : IRiversideApiClient
{
    public Task<ApiCallResult<ContractActionResponseViewModel>> ConfirmManagerDepositAsync(
        string accessToken, int id, ConfirmDepositViewModel model, CancellationToken cancellationToken) =>
        SendAsync<ContractActionResponseViewModel>(HttpMethod.Post, $"api/manager/contracts/{id}/deposit-commitment", model, accessToken, cancellationToken);

    public async Task<ApiCallResult<string>> ReschedulePublicBookingAsync(
        int bookingId, string bookingCode, string phoneNumber, DateTime newDate, string newShift, string? reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/public/bookings/{bookingId}/reschedule",
                new
                {
                    bookingCode = (bookingCode ?? string.Empty).Trim().ToUpperInvariant(),
                    phoneNumber = (phoneNumber ?? string.Empty).Trim(),
                    newDate = newDate.ToString("yyyy-MM-dd"),
                    newShift = (newShift ?? string.Empty).Trim(),
                    reason
                }, cancellationToken);
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
    public Task<ApiCallResult<List<BannerViewModel>>> GetPublicBannersAsync(CancellationToken cancellationToken) =>
        SendAsync<List<BannerViewModel>>(HttpMethod.Get, "api/banners/public", null, null, cancellationToken);
    public Task<ApiCallResult<List<BannerViewModel>>> GetPublicBannersAsync(string placement, CancellationToken cancellationToken) =>
        SendAsync<List<BannerViewModel>>(HttpMethod.Get, $"api/banners/public?placement={Uri.EscapeDataString(placement)}", null, null, cancellationToken);
    public Task<ApiCallResult<JsonElement>> GetChecklistsAsync(string token, CancellationToken ct) =>
        SendAsync<JsonElement>(HttpMethod.Get, "api/checklists", null, token, ct);
    public Task<ApiCallResult<JsonElement>> ExecuteChecklistAsync(string token, int bookingId, string action, JsonElement command, CancellationToken ct) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"api/checklists/{bookingId}/{Uri.EscapeDataString(action)}", command, token, ct);
    public Task<ApiCallResult<ChecklistSummaryViewModel>> GetChecklistSummaryAsync(string token, CancellationToken ct) =>
        SendAsync<ChecklistSummaryViewModel>(HttpMethod.Get, "api/checklists/summary", null, token, ct);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _httpClient;
    private readonly ProjectImageCatalog _projectImages;
    private readonly RecommendationOptions _recOptions;
    private readonly ILogger<RiversideApiClient>? _recLogger;

    public RiversideApiClient(
        HttpClient httpClient,
        ProjectImageCatalog projectImages,
        IOptions<RecommendationOptions>? recommendationOptions = null,
        ILogger<RiversideApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _projectImages = projectImages;
        _recOptions = recommendationOptions?.Value ?? new RecommendationOptions();
        _recLogger = logger;
    }

    public Task<ApiCallResult<IncidentListResponseViewModel>> GetIncidentsAsync(string accessToken,
        string? search, string? status, string? severity, int? hallId, DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={Math.Clamp(pageSize, 1, 50)}" };
        AddQueryParameter(query, "search", search);
        AddQueryParameter(query, "status", status);
        AddQueryParameter(query, "severity", severity);
        if (hallId.HasValue) query.Add($"hallId={hallId.Value}");
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        return SendAsync<IncidentListResponseViewModel>(HttpMethod.Get, $"api/incidents?{string.Join('&', query)}",
            null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<List<IncidentBookingOptionViewModel>>> GetIncidentBookingOptionsAsync(
        string accessToken, CancellationToken cancellationToken) =>
        SendAsync<List<IncidentBookingOptionViewModel>>(HttpMethod.Get, "api/incidents/booking-options", null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IncidentDetailViewModel>> GetIncidentAsync(string accessToken, int id,
        CancellationToken cancellationToken) => SendAsync<IncidentDetailViewModel>(HttpMethod.Get,
            $"api/incidents/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<IncidentDetailViewModel>> CreateIncidentAsync(string accessToken,
        IncidentFormViewModel model, CancellationToken cancellationToken) =>
        SendAsync<IncidentDetailViewModel>(HttpMethod.Post, "api/incidents", model, accessToken, cancellationToken);

    public Task<ApiCallResult<IncidentDetailViewModel>> UpdateIncidentAsync(string accessToken, int id,
        IncidentFormViewModel model, CancellationToken cancellationToken) =>
        SendAsync<IncidentDetailViewModel>(HttpMethod.Put, $"api/incidents/{id}", model, accessToken, cancellationToken);

    public Task<ApiCallResult<IncidentDetailViewModel>> UpdateIncidentStatusAsync(string accessToken, int id,
        string status, string? resolution, CancellationToken cancellationToken) =>
        SendAsync<IncidentDetailViewModel>(HttpMethod.Patch, $"api/incidents/{id}/status",
            new { status, resolution }, accessToken, cancellationToken);

    public Task<ApiCallResult<IncidentDetailViewModel>> UpdateIncidentResolutionAsync(string accessToken, int id,
        string resolution, CancellationToken cancellationToken) =>
        SendAsync<IncidentDetailViewModel>(HttpMethod.Patch, $"api/incidents/{id}/resolution",
            new { resolution }, accessToken, cancellationToken);

    public Task<ApiCallResult<List<IncidentHistoryItemViewModel>>> GetIncidentHistoryAsync(string accessToken, int id,
        CancellationToken cancellationToken) => SendAsync<List<IncidentHistoryItemViewModel>>(HttpMethod.Get,
            $"api/incidents/{id}/history", null, accessToken, cancellationToken);

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

    public Task<ApiCallResult<List<ManagerAvailableCoordinatorViewModel>>> GetManagerCoordinationAvailableCoordinatorsAsync(
        string accessToken, int bookingId, CancellationToken cancellationToken) =>
        SendAsync<List<ManagerAvailableCoordinatorViewModel>>(HttpMethod.Get,
            $"api/manager/event-coordination/{bookingId}/available-coordinators", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ManagerAssignResultViewModel>> AssignManagerEventCoordinatorAsync(
        string accessToken, int bookingId, int coordinatorEmployeeId, string reason, CancellationToken cancellationToken) =>
        SendAsync<ManagerAssignResultViewModel>(HttpMethod.Post,
            $"api/manager/event-coordination/{bookingId}/assign",
            new { CoordinatorEmployeeId = coordinatorEmployeeId, Reason = reason },
            accessToken, cancellationToken);

    public Task<ApiCallResult<HallManagerCoordinationListViewModel>> GetHallManagerCoordinationAsync(
        string accessToken, string? search, int? hallId, DateTime? eventDate, string? coordinationStatus,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={Math.Clamp(pageSize, 1, 50)}" };
        AddQueryParameter(query, "search", search);
        if (hallId.HasValue) query.Add($"hallId={hallId.Value}");
        if (eventDate.HasValue) query.Add($"eventDate={eventDate.Value:yyyy-MM-dd}");
        AddQueryParameter(query, "coordinationStatus", coordinationStatus);
        return SendAsync<HallManagerCoordinationListViewModel>(HttpMethod.Get,
            $"api/hall-manager/coordination-assignments?{string.Join('&', query)}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<List<CoordinatorAvailabilityViewModel>>> GetCoordinatorAvailabilityAsync(
        string accessToken, int bookingId, CancellationToken cancellationToken) =>
        SendAsync<List<CoordinatorAvailabilityViewModel>>(HttpMethod.Get,
            $"api/hall-manager/coordination-assignments/{bookingId}/coordinators", null, accessToken, cancellationToken);

    public Task<ApiCallResult<HallManagerCoordinationDetailViewModel>> GetHallManagerCoordinationDetailAsync(
        string accessToken, int bookingId, CancellationToken cancellationToken) =>
        SendAsync<HallManagerCoordinationDetailViewModel>(HttpMethod.Get,
            $"api/hall-manager/coordination-assignments/{bookingId}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<CoordinationMutationViewModel>> AssignCoordinatorAsync(string accessToken,
        int bookingId, int coordinatorEmployeeId, CancellationToken cancellationToken) =>
        SendAsync<CoordinationMutationViewModel>(HttpMethod.Post,
            $"api/hall-manager/coordination-assignments/{bookingId}", new { coordinatorEmployeeId }, accessToken, cancellationToken);

    public Task<ApiCallResult<CoordinationMutationViewModel>> ChangeCoordinatorAsync(string accessToken,
        int bookingId, int coordinatorEmployeeId, string reason, CancellationToken cancellationToken) =>
        SendAsync<CoordinationMutationViewModel>(HttpMethod.Put,
            $"api/hall-manager/coordination-assignments/{bookingId}", new { coordinatorEmployeeId, reason }, accessToken, cancellationToken);

    public Task<ApiCallResult<CoordinationMutationViewModel>> CancelCoordinatorAssignmentAsync(string accessToken,
        int bookingId, string reason, CancellationToken cancellationToken) =>
        SendAsync<CoordinationMutationViewModel>(HttpMethod.Patch,
            $"api/hall-manager/coordination-assignments/{bookingId}/cancel", new { reason }, accessToken, cancellationToken);

    public Task<ApiCallResult<List<CoordinatorAssignedPartyViewModel>>> GetCoordinatorAssignedPartiesAsync(
        string accessToken, CancellationToken cancellationToken) =>
        SendAsync<List<CoordinatorAssignedPartyViewModel>>(HttpMethod.Get,
            "api/coordinator/assigned-parties", null, accessToken, cancellationToken);

    public Task<ApiCallResult<GuestReviewFormViewModel>> GetGuestReviewAsync(string token, CancellationToken cancellationToken) =>
        SendAsync<GuestReviewFormViewModel>(HttpMethod.Get,
            $"api/reviews/guest/{Uri.EscapeDataString(token)}", null, null, cancellationToken);

    public Task<ApiCallResult<GuestReviewSubmitResult>> SubmitGuestReviewAsync(string token,
        GuestReviewScoresViewModel scores, string sessionId, CancellationToken cancellationToken)
    {
        const string ReviewSessionHeader = "X-Review-Session";
        var headers = sessionId.Length is >= 16 and <= 80 && sessionId.All(char.IsAsciiHexDigit)
            ? new Dictionary<string, string> { [ReviewSessionHeader] = sessionId }
            : null;
        return SendAsync<GuestReviewSubmitResult>(HttpMethod.Post,
            $"api/reviews/guest/{Uri.EscapeDataString(token)}", new
            {
                scores.HallScore, scores.FoodScore, scores.ServiceScore,
                scores.SoundScore, scores.LightingScore, scores.HygieneScore,
                scores.Comment, SessionId = sessionId
            }, null, cancellationToken, headers);
    }

    public Task<ApiCallResult<ReviewQrIssueViewModel>> IssueCoordinatorReviewQrAsync(string accessToken,
        int bookingId, CancellationToken cancellationToken) =>
        SendAsync<ReviewQrIssueViewModel>(HttpMethod.Post,
            $"api/reviews/coordinator/{bookingId}/issue", null, accessToken, cancellationToken);

    public Task<ApiCallResult<byte[]>> GetCoordinatorReviewQrPngAsync(string accessToken, int bookingId,
        CancellationToken cancellationToken) =>
        SendBytesAsync(HttpMethod.Get, $"api/reviews/coordinator/{bookingId}/qr.png", accessToken, cancellationToken);

    public Task<ApiCallResult<byte[]>> GetManagementReviewQrPngAsync(string accessToken, int bookingId,
        CancellationToken cancellationToken) =>
        SendBytesAsync(HttpMethod.Get, $"api/reviews/management/{bookingId}/qr.png", accessToken, cancellationToken);

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

    public Task<ApiCallResult<List<ManagerContractAmendmentViewModel>>> GetManagerContractAmendmentsAsync(
        string accessToken, int id, CancellationToken cancellationToken) =>
        SendAsync<List<ManagerContractAmendmentViewModel>>(HttpMethod.Get,
            $"api/manager/contracts/{id}/amendments", null, accessToken, cancellationToken);

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

    public Task<ApiCallResult<object>> RecordManagerPaymentAsync(
        string accessToken, RecordPaymentViewModel model, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Post, "api/manager/payments/record-cash", model, accessToken, cancellationToken);

    public Task<ApiCallResult<ContractPayOsLinkResultViewModel>> CreateManagerPayOsPaymentLinkAsync(
        string accessToken, CreateContractPayOsLinkViewModel model, CancellationToken cancellationToken) =>
        SendAsync<ContractPayOsLinkResultViewModel>(HttpMethod.Post, "api/manager/payments/create-payos-link", model, accessToken, cancellationToken);

    public Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>> CheckManagerPayOsPaymentStatusAsync(
        string accessToken, long orderCode, CancellationToken cancellationToken) =>
        SendAsync<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>(
            HttpMethod.Get, $"api/manager/payments/check-payos/{orderCode}", null, accessToken, cancellationToken);

    public async Task<ApiCallResult<PdfDownloadViewModel>> DownloadPublicContractPdfAsync(
        string bookingCode, string phone, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"api/v1/public/bookings/{Uri.EscapeDataString(bookingCode)}/contract/pdf?phone={Uri.EscapeDataString(phone)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<PdfDownloadViewModel>.Failure(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"HopDong_{bookingCode}.pdf";
            return ApiCallResult<PdfDownloadViewModel>.Success(new PdfDownloadViewModel(
                bytes, fileName, response.Content.Headers.ContentType?.MediaType ?? "application/pdf"));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<PdfDownloadViewModel>.Failure("Không thể kết nối Backend API.");
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
                    hall.ImageUrl = ResolvePublicImageUrl(hall.ImageUrl)
                        ?? _projectImages.GetImage("HallCovers", hall.HallName);
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
                    hall.ImageUrl = ResolvePublicImageUrl(hall.ImageUrl) ?? _projectImages.GetImage("HallCovers", hall.HallName);
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
                result.Value.ImageUrl = ResolvePublicImageUrl(result.Value.ImageUrl) ?? _projectImages.GetImage("HallCovers", result.Value.HallName);
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
            var result = await SendAsync<PublicMenuListResponse>(
                HttpMethod.Get,
                $"api/menus/public?{string.Join('&', query)}",
                null,
                null,
                cancellationToken);
            if (result.Value is not null)
            {
                foreach (var menu in result.Value.Items)
                    menu.ImageUrl = ResolvePublicImageUrl(menu.ImageUrl)
                        ?? _projectImages.GetImage("Menus", menu.MenuName);
            }
            return result;
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
                result.Value.ImageUrl = ResolvePublicImageUrl(result.Value.ImageUrl)
                    ?? _projectImages.GetImage("Menus", result.Value.MenuName);
                foreach (var dish in result.Value.Dishes)
                {
                    dish.ImageUrl = ResolveUploadedImageUrl(dish.ImageUrl)
                        ?? _projectImages.GetImage("Dishes", dish.DishCode)
                        ?? ResolvePublicImageUrl(dish.ImageUrl);
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
                    dish.ImageUrl = ResolveUploadedImageUrl(dish.ImageUrl)
                        ?? _projectImages.GetImage("Dishes", dish.DishCode)
                        ?? ResolvePublicImageUrl(dish.ImageUrl);
            }

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<List<PublicDishDto>>.Failure(
                "API trả về dữ liệu món ăn không hợp lệ.");
        }
    }


    public async Task<ApiCallResult<PublicDecorPackageListResponse>> GetPublicDecorPackagesAsync(
    string? keyword,
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
        AddQueryParameter(query, "sort", sort);

        try
        {
            var result = await SendAsync<PublicDecorPackageListResponse>(
                HttpMethod.Get,
                $"api/decor-packages/public?{string.Join('&', query)}",
                null,
                null,
                cancellationToken);


            if (result.Value is not null)
            {
                foreach (var package in result.Value.Items)
                    package.ImageUrl = ResolveUploadedImageUrl(package.ImageUrl)
                        ?? _projectImages.GetImage("DecorPackages", package.PackageName)
                        ?? ResolvePublicImageUrl(package.ImageUrl);
            }

            return result;
        }
        catch (JsonException)
        {

            return ApiCallResult<PublicDecorPackageListResponse>.Failure(
                "API trả về dữ liệu gói trang trí không hợp lệ.");
        }
    }

    public async Task<ApiCallResult<PublicDecorPackageDto>> GetPublicDecorPackageAsync(
        int decorationPackageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendAsync<PublicDecorPackageDto>(
                HttpMethod.Get,
                $"api/decor-packages/public/{decorationPackageId}",
                null,
                null,
                cancellationToken);

            if (result.Value is not null)
                result.Value.ImageUrl = ResolveUploadedImageUrl(result.Value.ImageUrl)
                    ?? _projectImages.GetImage("DecorPackages", result.Value.PackageName)
                    ?? ResolvePublicImageUrl(result.Value.ImageUrl);

            return result;
        }
        catch (JsonException)
        {
            return ApiCallResult<PublicDecorPackageDto>.Failure(
                "API trả về dữ liệu chi tiết gói trang trí không hợp lệ.");
        }
    }
    public async Task<ApiCallResult<List<HallDto>>> GetHallsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<List<HallDto>>(
            HttpMethod.Get,
            "api/halls",
            null,
            accessToken,
            cancellationToken);
        if (result.Value is not null)
        {
            foreach (var hall in result.Value)
                hall.ImageUrl = ResolvePublicImageUrl(hall.ImageUrl)
                    ?? _projectImages.GetImage("HallCovers", hall.HallName);
        }
        return result;
    }

    public async Task<ApiCallResult<HallDto>> GetHallByIdAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<HallDto>(
            HttpMethod.Get,
            $"api/halls/{hallId}",
            null,
            accessToken,
            cancellationToken);
        if (result.Value is not null)
            result.Value.ImageUrl = ResolvePublicImageUrl(result.Value.ImageUrl)
                ?? _projectImages.GetImage("HallCovers", result.Value.HallName);
        return result;
    }

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

    public Task<ApiActionResult> UploadHallImageAsync(
        int hallId, Stream fileStream, string fileName, string? contentType,
        string accessToken, CancellationToken cancellationToken) =>
        UploadImageAsync($"api/halls/{hallId}/image", fileStream, fileName, contentType,
            accessToken, cancellationToken);

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

    public Task<ApiCallResult<HallManagerDashboardViewModel>> GetHallManagerDashboardAsync(
        string accessToken, CancellationToken cancellationToken) =>
        SendAsync<HallManagerDashboardViewModel>(HttpMethod.Get, "api/hall-manager/dashboard", null, accessToken, cancellationToken);

    public Task<ApiCallResult<HallManagerScheduleViewModel>> GetHallManagerDashboardScheduleAsync(
        string accessToken, DateTime date, CancellationToken cancellationToken) =>
        SendAsync<HallManagerScheduleViewModel>(HttpMethod.Get,
            $"api/hall-manager/dashboard/schedule?date={date:yyyy-MM-dd}", null, accessToken, cancellationToken);

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
        string? group,
        string? outcome,
        string? authMethod,
        string? mode,
        string? importantGroup,
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
        if (!string.IsNullOrWhiteSpace(group)) query.Add($"group={Uri.EscapeDataString(group)}");
        if (!string.IsNullOrWhiteSpace(outcome)) query.Add($"outcome={Uri.EscapeDataString(outcome)}");
        if (!string.IsNullOrWhiteSpace(authMethod)) query.Add($"authMethod={Uri.EscapeDataString(authMethod)}");
        if (!string.IsNullOrWhiteSpace(mode)) query.Add($"mode={Uri.EscapeDataString(mode)}");
        if (!string.IsNullOrWhiteSpace(importantGroup)) query.Add($"importantGroup={Uri.EscapeDataString(importantGroup)}");

        return SendAsync<AuditLogPageViewModel>(
            HttpMethod.Get,
            "api/admin/audit-logs?" + string.Join("&", query),
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ApiMessageResponse>> RecordLogoutAsync(string accessToken, CancellationToken cancellationToken) =>
        SendAsync<ApiMessageResponse>(HttpMethod.Post, "api/auth/logout", new { }, accessToken, cancellationToken);


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

    private Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<T>(method, uri, body, accessToken, cancellationToken, null);

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken,
        IDictionary<string, string>? headers)
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
            if (headers is not null)
            {
                foreach (var (name, value) in headers)
                    request.Headers.TryAddWithoutValidation(name, value);
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
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure("Phản hồi từ Backend không đúng định dạng mong đợi.");
        }
    }

    private async Task<ApiCallResult<byte[]>> SendBytesAsync(
        HttpMethod method,
        string uri,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ApiCallResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ApiCallResult<byte[]>.Failure(ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<byte[]>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<byte[]>.Failure("Backend API phản hồi quá lâu.");
        }
    }

    private static void AddQueryParameter(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }

    private string? ResolvePublicImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteImageUrl))
            return absoluteImageUrl.Scheme is "http" or "https" ? absoluteImageUrl.ToString() : null;

        var normalized = imageUrl.Trim();
        if (normalized.StartsWith("~/"))
            normalized = normalized.Substring(1);
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        if (normalized.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
            return normalized;

        return _httpClient.BaseAddress is not null &&
            Uri.TryCreate(_httpClient.BaseAddress, normalized.TrimStart('/'), out var resolvedImageUrl)
            ? resolvedImageUrl.ToString()
            : normalized;
    }

    private string? ResolveUploadedImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        if (imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return ResolvePublicImageUrl(imageUrl);
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute) &&
            absolute.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return ResolvePublicImageUrl(imageUrl);
        return null;
    }

    private async Task<ApiActionResult> UploadImageAsync(
        string path,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var multipart = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);
            if (!string.IsNullOrWhiteSpace(contentType))
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            multipart.Add(fileContent, "file", fileName);
            request.Content = multipart;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return ApiActionResult.Success();
            var error = await ReadErrorAsync(response, cancellationToken);
            return ApiActionResult.Failure(error ?? "Không thể tải ảnh lên.");
        }
        catch (HttpRequestException)
        {
            return ApiActionResult.Failure("Không thể kết nối Backend API.");
        }
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

    public async Task<ApiCallResult<List<MenuViewModel>>> GetMenusAsync(
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

        var result = await SendJsonAsync<List<MenuViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
        if (result.Value is not null)
        {
            foreach (var menu in result.Value)
                menu.ImageUrl = ResolvePublicImageUrl(menu.ImageUrl)
                    ?? _projectImages.GetImage("Menus", menu.MenuName);
        }
        return result;
    }

    public async Task<ApiCallResult<Models.ManagerMenu.ManagerMenuPageViewModel>> GetManagerMenusAsync(
        string? keyword, string? status, int page, int pageSize,
        string accessToken, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        var result = await SendJsonAsync<Models.ManagerMenu.ManagerMenuPageViewModel>(
            HttpMethod.Get, "api/menus/management?" + string.Join("&", query), null, accessToken, cancellationToken);
        if (result.Value is not null)
        {
            foreach (var menu in result.Value.Items)
                menu.ImageUrl = ResolvePublicImageUrl(menu.ImageUrl)
                    ?? _projectImages.GetImage("Menus", menu.MenuName);
        }
        return result;
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
                model.PricePerTable,
                model.DishIds
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

    public Task<ApiActionResult> UploadMenuImageAsync(
        int menuId, Stream fileStream, string fileName, string? contentType,
        string accessToken, CancellationToken cancellationToken) =>
        UploadImageAsync($"api/menus/{menuId}/image", fileStream, fileName, contentType,
            accessToken, cancellationToken);

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
    // MÃ“N Ä‚N
    // =========================================================

    public async Task<ApiCallResult<List<DishViewModel>>> GetDishesAsync(
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

        var result = await SendJsonAsync<List<DishViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
        if (result.Value is not null)
        {
            foreach (var dish in result.Value)
                dish.ImageUrl = ResolveUploadedImageUrl(dish.ImageUrl)
                    ?? _projectImages.GetImage("Dishes", dish.DishCode)
                    ?? ResolvePublicImageUrl(dish.ImageUrl);
        }
        return result;
    }

    public async Task<ApiCallResult<Models.ManagerDish.ManagerDishPageViewModel>> GetManagerDishesAsync(
        string? keyword, string? category, string? status, int page, int pageSize,
        string accessToken, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        var result = await SendJsonAsync<Models.ManagerDish.ManagerDishPageViewModel>(HttpMethod.Get,
            "api/dishes/management?" + string.Join("&", query), null, accessToken, cancellationToken);
        if (result.Value is not null)
        {
            foreach (var dish in result.Value.Items)
                dish.ImageUrl = ResolveUploadedImageUrl(dish.ImageUrl)
                    ?? _projectImages.GetImage("Dishes", dish.DishCode)
                    ?? ResolvePublicImageUrl(dish.ImageUrl);
        }
        return result;
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
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(
                "Dữ liệu Backend API không tương thích với giao diện.");
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

    public async Task<ApiCallResult<PdfDownloadViewModel>> DownloadManagerContractAmendmentPdfAsync(
        string accessToken, int id, Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/manager/contracts/{id}/amendments/{requestId:D}/pdf");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiCallResult<PdfDownloadViewModel>.Failure(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"PhuLuc_{id}_{requestId:N}.pdf";
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

    public Task<ApiCallResult<dynamic>> GetManagerBookingCancelQuoteAsync(int id, ManagerBookingCancelQuoteRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<dynamic>(HttpMethod.Post, $"api/manager/bookings/{id}/cancel-quote", request, accessToken, cancellationToken);

    public Task<ApiCallResult<dynamic>> CancelManagerBookingAsync(int id, ManagerBookingCancelRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<dynamic>(HttpMethod.Post, $"api/manager/bookings/{id}/cancel", request, accessToken, cancellationToken);

    public async Task<ApiCallResult<dynamic>> GetManagerBookingRescheduleQuoteAsync(int id, ManagerBookingRescheduleRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"NewDate={request.NewDate:yyyy-MM-dd}",
            $"NewShift={Uri.EscapeDataString(request.NewShift)}",
            $"NewHallId={request.NewHallId}",
            $"ChangeMenu={request.ChangeMenu}"
        };
        if (request.MenuId.HasValue) query.Add($"menuId={request.MenuId.Value}");
        if (request.CustomDishIds is not null) query.AddRange(request.CustomDishIds.Select(value => $"customDishIds={value}"));
        return await SendJsonAsync<dynamic>(HttpMethod.Get, $"api/manager/bookings/{id}/reschedule-quote?{string.Join("&", query)}", null, accessToken, cancellationToken);
    }

    public async Task<ApiCallResult<dynamic>> RescheduleManagerBookingAsync(int id, ManagerBookingRescheduleRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        await SendJsonAsync<dynamic>(HttpMethod.Put, $"api/manager/bookings/{id}/reschedule", request, accessToken, cancellationToken);

    public async Task<ApiCallResult<List<ManagerBookingRefundObligationViewModel>>> GetManagerBookingRefundObligationsAsync(int id, string? accessToken, CancellationToken cancellationToken = default) =>
        await SendJsonAsync<List<ManagerBookingRefundObligationViewModel>>(HttpMethod.Get, $"api/manager/bookings/{id}/refund-obligations", null, accessToken, cancellationToken);

    public async Task<ApiCallResult<dynamic>> RecordManagerBookingRefundConfirmationAsync(int id, ManagerBookingRefundConfirmationRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        await SendJsonAsync<dynamic>(HttpMethod.Post, $"api/manager/bookings/{id}/refund-confirmations", request, accessToken, cancellationToken);

    public async Task<ApiCallResult<dynamic>> HandoffManagerBookingAsync(int id, ManagerBookingHandoffRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        await SendJsonAsync<dynamic>(HttpMethod.Post, $"api/manager/bookings/{id}/handoff", request, accessToken, cancellationToken);

    public Task<ApiCallResult<BookingListResponseViewModel>> GetHallManagerOperationalBookingsAsync(BookingFilterRequestViewModel filter, string? accessToken, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"PageIndex={Math.Max(1, filter.PageIndex)}", $"PageSize={Math.Clamp(filter.PageSize, 1, 50)}" };
        AddQueryParameter(query, "Keyword", filter.Keyword);
        AddQueryParameter(query, "StatusCode", filter.Status);
        if (filter.HallId.HasValue) query.Add($"HallId={filter.HallId.Value}");
        if (filter.FromDate.HasValue) query.Add($"FromDate={filter.FromDate:yyyy-MM-dd}");
        if (filter.ToDate.HasValue) query.Add($"ToDate={filter.ToDate:yyyy-MM-dd}");
        return SendJsonAsync<BookingListResponseViewModel>(HttpMethod.Get, $"api/manager/bookings?{string.Join("&", query)}", null, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<BookingDetailViewModel>> GetHallManagerOperationalBookingAsync(int id, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<BookingDetailViewModel>(HttpMethod.Get, $"api/manager/bookings/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<BookingHallOptionViewModel>>> GetHallManagerRescheduleOptionsAsync(int id, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<List<BookingHallOptionViewModel>>(HttpMethod.Get, $"api/manager/bookings/{id}/reschedule-options", null, accessToken, cancellationToken);

    public Task<ApiCallResult<dynamic>> RescheduleHallManagerBookingAsync(int id, ManagerBookingRescheduleRequestViewModel request, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<dynamic>(HttpMethod.Put, $"api/manager/bookings/{id}/reschedule", request, accessToken, cancellationToken);

    public Task<ApiCallResult<dynamic>> CancelHallManagerBookingAsync(int id, string reason, string? accessToken, CancellationToken cancellationToken = default) =>
        SendJsonAsync<dynamic>(HttpMethod.Post, $"api/manager/bookings/{id}/cancel", new { reason }, accessToken, cancellationToken);

    // VĐ2: mặc định gọi suggest-top3-hybrid (AI hybrid s_final=alpha*s_cf+beta*s_cbf+gamma*S_wsm),
    // giữ fallback về suggest-top3 (heuristic C#) khi AI timeout (HybridTimeoutSeconds, mặc định 5s).
    // Tắt khẩn: Recommendation:UseHybridDefault=false → quay về heuristic cũ (<5 phút). Truyền userId (CustomerId nullable, anonymous=null).
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
                serviceNeeds,
                userId = request.CustomerId
            };

            var useHybrid = _recOptions.UseHybridDefault;
            var allowFallback = _recOptions.FallbackToHeuristic;
            var hybridTimeoutSec = Math.Clamp(_recOptions.HybridTimeoutSeconds <= 0 ? 5 : _recOptions.HybridTimeoutSeconds, 2, 20);

            // Nhánh mặc định VĐ2: thử hybrid trước với timeout riêng (5s), độc lập với HttpClient.Timeout 20s.
            if (useHybrid)
            {
                try
                {
                    using var hybridCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    hybridCts.CancelAfter(TimeSpan.FromSeconds(hybridTimeoutSec));
                    var hybridResult = await PostRecommendationEnvelopeAsync(
                        "api/recommendations/suggest-top3-hybrid", backendPayload, tableCount, hybridCts.Token);
                    if (hybridResult is not null)
                        return ApiCallResult<RecommendationResponseViewModel>.Success(hybridResult);
                    _recLogger?.LogWarning("GetTop3Recommendations: hybrid trả rỗng, fallback heuristic={Allow}.", allowFallback);
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
                {
                    _recLogger?.LogWarning(ex, "GetTop3Recommendations: hybrid lỗi/timeout ({Sec}s), fallback heuristic={Allow}.",
                        hybridTimeoutSec, allowFallback);
                }
                if (!allowFallback)
                    return ApiCallResult<RecommendationResponseViewModel>.Failure("Hệ thống gợi ý AI đang bận, vui lòng thử lại sau.");
                // Rơi xuống nhánh heuristic bên dưới (giữ fallback).
            }

            // Chuỗi endpoint Backend heuristic cũ: api/recommendations/suggest-top3.
            // (VĐ2 giữ làm fallback + đường tắt khẩn khi UseHybridDefault=false.)
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/suggest-top3", backendPayload, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // Bóc {message} tiếng Việt từ Backend thay vì dump raw JSON/HTML cho user.
                return ApiCallResult<RecommendationResponseViewModel>.Failure(ExtractApiMessage(errorContent, response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<RecommendationResponseViewModel>(JsonOptions, cancellationToken);
            var vm = result ?? new RecommendationResponseViewModel();
            vm.Source ??= "heuristic";
            vm.TopCombos ??= new List<ComboSuggestionViewModel>();
            foreach (var c in vm.TopCombos)
            {
                c.Explanations ??= new List<string>();
                c.RecommendationReasons ??= new List<string>();
            }
            if (vm.Data is null) vm.Data = vm.TopCombos;
            if (vm.Count <= 0) vm.Count = vm.TopCombos.Count;
            return ApiCallResult<RecommendationResponseViewModel>.Success(vm);
        }
        catch (Exception ex)
        {
            return ApiCallResult<RecommendationResponseViewModel>.Failure($"Không thể kết nối thuật toán khuyến nghị: {ex.Message}");
        }
    }

    /// <summary>VĐ2: POST envelope {success, source, data} của BE hybrid (data là AI snake_case top_3
    /// hoặc fallback-internal PascalCase topCombos). Map null-safe sang ViewModel, không ném JsonException.</summary>
    private async Task<RecommendationResponseViewModel?> PostRecommendationEnvelopeAsync(
        string requestUri, object backendPayload, int tableCount, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(requestUri, backendPayload, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var source = GetJsonString(root, "source", "Source");
            JsonElement dataEl = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d1))
                dataEl = d1;
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Data", out var d2))
                dataEl = d2;
            if (dataEl.ValueKind == JsonValueKind.Null || dataEl.ValueKind == JsonValueKind.Undefined)
                return null;

            // data là AI hybrid snake_case (top_3) → map thủ công từng item.
            if (dataEl.ValueKind == JsonValueKind.Object
                && (HasProperty(dataEl, "top_3") || HasProperty(dataEl, "top3") || HasProperty(dataEl, "Top3")))
            {
                var items = GetJsonArray(dataEl, "top_3", "top3", "Top3", "top_3_recommendations");
                var combos = new List<ComboSuggestionViewModel>();
                var rank = 0;
                foreach (var it in items)
                {
                    rank++;
                    combos.Add(MapAiItemToCombo(it, rank, tableCount));
                }
                var aiVm = new RecommendationResponseViewModel
                {
                    Source = string.IsNullOrWhiteSpace(source) ? "ai-service-hybrid" : source,
                    WeightsSource = GetJsonString(dataEl, "weights_source", "weightsSource", "WeightsSource"),
                    AlgorithmNotice = GetJsonString(dataEl, "algorithm_notice", "algorithmNotice", "AlgorithmNotice") ?? string.Empty,
                    TotalCandidatesEvaluated = GetJsonInt(dataEl, "total_candidates_evaluated", "totalCandidatesEvaluated", "TotalCandidatesEvaluated"),
                    TopCombos = combos,
                    AnalyzedAt = DateTime.Now,
                };
                aiVm.Data = combos;
                aiVm.Count = combos.Count;
                return combos.Count > 0 || aiVm.TotalCandidatesEvaluated >= 0 ? aiVm : null;
            }

            // data là fallback-internal / heuristic PascalCase → deserialize trực tiếp, null-safe.
            var raw = dataEl.GetRawText();
            var heuristic = JsonSerializer.Deserialize<RecommendationResponseViewModel>(raw, JsonOptions)
                ?? new RecommendationResponseViewModel();
            heuristic.Source = string.IsNullOrWhiteSpace(heuristic.Source)
                ? (string.IsNullOrWhiteSpace(source) ? "fallback-internal" : source)
                : heuristic.Source;
            heuristic.TopCombos ??= new List<ComboSuggestionViewModel>();
            foreach (var c in heuristic.TopCombos)
            {
                c.HallImageUrl = _projectImages.GetImage("HallCovers", c.HallName)
                    ?? ResolvePublicImageUrl(c.HallImageUrl);
                c.Explanations ??= new List<string>();
                c.RecommendationReasons ??= new List<string>();
            }
            if (heuristic.Data is null) heuristic.Data = heuristic.TopCombos;
            if (heuristic.Count <= 0) heuristic.Count = heuristic.TopCombos.Count;
            return heuristic;
        }
        catch (JsonException ex)
        {
            _recLogger?.LogWarning(ex, "PostRecommendationEnvelope: JSON hybrid không đúng contract.");
            return null;
        }
    }

    private static readonly Dictionary<string, (string Name, decimal Price, string Note)> AiServiceCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DV001"] = ("Dịch vụ thức uống trọn gói", 200000m, "Thức uống trọn gói trong 2,5 giờ gồm nước ngọt, bia và nước suối cao cấp suốt tiệc."),
        ["DV002"] = ("Dịch vụ đón khách tại tiền sảnh", 2000000m, "Độc tấu Piano / Violin đón khách sang trọng tại tiền sảnh trong 1 giờ."),
        ["DV003"] = ("Dịch vụ lễ khai tiệc hoàng gia", 1000000m, "MC chuyên nghiệp dẫn lễ, violin rước dâu và nghi thức khai tiệc hoàng gia."),
        ["DV004"] = ("Chương trình giải trí âm nhạc", 800000m, "Chương trình giải trí 2 giờ với ban nhạc hòa tấu Acoustic lãng mạn."),
        ["DV005"] = ("Dịch vụ tiệc cưới đặc biệt", 2000000m, "Tổ chức Lễ Gia Tiên, hỗ trợ thiệp cưới và các dịch vụ trải nghiệm đặc quyền tại Riverside Palace.")
    };

    private ComboSuggestionViewModel MapAiItemToCombo(JsonElement it, int rank, int tableCount)
    {
        var explanations = new List<string>();
        foreach (var name in new[] { "explanation_arguments", "explanationArguments", "Explanations", "explanations" })
        {
            if (it.ValueKind != JsonValueKind.Object) break;
            if (TryGetPropertyIgnoreCase(it, name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                        explanations.Add(e.GetString()!);
                break;
            }
        }
        var services = new List<SuggestedServiceItemViewModel>();
        if (TryGetPropertyIgnoreCase(it, "included_services", out var svcArr) && svcArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in svcArr.EnumerateArray())
            {
                if (s.ValueKind == JsonValueKind.String)
                {
                    var raw = s.GetString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        if (AiServiceCatalog.TryGetValue(raw, out var cat))
                        {
                            services.Add(new SuggestedServiceItemViewModel
                            {
                                ServiceCode = raw,
                                ServiceName = cat.Name,
                                Price = cat.Price,
                                Note = cat.Note
                            });
                        }
                        else
                        {
                            services.Add(new SuggestedServiceItemViewModel
                            {
                                ServiceCode = raw,
                                ServiceName = raw,
                                Price = 0m,
                                Note = "Dịch vụ trọn gói Riverside Palace"
                            });
                        }
                    }
                }
                else if (s.ValueKind == JsonValueKind.Object)
                {
                    var code = GetJsonString(s, "serviceCode", "service_code", "ServiceCode") ?? string.Empty;
                    var name = GetJsonString(s, "serviceName", "service_name", "ServiceName") ?? string.Empty;
                    var price = GetJsonDecimal(s, "price", "Price");
                    var note = GetJsonString(s, "note", "Note") ?? string.Empty;
                    if (AiServiceCatalog.TryGetValue(code, out var cat))
                    {
                        if (string.IsNullOrWhiteSpace(name) || name.Equals(code, StringComparison.OrdinalIgnoreCase))
                            name = cat.Name;
                        if (price <= 0)
                            price = cat.Price;
                        if (string.IsNullOrWhiteSpace(note))
                            note = cat.Note;
                    }
                    services.Add(new SuggestedServiceItemViewModel
                    {
                        ServiceCode = code,
                        ServiceName = string.IsNullOrWhiteSpace(name) ? "Dịch vụ tiệc cưới" : name,
                        Price = price,
                        Note = note
                    });
                }
            }
        }
        var hallMaxGuests = GetJsonInt(it, "hall_max_guests", "capacity_max_guests", "CapacityMaxGuests", "hallCapacity", "HallCapacity");
        var hallName = GetJsonString(it, "hall_name", "HallName") ?? "Sảnh Riverside";
        var rawHallImg = GetJsonString(it, "hall_image_url", "hallImageUrl", "HallImageUrl", "hallImage", "HallImage");
        var hallImageUrl = _projectImages.GetImage("HallCovers", hallName) ?? ResolvePublicImageUrl(rawHallImg);

        var overallScore = GetJsonDouble(it, "matching_score", "matchingScore", "overallMatchScore", "OverallMatchScore");
        var bScore = GetJsonDouble(it, "budget_match_score", "budgetMatchScore", "BudgetMatchScore");
        var spScore = GetJsonDouble(it, "space_fit_score", "spaceFitScore", "SpaceFitScore");
        var stScore = GetJsonDouble(it, "style_theme_score", "styleThemeScore", "StyleThemeScore");
        var rtScore = GetJsonDouble(it, "rating_credibility_score", "ratingCredibilityScore", "RatingCredibilityScore");

        if (bScore <= 0 || spScore <= 0 || stScore <= 0 || rtScore <= 0)
        {
            var baseMatch = overallScore > 0 ? overallScore : 90.0;
            if (bScore <= 0) bScore = Math.Round(Math.Clamp(baseMatch + 2, 85.0, 99.0), 1);
            if (spScore <= 0) spScore = Math.Round(Math.Clamp(baseMatch, 88.0, 98.0), 1);
            if (stScore <= 0) stScore = Math.Round(Math.Clamp(baseMatch - 1, 85.0, 97.0), 1);
            if (rtScore <= 0) rtScore = Math.Round(Math.Clamp((bScore + spScore + stScore) / 3, 90.0, 99.0), 1);
        }

        var combo = new ComboSuggestionViewModel
        {
            Rank = GetJsonInt(it, "rank", "Rank") is var r && r > 0 ? r : rank,
            ComboType = GetJsonString(it, "combo_type", "comboType", "ComboType") ?? "BALANCED",
            ComboTitle = GetJsonString(it, "combo_title", "comboTitle", "ComboTitle", "combo_name", "comboName") ?? string.Empty,
            ComboBadge = GetJsonString(it, "combo_badge", "comboBadge", "ComboBadge") ?? string.Empty,
            OverallMatchScore = overallScore,
            BudgetMatchScore = bScore,
            SpaceFitScore = spScore,
            StyleThemeScore = stScore,
            RatingCredibilityScore = rtScore,
            HallId = GetJsonInt(it, "hall_id", "HallId"),
            HallName = hallName,
            HallImageUrl = hallImageUrl,
            HallCode = GetJsonString(it, "hall_code", "HallCode", "hallCode") ?? string.Empty,
            HallRentalPrice = GetJsonDecimal(it, "hallRentalPrice", "HallRentalPrice", "hall_rental_price", "hall_price", "HallPrice", "hallPrice"),
            HallCapacity = hallMaxGuests,
            CapacityMinGuests = GetJsonInt(it, "hall_min_guests", "capacity_min_guests", "CapacityMinGuests"),
            CapacityMaxGuests = hallMaxGuests,
            CapacityMinTables = GetJsonInt(it, "hall_min_tables", "capacity_min_tables", "CapacityMinTables"),
            CapacityMaxTables = GetJsonInt(it, "hall_max_tables", "capacity_max_tables", "CapacityMaxTables"),
            MenuId = GetJsonInt(it, "menu_id", "MenuId"),
            MenuName = GetJsonString(it, "menu_name", "MenuName") ?? "Thực đơn Riverside",
            MenuPricePerTable = GetJsonDecimal(it, "price_per_table", "PricePerTable", "menuPricePerTable", "MenuPricePerTable"),
            MenuTotalPrice = GetJsonDecimal(it, "total_menu_price", "TotalMenuPrice", "menuTotalPrice"),
            DecorationPackageId = GetJsonInt(it, "decor_id", "DecorationPackageId", "decorationPackageId"),
            DecorationPackageName = GetJsonString(it, "decor_name", "DecorationPackageName", "decorationPackageName") ?? "Decor Riverside",
            DecorationStyle = GetJsonString(it, "decor_style", "DecorationStyle", "decorationStyle") ?? string.Empty,
            DecorationPrice = GetJsonDecimal(it, "decor_price", "DecorationPrice", "decorationPrice"),
            IncludedServices = services,
            TotalEstimatedCost = GetJsonDecimal(it, "total_estimated_cost", "TotalEstimatedCost", "totalEstimatedCost"),
            RecommendationReasons = new List<string>(explanations),
            Explanations = explanations,
            ExplanationArguments = new List<string>(explanations),
        };
        combo.ComboName = combo.ComboTitle;
        combo.PackageName = combo.ComboTitle;
        // VĐ2: MenuTotalPrice = price_per_table * tableCount khi AI thiếu/sai (không gán bằng đơn giá).
        var expectedMenuTotal = combo.MenuPricePerTable > 0 && tableCount > 0
            ? combo.MenuPricePerTable * tableCount
            : 0m;
        if (combo.MenuTotalPrice <= 0 && expectedMenuTotal > 0)
            combo.MenuTotalPrice = expectedMenuTotal;
        else if (expectedMenuTotal > 0 && Math.Abs(combo.MenuTotalPrice - expectedMenuTotal) > 1m)
            combo.MenuTotalPrice = expectedMenuTotal;
        return combo;
    }

    private static bool HasProperty(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(el, name, out _);

    private static bool TryGetPropertyIgnoreCase(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        if (el.ValueKind != JsonValueKind.Object) return false;
        if (el.TryGetProperty(name, out value)) return true;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        return false;
    }

    private static string? GetJsonString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (TryGetPropertyIgnoreCase(el, n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
            }
        return null;
    }

    private static int GetJsonInt(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (TryGetPropertyIgnoreCase(el, n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var dd)) return (int)dd;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
            }
        return 0;
    }

    private static decimal GetJsonDecimal(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (TryGetPropertyIgnoreCase(el, n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var m)) return m;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var dd)) return (decimal)dd;
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var s)) return s;
            }
        return 0m;
    }

    private static double GetJsonDouble(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (TryGetPropertyIgnoreCase(el, n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var dd)) return dd;
                if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var s)) return s;
            }
        return 0;
    }

    private static List<JsonElement> GetJsonArray(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (TryGetPropertyIgnoreCase(el, n, out var v) && v.ValueKind == JsonValueKind.Array)
                return v.EnumerateArray().ToList();
        return new List<JsonElement>();
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

    public async Task<ApiCallResult<PublicBookingRegistrationResultViewModel>> RegisterPublicBookingAsync(
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
                customMenuDishIds = request.CustomMenuDishIds,
                serviceCodes = request.ServiceCodes
            };
            var response = await _httpClient.PostAsJsonAsync("api/recommendations/register-booking", backendPayload, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            PublicBookingRegistrationResultViewModel? registration = null;
            try
            {
                registration = JsonSerializer.Deserialize<PublicBookingRegistrationResultViewModel>(responseJson, JsonOptions);
            }
            catch { }
            if (!response.IsSuccessStatusCode)
            {
                var message = registration?.Message ?? responseJson;
                return new ApiCallResult<PublicBookingRegistrationResultViewModel>(
                    false, registration, message, (int)response.StatusCode);
            }

            registration ??= new PublicBookingRegistrationResultViewModel { BookingCode = "DT-ONLINE" };
            registration.BookingCode ??= "DT-ONLINE";
            return ApiCallResult<PublicBookingRegistrationResultViewModel>.Success(registration);
        }
        catch (Exception ex)
        {
            return ApiCallResult<PublicBookingRegistrationResultViewModel>.Failure($"Lỗi kết nối API: {ex.Message}");
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

    public async Task<ApiCallResult<bool>> CancelBookingAsync(int bookingId, string? bookingCode = null, string? phone = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(bookingCode)) query.Add($"bookingCode={Uri.EscapeDataString(bookingCode.Trim())}");
            if (!string.IsNullOrWhiteSpace(phone)) query.Add($"phone={Uri.EscapeDataString(phone.Trim())}");
            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
            var response = await _httpClient.PutAsync($"api/my-bookings/{bookingId}/cancel{queryString}", null, cancellationToken);
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

    // UC10: POST /api/v1/ai-chat/send (AllowAnonymous) â€” proxy FE -> BE -> AI_Service RAG.
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

    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>> CreateDepositPaymentAsync(
        HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentRequestViewModel request,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/payment/create-deposit")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            if (!string.IsNullOrWhiteSpace(accessToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var dto = JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>(dataEl.GetRawText(), JsonOptions);
                return dto != null
                    ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Success(dto)
                    : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không thể đọc dữ liệu phản hồi thanh toán.");
            }

            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không tìm thấy dữ liệu thanh toán trong phản hồi.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không thể kết nối đến máy chủ thanh toán.");
        }
        catch (Exception ex)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure($"Lỗi khởi tạo thanh toán: {ex.Message}");
        }
    }

        public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>> CreateCustomerContractPaymentAsync(
        HeThongDatTiecCuoi_WEB.Models.Payment.CreateCustomerContractPaymentRequestViewModel request,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/payment/create-contract-payment")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            if (!string.IsNullOrWhiteSpace(accessToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var dto = JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>(dataEl.GetRawText(), JsonOptions);
                return dto != null
                    ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Success(dto)
                    : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không thể đọc dữ liệu phản hồi thanh toán.");
            }

            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không tìm thấy dữ liệu thanh toán trong phản hồi.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure("Không thể kết nối đến máy chủ thanh toán.");
        }
        catch (Exception ex)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.CreateDepositPaymentResponseViewModel>.Failure($"Lỗi khởi tạo thanh toán: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>> CheckPaymentStatusAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"api/v1/payment/check-status/{orderCode}", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Failure(
                    ExtractApiMessage(json, response.StatusCode), (int)response.StatusCode);
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var dto = JsonSerializer.Deserialize<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>(dataEl.GetRawText(), JsonOptions);
                return dto != null
                    ? ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Success(dto)
                    : ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Failure("Không thể đọc dữ liệu trạng thái.");
            }

            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Failure("Không tìm thấy dữ liệu trạng thái.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Failure("Không thể kết nối kiểm tra thanh toán.");
        }
        catch (Exception ex)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.Payment.PaymentStatusResponseViewModel>.Failure($"Lỗi kiểm tra trạng thái: {ex.Message}");
        }
    }

    public async Task<ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>> DownloadInvoicePdfAsync(
        string bookingCode,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/v1/payment/invoice/{Uri.EscapeDataString(bookingCode)}?phone={Uri.EscapeDataString(phoneNumber)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>.Failure(
                    ExtractApiMessage(errorJson, response.StatusCode), (int)response.StatusCode);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? $"HoaDonDienTu_{bookingCode}.pdf";
            fileName = fileName.Trim('"');

            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>.Success(
                new HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel(
                    bytes,
                    fileName,
                    "application/pdf"));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>.Failure("Backend API phản hồi quá lâu.");
        }
        catch (Exception ex)
        {
            return ApiCallResult<HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment.PdfDownloadViewModel>.Failure($"Lỗi tải hóa đơn điện tử: {ex.Message}");
        }
    }

    public Task<ApiCallResult<ScheduleChangeEligibilityViewModel>> GetScheduleChangeEligibilityAsync(
        string bookingCode, string phoneNumber, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangeEligibilityViewModel>(HttpMethod.Post, "api/v1/public/schedule-changes/eligibility",
            new { bookingCode, phoneNumber }, null, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangeAvailabilityViewModel>>> GetScheduleChangeAvailabilityAsync(
        string bookingCode, string phoneNumber, DateTime date, string shift, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<List<ScheduleChangeAvailabilityViewModel>>(HttpMethod.Post, "api/v1/public/schedule-changes/availability",
            new { bookingCode, phoneNumber, date, shift }, null, cancellationToken);

    public async Task<ApiCallResult<string>> SendScheduleChangeOtpAsync(string bookingCode, string phoneNumber,
        string purpose, int? scheduleChangeId, CancellationToken cancellationToken)
    {
        var result = await SendEnvelopeAsync<JsonElement>(HttpMethod.Post, "api/v1/public/schedule-changes/otp/send",
            new { bookingCode, phoneNumber, purpose, scheduleChangeId }, null, cancellationToken);
        return result.Succeeded
            ? ApiCallResult<string>.Success("Mã OTP đã được gửi qua SMS.")
            : ApiCallResult<string>.Failure(result.Error ?? "Không thể gửi OTP.", result.StatusCode);
    }

    public Task<ApiCallResult<OtpVerificationViewModel>> VerifyScheduleChangeOtpAsync(string bookingCode,
        string phoneNumber, string purpose, int? scheduleChangeId, string code, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<OtpVerificationViewModel>(HttpMethod.Post, "api/v1/public/schedule-changes/otp/verify",
            new { bookingCode, phoneNumber, purpose, scheduleChangeId, code }, null, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> CreateScheduleChangeAsync(CreateScheduleChangeInput input,
        CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Post,
        "api/v1/public/schedule-changes", input, null, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangeViewModel>>> LookupScheduleChangesAsync(string bookingCode,
        string phoneNumber, CancellationToken cancellationToken) => SendEnvelopeAsync<List<ScheduleChangeViewModel>>(
        HttpMethod.Post, "api/v1/public/schedule-changes/lookup", new { bookingCode, phoneNumber }, null, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> GetPublicScheduleChangeAsync(int id, string bookingCode,
        string phoneNumber, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(
        HttpMethod.Post, $"api/v1/public/schedule-changes/{id}/detail", new { bookingCode, phoneNumber }, null, cancellationToken);

    public Task<ApiCallResult<QuoteAcceptanceResultViewModel>> AcceptScheduleChangeQuoteAsync(int id,
        string bookingCode, string phoneNumber, int quoteId, string verificationToken, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<QuoteAcceptanceResultViewModel>(HttpMethod.Post,
            $"api/v1/public/schedule-changes/{id}/quote/accept",
            new { bookingCode, phoneNumber, quoteId, verificationToken }, null, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> WithdrawScheduleChangeAsync(int id, string bookingCode,
        string phoneNumber, string reason, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Post,
            $"api/v1/public/schedule-changes/{id}/withdraw", new { bookingCode, phoneNumber, reason }, null, cancellationToken);

    public Task<ApiCallResult<ScheduleChangePaymentViewModel>> GetScheduleChangePaymentStatusAsync(int id,
        string bookingCode, string phoneNumber, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangePaymentViewModel>(HttpMethod.Post,
            $"api/v1/public/schedule-changes/{id}/payment/status", new { bookingCode, phoneNumber }, null, cancellationToken);

    public Task<ApiCallResult<HallManagerScheduleChangeOptionsViewModel>> GetHallManagerScheduleChangeOptionsAsync(string accessToken,
        CancellationToken cancellationToken) => SendEnvelopeAsync<HallManagerScheduleChangeOptionsViewModel>(HttpMethod.Get,
        "api/hall-manager/schedule-changes/create-options", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangeAvailabilityViewModel>>> GetHallManagerScheduleChangeAvailabilityAsync(string accessToken,
        int bookingId, DateTime date, string shift, CancellationToken cancellationToken) => SendEnvelopeAsync<List<ScheduleChangeAvailabilityViewModel>>(HttpMethod.Get,
        $"api/hall-manager/schedule-changes/availability?bookingId={bookingId}&date={date:yyyy-MM-dd}&shift={Uri.EscapeDataString(shift)}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangeViewModel>>> GetHallManagerScheduleChangesAsync(string accessToken,
        string? status, CancellationToken cancellationToken) => SendEnvelopeAsync<List<ScheduleChangeViewModel>>(
        HttpMethod.Get, $"api/hall-manager/schedule-changes{StatusQuery(status)}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> GetHallManagerScheduleChangeAsync(string accessToken, int id,
        CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Get,
        $"api/hall-manager/schedule-changes/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> CreateHallManagerScheduleChangeAsync(string accessToken,
        HallManagerCreateScheduleChangeInput input, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Post, "api/hall-manager/schedule-changes", input,
            accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> ReviewScheduleChangeAsync(string accessToken, int id,
        string reviewResult, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(
        HttpMethod.Post, $"api/hall-manager/schedule-changes/{id}/review", new { reviewResult }, accessToken, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangeViewModel>>> GetManagerScheduleChangesAsync(string accessToken,
        string? status, CancellationToken cancellationToken) => SendEnvelopeAsync<List<ScheduleChangeViewModel>>(
        HttpMethod.Get, $"api/manager/schedule-changes{StatusQuery(status)}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> GetManagerScheduleChangeAsync(string accessToken, int id,
        CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Get,
        $"api/manager/schedule-changes/{id}", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> QuoteScheduleChangeAsync(string accessToken, int id,
        string? note, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Post,
        $"api/manager/schedule-changes/{id}/quote", new { note }, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> RejectScheduleChangeAsync(string accessToken, int id,
        string reason, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(HttpMethod.Post,
        $"api/manager/schedule-changes/{id}/reject", new { reason }, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeViewModel>> ReconcileScheduleChangeAsync(string accessToken, int id,
        string action, string evidence, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeViewModel>(
        HttpMethod.Post, $"api/manager/schedule-changes/{id}/reconcile", new { action, evidence }, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeFinancialViewModel>> GetScheduleChangeFinancialsAsync(string accessToken,
        int id, CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangeFinancialViewModel>(HttpMethod.Get,
        $"api/manager/schedule-changes/{id}/financials", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangeFinancialViewModel>> ConfirmScheduleChangeRefundAsync(string accessToken,
        int id, int refundId, string evidence, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangeFinancialViewModel>(HttpMethod.Post,
            $"api/manager/schedule-changes/{id}/refunds/{refundId}/confirm", new { evidence }, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangePolicyViewModel>> GetActiveScheduleChangePolicyAsync(string accessToken,
        CancellationToken cancellationToken) => SendEnvelopeAsync<ScheduleChangePolicyViewModel>(HttpMethod.Get,
        "api/admin/schedule-change-policy/active", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<ScheduleChangePolicyViewModel>>> GetScheduleChangePolicyHistoryAsync(string accessToken,
        CancellationToken cancellationToken) => SendEnvelopeAsync<List<ScheduleChangePolicyViewModel>>(HttpMethod.Get,
        "api/admin/schedule-change-policy/history", null, accessToken, cancellationToken);

    public Task<ApiCallResult<ScheduleChangePolicyViewModel>> CreateScheduleChangePolicyVersionAsync(string accessToken,
        ScheduleChangePolicyVersionInput input, CancellationToken cancellationToken) =>
        SendEnvelopeAsync<ScheduleChangePolicyViewModel>(HttpMethod.Post, "api/admin/schedule-change-policy/versions",
            input, accessToken, cancellationToken);

    private static string StatusQuery(string? status) => string.IsNullOrWhiteSpace(status)
        ? string.Empty : $"?status={Uri.EscapeDataString(status.Trim())}";

    private async Task<ApiCallResult<T>> SendEnvelopeAsync<T>(HttpMethod method, string uri, object? body,
        string? accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var envelope = JsonSerializer.Deserialize<ScheduleChangeEnvelope<T>>(json, JsonOptions);
                return envelope is not null
                    ? ApiCallResult<T>.Success(envelope.Data)
                    : ApiCallResult<T>.Failure("API trả về dữ liệu rỗng.", (int)response.StatusCode);
            }
            var error = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ApiErrorDto>(json, JsonOptions);
            return ApiCallResult<T>.Failure(error?.Message ?? $"Yêu cầu không thành công ({(int)response.StatusCode}).",
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
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure("Phản hồi từ Backend không đúng định dạng mong đợi.");
        }
    }

    private sealed class ScheduleChangeEnvelope<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; } = default!;
    }
}

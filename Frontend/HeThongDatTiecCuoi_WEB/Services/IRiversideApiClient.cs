using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Halls;
using HeThongDatTiecCuoi_WEB.Models.Menus;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
using HeThongDatTiecCuoi_WEB.Models.AdminReport;
using HeThongDatTiecCuoi_WEB.Models.AdminBooking;
using HeThongDatTiecCuoi_WEB.Models.Recommendation;
using Microsoft.AspNetCore.Mvc;

public interface IRiversideApiClient
{
    // Auth
    Task<ApiCallResult<AuthResponseDto>> LoginAsync(
        LoginViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AuthResponseDto>> LoginWithGoogleAsync(
        string accessToken,
        bool rememberMe,
        CancellationToken cancellationToken);
    Task<ApiCallResult<MessageResponseDto>> ChangePasswordAsync(ChangePasswordViewModel model, string accessToken, CancellationToken cancellationToken);

    Task<ApiCallResult<MessageResponseDto>> ForgotPasswordAsync(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MessageResponseDto>> ResetPasswordAsync(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken);


    // Hall management
    Task<ApiCallResult<List<HallDto>>> GetFeaturedHallsAsync(
        CancellationToken cancellationToken);

    Task<ApiCallResult<PublicHallListResponse>> GetPublicHallsAsync(
        string? keyword,
        string? capacity,
        string? priceRange,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PublicHallDetailDto>> GetPublicHallAsync(
        int hallId,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PublicMenuListResponse>> GetPublicMenusAsync(
        string? keyword,
        string? priceRange,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PublicMenuDetailDto>> GetPublicMenuDetailAsync(
        int menuId,
        CancellationToken cancellationToken);

    Task<ApiCallResult<List<HallDto>>> GetHallsAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> GetHallByIdAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> CreateHallAsync(
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> UpdateHallAsync(
        int hallId,
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateHallStatusAsync(
        int hallId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> DeleteHallAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken);


    // Hall schedules
    Task<ApiCallResult<WeeklyHallScheduleDto>> GetWeeklyHallSchedulesAsync(
        DateTime startDate,
        int? hallId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateHallScheduleStatusAsync(
        int hallScheduleId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<List<AccountDto>>> GetAccountsAsync(
        string accessToken,
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken);

    Task<ApiCallResult<List<RoleDto>>> GetRolesAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> CreateEmployeeAccountAsync(
        CreateEmployeeAccountRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateEmployeeAccountAsync(
        int userId,
        UpdateEmployeeAccountRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateAccountStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> SendPasswordResetLinkAsync(
        int userId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateEmployeeStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);
    // THỰC ĐƠN
    Task<ApiCallResult<List<MenuViewModel>>> GetMenusAsync(
        string? keyword,
        string? status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuViewModel>> CreateMenuAsync(
        CreateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuViewModel>> UpdateMenuAsync(
        int menuId,
        UpdateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UpdateMenuStatusAsync(
        int menuId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    // MÓN ĂN
    Task<ApiCallResult<List<DishViewModel>>> GetDishesAsync(
        string? keyword,
        string? category,
        string? status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DishViewModel>> CreateDishAsync(
        CreateDishForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DishViewModel>> UpdateDishAsync(
        int dishId,
        UpdateDishForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UpdateDishStatusAsync(
        int dishId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UploadDishImageAsync(
        int dishId,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> DeleteDishImageAsync(
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    // CHI TIẾT THỰC ĐƠN
    Task<ApiCallResult<List<MenuDishViewModel>>> GetMenuDishesAsync(
        int menuId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuDishViewModel>> AddDishToMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> RemoveDishFromMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> ReorderMenuDishesAsync(
        int menuId,
        List<int> dishIds,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<RevenueBiViewModel>> GetRevenueBiReportAsync(
        int year,
        int quarter,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<HallScheduleMatrixDto>> GetHallScheduleMatrixAsync(
        DateTime? startDate,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<BookingListResponseViewModel>> GetBookingsAsync(
    BookingFilterRequestViewModel filter,
    string? accessToken,
    CancellationToken cancellationToken = default);

    Task<ApiCallResult<BookingDetailViewModel>> GetBookingDetailAsync(
        int id,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<dynamic>> CreateBookingAsync(
        CreateBookingRequestViewModel request,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<dynamic>> UpdateBookingStatusAsync(
        int id,
        UpdateBookingStatusRequestViewModel request,
        string? accessToken,
        CancellationToken cancellationToken = default);

    // Thay thế đoạn cuối bằng:
    Task<ApiCallResult<RecommendationResponseViewModel>> GetTop3RecommendationsAsync(
        RecommendationRequestViewModel request,
        CancellationToken cancellationToken = default);


    Task<ApiCallResult<dynamic>> GetHallAvailabilityRealtimeAsync(DateTime date, CancellationToken cancellationToken = default);
   
    
    Task<ApiCallResult<HallAvailabilityResponseViewModel>> GetHallAvailabilityRealtimeAsync(string dateStr, CancellationToken cancellationToken = default);

    Task<ApiCallResult<string>> RegisterPublicBookingAsync(
    PublicBookingRequestViewModel request,
    CancellationToken cancellationToken = default);

    Task<ApiCallResult<List<HeThongDatTiecCuoi_WEB.Models.MyBookings.MyBookingViewModel>>> GetMyBookingsAsync(string? phone, int? userId = null, CancellationToken cancellationToken = default);
    Task<ApiCallResult<bool>> CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default);
}

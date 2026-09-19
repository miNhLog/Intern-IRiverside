using HeThongDatTiecCuoi_WEB.Models.AdminSanh;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;
using HeThongDatTiecCuoi_WEB.Models.AdminGoiTrangTriDichVu;
using HeThongDatTiecCuoi_WEB.Models.AdminPricingPolicy;
namespace HeThongDatTiecCuoi_WEB.Services;

public interface IRiversideApiClient
{
    // Auth
    Task<ApiCallResult<AuthResponseDto>> LoginAsync(
        LoginViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AuthResponseDto>> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken);


    // Sảnh tiệc
    Task<ApiCallResult<List<SanhTiecDto>>> GetDanhSachSanhAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> GetSanhByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> CreateSanhAsync(
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> UpdateSanhAsync(
        int id,
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiSanhAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> DeleteSanhAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);


    // Lịch sảnh
    Task<ApiCallResult<LichSanhTuanDto>> GetLichSanhTheoTuanAsync(
        DateTime ngayBatDau,
        int? sanhTiecId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiLichAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<List<TaiKhoanDto>>> GetDanhSachTaiKhoanAsync(
    string accessToken,
    string? tuKhoa,
    int? vaiTroId,
    string? trangThai,
    CancellationToken cancellationToken);

    Task<ApiCallResult<List<VaiTroDto>>> GetDanhSachVaiTroAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> TaoTaiKhoanNhanVienAsync(
        TaoTaiKhoanNhanVienRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTaiKhoanNhanVienAsync(
        int id,
        CapNhatTaiKhoanNhanVienRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTrangThaiTaiKhoanAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> DatLaiMatKhauAsync(
        int id,
        DatLaiMatKhauRequest model,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTrangThaiNhanVienAsync(
    int nguoiDungId,
    string trangThai,
    string accessToken,
    CancellationToken cancellationToken);

    // Gói trang trí
    Task<ApiCallResult<List<GoiTrangTriDto>>> GetDanhSachGoiTrangTriAsync(
        string accessToken,
        string? tuKhoa,
        string? phongCach,
        string? trangThai,
        CancellationToken cancellationToken);

    Task<ApiCallResult<GoiTrangTriDto>> GetGoiTrangTriByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<GoiTrangTriDto>> CreateGoiTrangTriAsync(
        GoiTrangTriFormModel model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<GoiTrangTriDto>> UpdateGoiTrangTriAsync(
        int id,
        GoiTrangTriFormModel model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<GoiTrangTriDto>> UpdateTrangThaiGoiTrangTriAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    // Dịch vụ
    Task<ApiCallResult<List<DichVuDto>>> GetDanhSachDichVuAsync(
        string accessToken,
        string? tuKhoa,
        string? loaiDichVu,
        string? trangThai,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DichVuDto>> GetDichVuByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DichVuDto>> CreateDichVuAsync(
        DichVuFormModel model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DichVuDto>> UpdateDichVuAsync(
        int id,
        DichVuFormModel model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DichVuDto>> UpdateTrangThaiDichVuAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    // Bảng giá & chính sách
    Task<ApiCallResult<PricingResponseDto>> GetPricingAsync(
        string? category,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PricingItemDto>> UpdatePriceAsync(
        string category,
        int id,
        decimal price,
        decimal expectedPrice,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PolicyResponseDto>> GetPoliciesAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<PolicyResponseDto>> UpdatePoliciesAsync(
        UpdatePolicyRequestDto request,
        string accessToken,
        CancellationToken cancellationToken);
}

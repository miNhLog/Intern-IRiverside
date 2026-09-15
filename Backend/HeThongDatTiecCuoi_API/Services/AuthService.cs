using System.Text.RegularExpressions;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<NguoiDung> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<NguoiDung> passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalizePhone(request.SoDienThoai);

        if (!StrongPasswordRegex().IsMatch(request.MatKhau))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Mật khẩu phải có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.",
                StatusCodes.Status400BadRequest);
        }

        if (await _db.NguoiDung.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Email đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        if (await _db.KhachHang.AnyAsync(x => x.SoDienThoai == phone, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var customerRole = await _db.VaiTro.SingleOrDefaultAsync(
            x => x.TenVaiTro == RoleNames.Customer,
            cancellationToken);

        if (customerRole is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Hệ thống chưa có vai trò Khách hàng. Vui lòng chạy script khởi tạo dữ liệu.",
                StatusCodes.Status500InternalServerError);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = new NguoiDung
            {
                VaiTroID = customerRole.VaiTroID,
                VaiTro = customerRole,
                Email = email,
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
            };
            user.MatKhauHash = _passwordHasher.HashPassword(user, request.MatKhau);

            var customer = new KhachHang
            {
                NguoiDung = user,
                HoTen = request.HoTen.Trim(),
                SoDienThoai = phone
            };

            _db.NguoiDung.Add(user);
            _db.KhachHang.Add(customer);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            user.KhachHang = customer;
            var token = _jwtTokenService.CreateAccessToken(user, rememberMe: false);
            return ServiceResult<AuthResponse>.Success(
                new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)),
                StatusCodes.Status201Created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<AuthResponse>.Failure(
                "Email hoặc số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.DinhDanh.Trim();
        var isStaffLogin = request.LoaiTaiKhoan == "Staff";

        IQueryable<NguoiDung> query = _db.NguoiDung
            .Include(x => x.VaiTro)
            .Include(x => x.KhachHang)
            .Include(x => x.NhanVien);

        NguoiDung? user;
        if (identifier.Contains('@'))
        {
            var email = identifier.ToLowerInvariant();
            user = await query.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        }
        else
        {
            var phone = NormalizePhone(identifier);
            user = isStaffLogin
                ? await query.SingleOrDefaultAsync(x => x.NhanVien != null && x.NhanVien.SoDienThoai == phone, cancellationToken)
                : await query.SingleOrDefaultAsync(x => x.KhachHang != null && x.KhachHang.SoDienThoai == phone, cancellationToken);
        }

        if (user is null || !MatchesSelectedAccountType(user, isStaffLogin))
        {
            return InvalidCredentials();
        }

        if (user.TrangThai != "Hoạt động")
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản đang bị khóa hoặc đã ngừng hoạt động.",
                StatusCodes.Status403Forbidden);
        }

        if (isStaffLogin && user.NhanVien is not null && user.NhanVien.TrangThai != "Đang làm việc")
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản nhân viên hiện không được phép đăng nhập.",
                StatusCodes.Status403Forbidden);
        }


        PasswordVerificationResult verification;
        try
        {
            verification = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, request.MatKhau);
        }
        catch (FormatException)
        {
            return InvalidCredentials();
        }
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.MatKhauHash = _passwordHasher.HashPassword(user, request.MatKhau);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.CreateAccessToken(user, request.GhiNhoDangNhap);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)));
    }

    public async Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.NguoiDung
            .AsNoTracking()
            .Include(x => x.VaiTro)
            .Include(x => x.KhachHang)
            .Include(x => x.NhanVien)
            .SingleOrDefaultAsync(x => x.NguoiDungID == userId, cancellationToken);

        return user is null
            ? ServiceResult<CurrentUserResponse>.Failure("Không tìm thấy người dùng.", StatusCodes.Status404NotFound)
            : ServiceResult<CurrentUserResponse>.Success(ToCurrentUser(user));
    }

    private static bool MatchesSelectedAccountType(
    NguoiDung user,
    bool isStaffLogin) =>
    isStaffLogin
        ? user.VaiTro.TenVaiTro is
            RoleNames.Admin or
            RoleNames.Staff or
            RoleNames.Coordinator
        : user.VaiTro.TenVaiTro == RoleNames.Customer;

    private static CurrentUserResponse ToCurrentUser(NguoiDung user) => new(
        user.NguoiDungID,
        user.Email,
        user.KhachHang?.HoTen ?? user.NhanVien?.HoTen ?? user.Email,
        user.KhachHang?.SoDienThoai ?? user.NhanVien?.SoDienThoai,
        user.VaiTro.TenVaiTro,
        user.TrangThai);

    private static ServiceResult<AuthResponse> InvalidCredentials() =>
        ServiceResult<AuthResponse>.Failure(
            "Thông tin đăng nhập không chính xác.",
            StatusCodes.Status401Unauthorized);

    private static string NormalizePhone(string value)
    {
        var digits = NonDigitRegex().Replace(value, string.Empty);
        return digits.StartsWith("84") && digits.Length == 11
            ? $"0{digits[2..]}"
            : digits;
    }

    [GeneratedRegex(@"[^0-9]")]
    private static partial Regex NonDigitRegex();

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$")]
    private static partial Regex StrongPasswordRegex();
}

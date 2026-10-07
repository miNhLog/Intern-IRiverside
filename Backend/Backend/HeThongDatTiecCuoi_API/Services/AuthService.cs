using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed partial class AuthService : IAuthService
{
    public async Task<ServiceResult<object>> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword || !PasswordPolicy.IsValid(request.NewPassword))
            return ServiceResult<object>.Failure("Mật khẩu mới không hợp lệ hoặc xác nhận không khớp.", StatusCodes.Status400BadRequest);
        var user = await _db.Users.Include(x => x.Role).Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return ServiceResult<object>.Failure("Mật khẩu hiện tại không chính xác.", StatusCodes.Status400BadRequest);
        if (request.NewPassword == _accountProvisioningOptions.DefaultStaffPassword)
            return ServiceResult<object>.Failure("Mật khẩu mới không được trùng với mật khẩu mặc định.", StatusCodes.Status400BadRequest);
        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.NewPassword) != PasswordVerificationResult.Failed)
            return ServiceResult<object>.Failure("Mật khẩu mới phải khác mật khẩu hiện tại.", StatusCodes.Status400BadRequest);
        var wasFirstPasswordChange = user.MustChangePassword;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.Now;
        if (user.Role.RoleName == RoleNames.Admin)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId, Action = AuditActions.AdminPasswordChanged,
                EntityName = AuditEntityNames.User, EntityId = user.UserId,
                NewData = JsonSerializer.Serialize(new { PasswordChanged = true }),
                Timestamp = DateTime.Now, Notes = "Quản trị viên đã thay đổi mật khẩu tài khoản."
            });
        }
        else if (wasFirstPasswordChange)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId, Action = AuditActions.FirstPasswordChanged,
                EntityName = AuditEntityNames.User, EntityId = user.UserId,
                OldData = JsonSerializer.Serialize(new { MustChangePassword = true }),
                NewData = JsonSerializer.Serialize(new { MustChangePassword = false }),
                Timestamp = DateTime.Now,
                Notes = $"Nhân viên {user.Employee?.FullName ?? user.Email} đã hoàn tất đổi mật khẩu lần đầu."
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (wasFirstPasswordChange && user.Role.RoleName != RoleNames.Admin)
        {
            try
            {
                var adminUserId = await _notifications.FindActiveAdminUserIdAsync(cancellationToken);
                if (adminUserId.HasValue && await _notifications.AddAsync(adminUserId.Value, user.UserId,
                    NotificationTypeCodes.FirstPasswordChanged, "Nhân viên đã đổi mật khẩu",
                    $"{user.Employee?.EmployeeCode} - {user.Employee?.FullName ?? user.Email} đã hoàn tất đổi mật khẩu lần đầu.",
                    AuditEntityNames.User, user.UserId, cancellationToken))
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                else if (!adminUserId.HasValue)
                {
                    _logger.LogWarning(
                        "Không tìm thấy tài khoản Admin đang hoạt động để nhận thông báo đổi mật khẩu lần đầu của UserId {UserId}.",
                        user.UserId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Không thể tạo thông báo đổi mật khẩu lần đầu cho UserId {UserId}.", user.UserId);
            }
        }
        return ServiceResult<object>.Success(new { message = "Đổi mật khẩu thành công." });
    }
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IStatusService _statusService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INotificationService _notifications;
    private readonly ILogger<AuthService> _logger;
    private readonly AccountProvisioningOptions _accountProvisioningOptions;

    public AuthService(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        IStatusService statusService,
        IHttpClientFactory httpClientFactory,
        INotificationService notifications,
        IOptions<AccountProvisioningOptions> accountProvisioningOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _statusService = statusService;
        _httpClientFactory = httpClientFactory;
        _notifications = notifications;
        _accountProvisioningOptions = accountProvisioningOptions.Value;
        _logger = logger;
    }

 #if false
    public async Task<ServiceResult<AuthResponse>> RegisterRemovedAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!PasswordPolicy.IsValid(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Mật khẩu phải có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.",
                StatusCodes.Status400BadRequest);
        }

        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Email đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var phoneExists =
            await _db.Customers.AnyAsync(
                customer => customer.PhoneNumber == phone,
                cancellationToken) ||
            await _db.Employees.AnyAsync(
                employee => employee.PhoneNumber == phone,
                cancellationToken);

        if (phoneExists)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Số điện thoại đã được sử dụng.",
                StatusCodes.Status409Conflict);
        }

        var customerRole = await _db.Roles.SingleOrDefaultAsync(
            x => x.RoleName == RoleNames.Customer,
            cancellationToken);

        if (customerRole is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Hệ thống chưa có vai trò Khách hàng. Vui lòng chạy script khởi tạo dữ liệu.",
                StatusCodes.Status500InternalServerError);
        }

        var existingDataStatus = await _db.DataStatuses.SingleOrDefaultAsync(
            x => x.DataStatusCode == DataStatusCodes.Existing,
            cancellationToken);

        if (existingDataStatus is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Hệ thống chưa có trạng thái dữ liệu EXISTING. Vui lòng chạy script khởi tạo dữ liệu.",
                StatusCodes.Status500InternalServerError);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountStatusId = await _statusService.GetStatusIdAsync(
                StatusGroups.Account,
                AccountStatusCodes.Active,
                cancellationToken);

            var user = new User
            {
                RoleId = customerRole.RoleId,
                Role = customerRole,
                Email = email,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            var customer = new Customer
            {
                User = user,
                CustomerCode = CreateCustomerCode(),
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                Email = email,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };

            _db.Users.Add(user);
            _db.Customers.Add(customer);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Load Status navigation sau khi User đã được lưu
            await _db.Entry(user)
                .Reference(x => x.Status)
                .LoadAsync(cancellationToken);

            user.Customer = customer;

            var token = _jwtTokenService.CreateAccessToken(
                user,
                rememberMe: false);

            return ServiceResult<AuthResponse>.Success(
                new AuthResponse(
                    token.Token,
                    token.ExpiresAtUtc,
                    ToCurrentUser(user)),
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

 #endif
    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        IQueryable<User> query = _db.Users
            .Include(x => x.Role)
            .Include(x => x.Status)
            .Include(x => x.DataStatus)
            .Include(x => x.Customer)
            .Include(x => x.Employee)
                .ThenInclude(x => x!.DataStatus);

        User? user;
        if (identifier.Contains('@'))
        {
            var email = identifier.ToLowerInvariant();
            user = await query.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        }
        else
        {
            var phone = PhoneNumberHelper.Normalize(identifier);
            user = await query.SingleOrDefaultAsync(
                x => (x.Employee != null && x.Employee.PhoneNumber == phone) ||
                     (x.Customer != null && x.Customer.PhoneNumber == phone),
                cancellationToken);
        }

        if (user is null)
        {
            return InvalidCredentials();
        }

        if (user.Role.RoleName == RoleNames.Customer)
        {
            return InvalidCredentials();
        }

        if (user.Status.StatusCode != AccountStatusCodes.Active)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản đang bị khóa hoặc đã ngừng hoạt động.",
                StatusCodes.Status403Forbidden);
        }

        if (user.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
            (user.Employee is not null &&
             user.Employee.DataStatus.DataStatusCode != DataStatusCodes.Existing) ||
            (user.Customer is not null &&
             user.Customer.DataStatusId != user.DataStatusId))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản hoặc hồ sơ hiện không được phép đăng nhập.",
                StatusCodes.Status403Forbidden);
        }


        PasswordVerificationResult verification;
        try
        {
            verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
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
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.CreateAccessToken(user, request.RememberMe);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)));
    }

    public async Task<ServiceResult<AuthResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        GoogleUserInfo? userInfo;
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var googleRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.googleapis.com/oauth2/v3/userinfo");
            googleRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", request.AccessToken);

            using var googleResponse = await client.SendAsync(googleRequest, cancellationToken);
            if (!googleResponse.IsSuccessStatusCode)
                return InvalidCredentials();

            userInfo = await googleResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return InvalidCredentials();
        }

        if (userInfo is null || userInfo.EmailVerified is not true ||
            string.IsNullOrWhiteSpace(userInfo.Email))
            return InvalidCredentials();

        var email = userInfo.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.Role).Include(x => x.Status).Include(x => x.DataStatus)
            .Include(x => x.Customer)
            .Include(x => x.Employee).ThenInclude(x => x!.DataStatus)
            .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
            return ServiceResult<AuthResponse>.Failure(
                "Email Google chưa được cấp tài khoản nhân viên Riverside Palace.",
                StatusCodes.Status404NotFound);

        if (user.Role.RoleName == RoleNames.Customer)
            return InvalidCredentials();

        if (user.Status.StatusCode != AccountStatusCodes.Active ||
            user.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
            (user.Employee is not null && user.Employee.DataStatus.DataStatusCode != DataStatusCodes.Existing) ||
            (user.Customer is not null && user.Customer.DataStatusId != user.DataStatusId))
            return ServiceResult<AuthResponse>.Failure(
                "Tài khoản hoặc hồ sơ hiện không được phép đăng nhập.",
                StatusCodes.Status403Forbidden);

        var token = _jwtTokenService.CreateAccessToken(user, request.RememberMe);
        return ServiceResult<AuthResponse>.Success(
            new AuthResponse(token.Token, token.ExpiresAtUtc, ToCurrentUser(user)));
    }

    public async Task<ServiceResult<CurrentUserResponse>> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Status)
            .Include(x => x.DataStatus)
            .Include(x => x.Customer)
            .Include(x => x.Employee)
                .ThenInclude(x => x!.DataStatus)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return user is null
            ? ServiceResult<CurrentUserResponse>.Failure("Không tìm thấy người dùng.", StatusCodes.Status404NotFound)
            : ServiceResult<CurrentUserResponse>.Success(ToCurrentUser(user));
    }

    private static CurrentUserResponse ToCurrentUser(User user) => new(
        user.UserId,
        user.Email,
        user.Customer?.FullName ?? user.Employee?.FullName ?? user.Email,
        user.Customer?.PhoneNumber ?? user.Employee?.PhoneNumber,
        user.Role.RoleName,
        user.Status.StatusCode,
        user.MustChangePassword);

    private static ServiceResult<AuthResponse> InvalidCredentials() =>
        ServiceResult<AuthResponse>.Failure(
            "Thông tin đăng nhập không chính xác.",
            StatusCodes.Status401Unauthorized);

    private static string CreateCustomerCode() =>
        $"KH{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    private sealed record GoogleUserInfo(
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool? EmailVerified);

}

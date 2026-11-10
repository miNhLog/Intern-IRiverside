using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class DevelopmentAuthSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly DevelopmentAccountsOptions _options;
    private readonly IStatusService _statusService;

    public DevelopmentAuthSeeder(
        ApplicationDbContext db,
        IPasswordHasher<User> hasher,
        IOptions<DevelopmentAccountsOptions> options,
        IStatusService statusService)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        _statusService = statusService;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var roles = await EnsureRolesAsync(cancellationToken);

        var coordinatorEmail = string.IsNullOrWhiteSpace(_options.CoordinatorEmail)
            ? _options.StaffEmail
            : _options.CoordinatorEmail;
        var coordinatorPassword = string.IsNullOrWhiteSpace(_options.CoordinatorPassword)
            ? _options.StaffPassword
            : _options.CoordinatorPassword;

        await UpsertAccountAsync(_options.AdminEmail, _options.AdminPassword, roles[RoleNames.Admin], null, null, null, cancellationToken);
        await UpsertAccountAsync(_options.ManagerEmail, _options.ManagerPassword, roles[RoleNames.Manager], "QL001", "Quản lý", "0900000001", cancellationToken);
        await UpsertAccountAsync(_options.HallManagerEmail, _options.HallManagerPassword, roles[RoleNames.HallManager], "QLS001", "Quản lý sảnh", "0900000002", cancellationToken);
        await UpsertAccountAsync(coordinatorEmail, coordinatorPassword, roles[RoleNames.Coordinator], "DP001", "Nhân viên điều phối", "0912345678", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Role>> EnsureRolesAsync(CancellationToken cancellationToken)
    {
        var required = new Dictionary<byte, string>
        {
            [1] = RoleNames.Admin,
            [2] = RoleNames.Manager,
            [3] = RoleNames.HallManager,
            [4] = RoleNames.Coordinator,
            [5] = RoleNames.Customer
        };
        var roles = await _db.Roles
            .Where(role => required.Values.Contains(role.RoleName))
            .ToDictionaryAsync(role => role.RoleName, cancellationToken);

        foreach (var (roleId, name) in required.Where(item => !roles.ContainsKey(item.Value)))
        {
            var role = new Role { RoleId = roleId, RoleName = name };
            _db.Roles.Add(role);
            roles[name] = role;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task UpsertAccountAsync(
        string email,
        string password,
        Role role,
        string? employeeCode,
        string? fullName,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var accountStatusId = await _statusService.GetStatusIdAsync(
            StatusGroups.Account, AccountStatusCodes.Active, cancellationToken);
        var existingDataStatus = await _db.DataStatuses.SingleAsync(
            status => status.DataStatusCode == DataStatusCodes.Existing,
            cancellationToken);
        var user = await _db.Users
            .Include(x => x.Customer)
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        // The database permits only one active Admin and one active Manager. When a
        // development account email changes, reuse that singleton account instead of
        // trying to insert a second row and violating UX_TaiKhoan_*_Active.
        if (user is null && (role.RoleName == RoleNames.Admin || role.RoleName == RoleNames.Manager))
        {
            user = await _db.Users
                .Include(x => x.Customer)
                .Include(x => x.Employee)
                .SingleOrDefaultAsync(
                    x => x.RoleId == role.RoleId
                        && x.StatusId == accountStatusId
                        && x.DataStatusId == existingDataStatus.DataStatusId,
                    cancellationToken);

            if (user is not null)
            {
                user.Email = normalizedEmail;
            }
        }

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                Role = role,
                RoleId = role.RoleId,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId,
                MustChangePassword = false
            };
            user.PasswordHash = _hasher.HashPassword(user, password);
            _db.Users.Add(user);
        }
        else
        {
            user.Role = role;
            user.RoleId = role.RoleId;
            user.StatusId = accountStatusId;
            user.DataStatusId = existingDataStatus.DataStatusId;
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.Now;
        }

        // Admin không cần hồ sơ NhanVien; 3 role nhân viên còn lại bắt buộc có Employee để login hợp lệ.
        if (role.RoleName != RoleNames.Admin && !string.IsNullOrWhiteSpace(employeeCode) && user.Employee is null)
        {
            var codeTaken = await _db.Employees.AnyAsync(
                e => e.EmployeeCode == employeeCode && e.UserId != user.UserId,
                cancellationToken);
            if (!codeTaken)
            {
                user.Employee = new Employee
                {
                    User = user,
                    EmployeeCode = employeeCode,
                    FullName = fullName ?? role.RoleName,
                    PhoneNumber = phoneNumber ?? "0900000000",
                    DataStatusId = existingDataStatus.DataStatusId
                };
            }
        }

    }
}

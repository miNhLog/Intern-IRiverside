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
        await UpsertAccountAsync(_options.AdminEmail, _options.AdminPassword, roles[RoleNames.Admin], cancellationToken);
        await UpsertAccountAsync(_options.StaffEmail, _options.StaffPassword, roles[RoleNames.Coordinator], cancellationToken);
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

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                Role = role,
                RoleId = role.RoleId,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Role = role;
            user.RoleId = role.RoleId;
            user.StatusId = accountStatusId;
            user.DataStatusId = existingDataStatus.DataStatusId;
        }

        user.PasswordHash = _hasher.HashPassword(user, password);

        if (role.RoleName == RoleNames.Coordinator && user.Employee is null)
        {
            user.Employee = new Employee
            {
                User = user,
                EmployeeCode = "DP001",
                FullName = "Nhân viên điều phối",
                PhoneNumber = "0912345678",
                DataStatusId = existingDataStatus.DataStatusId
            };
        }

    }
}

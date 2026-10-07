using System.Security.Claims;
using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AdminAccount;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Services;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IStatusService _statusService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IEmployeeCodeGenerator _employeeCodeGenerator;
    private readonly ILogger<AdminAccountController> _logger;
    private readonly string _defaultStaffPassword;

    public AdminAccountController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IStatusService statusService,
        IPasswordResetService passwordResetService,
        IEmployeeCodeGenerator employeeCodeGenerator,
        IOptions<AccountProvisioningOptions> accountProvisioningOptions,
        ILogger<AdminAccountController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _statusService = statusService;
        _passwordResetService = passwordResetService;
        _employeeCodeGenerator = employeeCodeGenerator;
        _defaultStaffPassword = accountProvisioningOptions.Value.DefaultStaffPassword;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts(
        string? keyword,
        int? roleId,
        string? status,
        bool? mustChangePassword,
        CancellationToken cancellationToken)
    {
        var query =
            from user in _context.Users.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on user.RoleId equals role.RoleId
            join employeeRecord in _context.Employees.AsNoTracking()
                on user.UserId equals employeeRecord.UserId
                into employeeGroup
            from employee in employeeGroup.DefaultIfEmpty()
            join customerRecord in _context.Customers.AsNoTracking()
                on user.UserId equals customerRecord.UserId
                into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            select new
            {
                user,
                role,
                employee,
                customer
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            var accountCodeText = keyword.TrimStart('#');
            var hasAccountId = int.TryParse(accountCodeText, out var accountId);

            query = query.Where(item =>
                (hasAccountId && item.user.UserId == accountId) ||
                item.user.Email.Contains(keyword) ||
                (item.employee != null &&
                 ((item.employee.FullName ?? "").Contains(keyword) ||
                  (item.employee.EmployeeCode ?? "").Contains(keyword) ||
                  (item.employee.PhoneNumber ?? "").Contains(keyword))) ||
                (item.customer != null &&
                 ((item.customer.FullName ?? "").Contains(keyword) ||
                  (item.customer.PhoneNumber ?? "").Contains(keyword))));
        }

        if (roleId.HasValue)
        {
            query = query.Where(item => item.user.RoleId == roleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.user.Status.StatusCode == status);
        }

        if (mustChangePassword.HasValue)
        {
            query = query.Where(item => item.user.MustChangePassword == mustChangePassword.Value);
        }

        var accounts = await query
            .OrderByDescending(item => item.user.CreatedAt)
            .Select(item => new AccountDto
            {
                UserId = item.user.UserId,
                Email = item.user.Email,
                RoleId = item.user.RoleId,
                RoleName = item.role.RoleName,
                FullName = item.employee != null
                    ? item.employee.FullName
                    : item.customer != null
                        ? item.customer.FullName
                        : null,
                EmployeeCode = item.employee != null
                    ? item.employee.EmployeeCode
                    : null,
                PhoneNumber = item.employee != null
                    ? item.employee.PhoneNumber
                    : item.customer != null
                        ? item.customer.PhoneNumber
                        : null,
                Status = item.user.Status.StatusCode,
                StatusName = item.user.Status.StatusName,
                CreatedAt = item.user.CreatedAt,
                DataStatus = item.user.DataStatus.DataStatusCode,
                DataStatusName = item.user.DataStatus.DataStatusName,
                EmployeeDataStatus = item.employee != null
                    ? item.employee.DataStatus.DataStatusCode
                    : null,
                EmployeeDataStatusName = item.employee != null
                    ? item.employee.DataStatus.DataStatusName
                    : null,
                MustChangePassword = item.user.MustChangePassword
            })
            .ToListAsync(cancellationToken);

        return Ok(accounts);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<SystemDashboardDto>> GetDashboard(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue != toDate.HasValue)
        {
            return BadRequest(new { message = "Vui lòng cung cấp đầy đủ ngày bắt đầu và ngày kết thúc." });
        }

        var today = DateTime.Today;
        var rangeStart = fromDate?.Date ?? new DateTime(today.Year, today.Month, 1);
        var rangeEnd = toDate?.Date ?? rangeStart.AddMonths(1).AddDays(-1);

        if (rangeEnd < rangeStart)
        {
            return BadRequest(new { message = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu." });
        }

        var rangeEndExclusive = rangeEnd.AddDays(1);
        var internalRoleNames = new[]
        {
            RoleNames.Admin,
            RoleNames.Manager,
            RoleNames.HallManager,
            RoleNames.Coordinator
        };

        var totalAccounts = await _context.Users.CountAsync(cancellationToken);
        var activeAccounts = await _context.Users.CountAsync(
            user => user.Status.StatusCode == AccountStatusCodes.Active,
            cancellationToken);
        var lockedAccounts = await _context.Users.CountAsync(
            user => user.Status.StatusCode == AccountStatusCodes.Locked,
            cancellationToken);

        var internalEmployees =
            from user in _context.Users.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on user.RoleId equals role.RoleId
            join employeeRecord in _context.Employees.AsNoTracking() on user.UserId equals employeeRecord.UserId into employeeGroup
            from employee in employeeGroup.DefaultIfEmpty()
            where internalRoleNames.Contains(role.RoleName)
            select new { employee, user, role };

        var totalInternalEmployees = await internalEmployees.CountAsync(cancellationToken);
        var mustChangePasswordEmployees = await internalEmployees.CountAsync(
            account => account.user.MustChangePassword,
            cancellationToken);

        var roleCounts = await internalEmployees
            .GroupBy(account => account.role.RoleName)
            .Select(group => new { RoleName = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RoleName, item => item.Count, cancellationToken);

        var roleDistribution = internalRoleNames
            .Select(roleName => new RoleDistributionDto
            {
                RoleName = roleName,
                Count = roleCounts.GetValueOrDefault(roleName)
            })
            .ToList();

        var recentInternalAccounts = await internalEmployees
            .Where(account => account.user.CreatedAt >= rangeStart && account.user.CreatedAt < rangeEndExclusive)
            .OrderByDescending(account => account.user.CreatedAt)
            .Take(5)
            .Select(account => new DashboardAccountDto
            {
                UserId = account.user.UserId,
                EmployeeCode = account.employee != null ? account.employee.EmployeeCode : null,
                FullName = account.employee != null ? account.employee.FullName : account.user.Email,
                RoleName = account.role.RoleName,
                CreatedAt = account.user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var attentionAccounts = await internalEmployees
            .Where(account => account.user.Status.StatusCode == AccountStatusCodes.Locked || account.user.MustChangePassword)
            .OrderByDescending(account => account.user.Status.StatusCode == AccountStatusCodes.Locked)
            .ThenByDescending(account => account.user.UpdatedAt ?? account.user.CreatedAt)
            .Take(5)
            .Select(account => new DashboardAttentionAccountDto
            {
                UserId = account.user.UserId,
                EmployeeCode = account.employee != null ? account.employee.EmployeeCode : null,
                FullName = account.employee != null ? account.employee.FullName : account.user.Email,
                RoleName = account.role.RoleName,
                Issue = account.user.Status.StatusCode == AccountStatusCodes.Locked
                    ? "Bị khóa"
                    : "Chờ đổi mật khẩu",
                UpdatedAt = account.user.UpdatedAt,
                CreatedAt = account.user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var recentAuditLogs = await (
            from log in _context.AuditLogs.AsNoTracking()
            join actorUserRecord in _context.Users.AsNoTracking() on log.UserId equals actorUserRecord.UserId into actorUsers
            from actorUser in actorUsers.DefaultIfEmpty()
            join actorEmployeeRecord in _context.Employees.AsNoTracking() on log.UserId equals actorEmployeeRecord.UserId into actorEmployees
            from actorEmployee in actorEmployees.DefaultIfEmpty()
            join targetUserRecord in _context.Users.AsNoTracking() on log.EntityId equals (long)targetUserRecord.UserId into targetUsers
            from targetUser in targetUsers.DefaultIfEmpty()
            join targetEmployeeRecord in _context.Employees.AsNoTracking() on log.EntityId equals (long)targetEmployeeRecord.UserId into targetEmployees
            from targetEmployee in targetEmployees.DefaultIfEmpty()
            where log.EntityName == AuditEntityNames.User &&
                  AuditActions.AccountActions.Contains(log.Action) &&
                  log.Timestamp >= rangeStart && log.Timestamp < rangeEndExclusive
            orderby log.Timestamp descending, log.AuditLogId descending
            select new HeThongDatTiecCuoi_API.DTOs.AuditLog.AuditLogDto
            {
                AuditLogId = log.AuditLogId,
                Timestamp = log.Timestamp,
                ActorUserId = log.UserId,
                Actor = actorEmployee != null
                    ? actorEmployee.FullName + " (" + actorUser!.Email + ")"
                    : actorUser != null ? actorUser.Email : "Hệ thống",
                Action = log.Action,
                TargetUserId = log.EntityId,
                Target = targetEmployee != null
                    ? targetEmployee.EmployeeCode + " - " + targetEmployee.FullName
                    : targetUser != null ? targetUser.Email : "Tài khoản #" + log.EntityId,
                OldData = log.OldData,
                NewData = log.NewData,
                Notes = log.Notes
            })
            .Take(8)
            .ToListAsync(cancellationToken);

        return Ok(new SystemDashboardDto
        {
            TotalAccounts = totalAccounts,
            ActiveAccounts = activeAccounts,
            LockedAccounts = lockedAccounts,
            MustChangePasswordEmployees = mustChangePasswordEmployees,
            TotalInternalEmployees = totalInternalEmployees,
            FromDate = rangeStart,
            ToDate = rangeEnd,
            RoleDistribution = roleDistribution,
            RecentInternalAccounts = recentInternalAccounts,
            AttentionAccounts = attentionAccounts,
            RecentAuditLogs = recentAuditLogs
        });
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .OrderBy(role => role.RoleId)
            .Select(role => new RoleDto
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName
            })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpPatch("{userId:int}/status")]
    public async Task<IActionResult> UpdateAccountStatus(
        int userId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var statusCode = request.Status?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái không được để trống."
            });
        }

        var user = await _context.Users
            .Include(account => account.Status)
            .Include(account => account.Employee)
            .FirstOrDefaultAsync(
                account => account.UserId == userId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        var validStatuses = new[]
        {
            AccountStatusCodes.Active,
            AccountStatusCodes.Locked
        };

        if (!validStatuses.Contains(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái tài khoản không hợp lệ."
            });
        }

        var signedInEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        if (user.Email == signedInEmail && statusCode != AccountStatusCodes.Active)
        {
            return BadRequest(new
            {
                message = "Không thể khóa hoặc ngừng hoạt động tài khoản đang đăng nhập."
            });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.Account, statusCode, cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái tài khoản chưa được cấu hình." });
        }

        var oldStatusCode = user.Status.StatusCode;
        user.StatusId = status.StatusId;
        user.UpdatedAt = DateTime.Now;

        if (!string.Equals(oldStatusCode, statusCode, StringComparison.OrdinalIgnoreCase))
        {
            AddAuditLog(
                statusCode == AccountStatusCodes.Locked ? AuditActions.LockAccount : AuditActions.UnlockAccount,
                user.UserId,
                new { status = oldStatusCode },
                new { status = statusCode },
                $"Admin đã {(statusCode == AccountStatusCodes.Locked ? "khóa" : "mở khóa")} tài khoản {(user.Employee is null ? user.Email : $"{user.Employee.EmployeeCode} - {user.Employee.FullName}")}.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        var message = statusCode switch
        {
            AccountStatusCodes.Active => "Mở khóa tài khoản thành công.",
            AccountStatusCodes.Locked => "Khóa tài khoản thành công.",
            _ => "Cập nhật trạng thái tài khoản thành công."
        };

        return Ok(new
        {
            message,
            userId = user.UserId,
            status = status.StatusCode,
            statusName = status.StatusName
        });
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployeeAccount(
        [FromBody] CreateEmployeeAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phoneNumber = PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists =
                await _context.Employees.AnyAsync(
                    employee => employee.PhoneNumber == phoneNumber,
                    cancellationToken) ||
                await _context.Customers.AnyAsync(
                    customer => customer.PhoneNumber == phoneNumber,
                    cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại đã được sử dụng."
                });
            }
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }

        if (email.Length > 255 || fullName.Length > 150 || phoneNumber.Length > 20)
        {
            return BadRequest(new
            {
                message = "Email, họ tên hoặc số điện thoại vượt quá độ dài cho phép."
            });
        }

        var emailExists = await _context.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email đã được sử dụng."
            });
        }

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existingRole => existingRole.RoleId == request.RoleId,
                cancellationToken);

        if (role is null)
        {
            return NotFound(new
            {
                message = "Vai trò không tồn tại."
            });
        }

        if (role.RoleName != RoleNames.Manager &&
            role.RoleName != RoleNames.HallManager &&
            role.RoleName != RoleNames.Coordinator)
        {
            return BadRequest(new
            {
                message = "Endpoint này chỉ dùng để tạo tài khoản Quản lý, Quản lý sảnh hoặc Nhân viên điều phối."
            });
        }

        if (role.RoleName == RoleNames.Customer)
        {
            return BadRequest(new
            {
                message = "Tài khoản khách hàng phải được tạo qua chức năng đăng ký."
            });
        }

        var employeeCode = await _employeeCodeGenerator.GenerateAsync(role.RoleName, cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var accountStatusId = await _statusService.GetStatusIdAsync(
                StatusGroups.Account, AccountStatusCodes.Active, cancellationToken);
            var existingDataStatus = await _context.DataStatuses.SingleAsync(
                item => item.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);

            var user = new User
            {
                RoleId = request.RoleId,
                Email = email,
                StatusId = accountStatusId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                _defaultStaffPassword);
            user.MustChangePassword = true;

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var employee = new Employee
            {
                UserId = user.UserId,
                EmployeeCode = employeeCode,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                DataStatusId = existingDataStatus.DataStatusId
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(cancellationToken);
            AddAuditLog(
                AuditActions.CreateAccount,
                user.UserId,
                null,
                new { userId = user.UserId, employeeId = employee.EmployeeId, employeeCode, fullName, email = user.Email, roleName = role.RoleName, status = AccountStatusCodes.Active },
                $"Cấp tài khoản cho {employeeCode} - {fullName} với vai trò {role.RoleName}.");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Cấp tài khoản thành công. Nhân viên sẽ được yêu cầu đổi mật khẩu trong lần đăng nhập đầu tiên.",
                userId = user.UserId,
                email = user.Email,
                employeeCode = employee.EmployeeCode,
                fullName = employee.FullName,
                roleName = role.RoleName,
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpPut("employees/{userId:int}")]
    public async Task<IActionResult> UpdateEmployeeAccount(
        int userId,
        [FromBody] UpdateEmployeeAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phoneNumber = PhoneNumberHelper.Normalize(request.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNumberExists =
                await _context.Employees.AnyAsync(
                    employee =>
                        employee.PhoneNumber == phoneNumber &&
                        employee.UserId != userId,
                    cancellationToken) ||
                await _context.Customers.AnyAsync(
                    customer => customer.PhoneNumber == phoneNumber,
                    cancellationToken);

            if (phoneNumberExists)
            {
                return Conflict(new
                {
                    message = "Số điện thoại đã được sử dụng."
                });
            }
        }

        var employeeDataStatus = string.IsNullOrWhiteSpace(request.DataStatus)
            ? null
            : request.DataStatus.Trim().ToUpperInvariant();

        DataStatus? updatedEmployeeDataStatus = null;
        if (employeeDataStatus is not null)
        {
            var validDataStatuses = new[]
            {
                DataStatusCodes.Existing,
                DataStatusCodes.Deleted
            };

            if (!validDataStatuses.Contains(employeeDataStatus))
            {
                return BadRequest(new
                {
                    message = "Trạng thái dữ liệu nhân viên không hợp lệ."
                });
            }
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin bắt buộc."
            });
        }

        if (email.Length > 255)
        {
            return BadRequest(new
            {
                message = "Email không được vượt quá 255 ký tự."
            });
        }

        if (fullName.Length > 150)
        {
            return BadRequest(new
            {
                message = "Họ tên không được vượt quá 150 ký tự."
            });
        }

        if (phoneNumber.Length > 20)
        {
            return BadRequest(new
            {
                message = "Số điện thoại không được vượt quá 20 ký tự."
            });
        }

        var user = await _context.Users
            .Include(account => account.Status)
            .FirstOrDefaultAsync(
                account => account.UserId == userId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tài khoản."
            });
        }

        var employee = await _context.Employees
            .Include(existingEmployee => existingEmployee.DataStatus)
            .FirstOrDefaultAsync(
                existingEmployee => existingEmployee.UserId == userId,
                cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Tài khoản này không phải tài khoản nhân viên."
            });
        }

        var emailExists = await _context.Users.AnyAsync(
            otherUser =>
                otherUser.Email == email &&
                otherUser.UserId != userId,
            cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email đã được sử dụng."
            });
        }

        var currentRoleName = await _context.Roles
            .Where(existingRole => existingRole.RoleId == user.RoleId)
            .Select(existingRole => existingRole.RoleName)
            .SingleAsync(cancellationToken);
        var oldEmail = user.Email;
        var oldFullName = employee.FullName;
        var oldPhoneNumber = employee.PhoneNumber;
        var oldDataStatus = employee.DataStatus.DataStatusCode;

        user.Email = email;
        user.UpdatedAt = DateTime.Now;
        employee.FullName = fullName;
        employee.PhoneNumber = phoneNumber;

        if (employeeDataStatus is not null)
        {
            var resolvedDataStatus = await _context.DataStatuses.SingleOrDefaultAsync(
                item => item.DataStatusCode == employeeDataStatus,
                cancellationToken);
            if (resolvedDataStatus is null)
            {
                return BadRequest(new { message = "Trạng thái dữ liệu chưa được cấu hình." });
            }

            employee.DataStatusId = resolvedDataStatus.DataStatusId;
            updatedEmployeeDataStatus = resolvedDataStatus;
        }

        var newDataStatus = (updatedEmployeeDataStatus ?? employee.DataStatus).DataStatusCode;
        var profileChanged = oldEmail != email || oldFullName != fullName ||
            oldPhoneNumber != phoneNumber || oldDataStatus != newDataStatus;

        if (profileChanged)
        {
            var oldData = new Dictionary<string, object?>();
            var newData = new Dictionary<string, object?>();
            if (!string.Equals(oldEmail, email, StringComparison.OrdinalIgnoreCase)) { oldData["Email"] = oldEmail; newData["Email"] = email; }
            if (oldFullName != fullName) { oldData["FullName"] = oldFullName; newData["FullName"] = fullName; }
            if (oldPhoneNumber != phoneNumber) { oldData["PhoneNumber"] = oldPhoneNumber; newData["PhoneNumber"] = phoneNumber; }
            if (oldDataStatus != newDataStatus) { oldData["DataStatus"] = oldDataStatus; newData["DataStatus"] = newDataStatus; }
            AddAuditLog(
                AuditActions.UpdateAccount,
                user.UserId,
                oldData,
                newData,
                $"Cập nhật thông tin tài khoản {employee.EmployeeCode} - {employee.FullName}.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật tài khoản nhân viên thành công.",
            userId = user.UserId,
            email = user.Email,
            employeeCode = employee.EmployeeCode,
            fullName = employee.FullName,
            phoneNumber = employee.PhoneNumber,
            roleName = currentRoleName,
            accountStatus = user.Status.StatusCode,
            accountStatusName = user.Status.StatusName,
            employeeDataStatus = (updatedEmployeeDataStatus ?? employee.DataStatus).DataStatusCode,
            employeeDataStatusName = (updatedEmployeeDataStatus ?? employee.DataStatus).DataStatusName
        });
    }

    [HttpPut("administrator/{userId:int}")]
    public async Task<IActionResult> UpdateAdministratorAccount(
        int userId,
        [FromBody] UpdateAdministratorAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId) ||
            actorUserId != userId)
        {
            return BadRequest(new { message = "Chỉ có thể chỉnh sửa tài khoản quản trị đang đăng nhập." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255)
        {
            return BadRequest(new { message = "Email quản trị không hợp lệ." });
        }

        var user = await _context.Users
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.UserId == userId, cancellationToken);

        if (user is null || user.Role.RoleName != RoleNames.Admin)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản Quản trị viên." });
        }

        var emailExists = await _context.Users.AnyAsync(
            account => account.UserId != userId && account.Email == email,
            cancellationToken);
        if (emailExists)
        {
            return Conflict(new { message = "Email đã được sử dụng." });
        }

        var oldEmail = user.Email;
        if (!string.Equals(oldEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = email;
            user.UpdatedAt = DateTime.Now;
            AddAuditLog(
                AuditActions.UpdateAccount,
                userId,
                new { email = oldEmail },
                new { email },
                "Cập nhật email tài khoản Quản trị viên hệ thống");
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            message = "Cập nhật thông tin Quản trị viên thành công.",
            userId,
            email = user.Email,
            roleName = user.Role.RoleName
        });
    }

    [HttpPost("{userId:int}/password-reset")]
    public async Task<IActionResult> SendPasswordResetLink(
        int userId,
        CancellationToken cancellationToken)
    {
        ServiceResult<object> result;
        try
        {
            result = await _passwordResetService.SendResetLinkForUserAsync(
                userId, cancellationToken);
        }

        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa thể gửi email đặt lại mật khẩu. Vui lòng kiểm tra cấu hình SMTP."
            });
        }

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { message = result.Error });
        }

        try
        {
            var target = await _context.Employees.AsNoTracking()
                .Where(employee => employee.UserId == userId)
                .Select(employee => new { employee.EmployeeId, employee.EmployeeCode, employee.FullName, employee.User.Email })
                .FirstOrDefaultAsync(cancellationToken);
            AddAuditLog(
                AuditActions.ResetPassword,
                userId,
                null,
                new { UserId = userId, EmployeeId = target == null ? (int?)null : target.EmployeeId, Email = target == null ? null : target.Email, ResetMethod = "EMAIL" },
                $"Quản trị viên đã gửi yêu cầu đặt lại mật khẩu cho {(target == null ? $"tài khoản #{userId}" : $"{target.EmployeeCode} - {target.FullName}")}.");
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Không thể ghi audit log RESET_PASSWORD cho UserId {UserId}.", userId);
        }

        return Ok(new
        {
            message = "Đã gửi liên kết đặt lại mật khẩu đến email của nhân viên.",
            userId
        });
    }

    private void AddAuditLog(string action, int targetUserId, object? oldData, object? newData, string notes)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            throw new InvalidOperationException("Không xác định được Admin đang thực hiện thao tác.");
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityName = AuditEntityNames.User,
            EntityId = targetUserId,
            OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData is null ? null : JsonSerializer.Serialize(newData),
            Timestamp = DateTime.Now,
            Notes = notes
        });
    }

    [HttpPatch("employees/{userId:int}/status")]
    public async Task<IActionResult> UpdateEmployeeStatus(
        int userId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var statusCode = request.Status?.Trim().ToUpperInvariant();
        var validStatuses = new[]
        {
            DataStatusCodes.Existing,
            DataStatusCodes.Deleted
        };

        if (string.IsNullOrWhiteSpace(statusCode) ||
            !validStatuses.Contains(statusCode))
        {
            return BadRequest(new
            {
                message = "Trạng thái dữ liệu nhân viên không hợp lệ."
            });
        }

        var employee = await _context.Employees
            .Include(existingEmployee => existingEmployee.DataStatus)
            .FirstOrDefaultAsync(
                existingEmployee => existingEmployee.UserId == userId,
                cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy nhân viên."
            });
        }

        var status = await _context.DataStatuses.SingleOrDefaultAsync(
            item => item.DataStatusCode == statusCode,
            cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái dữ liệu chưa được cấu hình." });
        }

        var oldStatusCode = employee.DataStatus.DataStatusCode;
        employee.DataStatusId = status.DataStatusId;

        if (!string.Equals(oldStatusCode, status.DataStatusCode, StringComparison.OrdinalIgnoreCase))
        {
            AddAuditLog(
                AuditActions.UpdateAccount,
                userId,
                new { dataStatus = oldStatusCode },
                new { dataStatus = status.DataStatusCode },
                $"Cập nhật trạng thái dữ liệu nhân viên {employee.EmployeeCode} từ {oldStatusCode} sang {status.DataStatusCode}");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật trạng thái dữ liệu nhân viên thành công.",
            userId,
            employeeCode = employee.EmployeeCode,
            employeeDataStatus = status.DataStatusCode,
            employeeDataStatusName = status.DataStatusName
        });
    }
}

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
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Services;

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

    public AdminAccountController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        IStatusService statusService,
        IPasswordResetService passwordResetService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _statusService = statusService;
        _passwordResetService = passwordResetService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts(
        string? keyword,
        int? roleId,
        string? status,
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

            query = query.Where(item =>
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
                    : null
            })
            .ToListAsync(cancellationToken);

        return Ok(accounts);
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

        user.StatusId = status.StatusId;
        user.UpdatedAt = DateTime.Now;

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
            string.IsNullOrWhiteSpace(phoneNumber) ||
            !Regex.IsMatch(request.InitialPassword, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,100}$"))
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập đầy đủ thông tin và mật khẩu ban đầu phải có chữ hoa, chữ thường, số, ký tự đặc biệt, tối thiểu 8 ký tự."
            });
        }

        if (request.InitialPassword != request.ConfirmPassword)
        {
            return BadRequest(new { message = "Mật khẩu ban đầu và xác nhận mật khẩu không khớp." });
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

        var employeeCode = await GenerateEmployeeCodeAsync(role.RoleName, cancellationToken);

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
                request.InitialPassword);
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
            await transaction.CommitAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Đã tạo tài khoản. Admin hãy bàn giao email, mật khẩu ban đầu và yêu cầu nhân viên đổi mật khẩu ngay lần đăng nhập đầu tiên.",
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
                message = "Nhân viên chỉ có thể thuộc vai trò Quản lý, Quản lý sảnh hoặc Nhân viên điều phối."
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

        user.Email = email;
        user.RoleId = request.RoleId;
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

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật tài khoản nhân viên thành công.",
            userId = user.UserId,
            email = user.Email,
            employeeCode = employee.EmployeeCode,
            fullName = employee.FullName,
            phoneNumber = employee.PhoneNumber,
            roleName = role.RoleName,
            accountStatus = user.Status.StatusCode,
            accountStatusName = user.Status.StatusName,
            employeeDataStatus = (updatedEmployeeDataStatus ?? employee.DataStatus).DataStatusCode,
            employeeDataStatusName = (updatedEmployeeDataStatus ?? employee.DataStatus).DataStatusName
        });
    }

    [HttpPost("{userId:int}/password-reset")]
    public async Task<IActionResult> SendPasswordResetLink(
        int userId,
        CancellationToken cancellationToken)
    {
        bool userExists;
        try
        {
            userExists = await _passwordResetService.SendResetLinkForUserAsync(
                userId, cancellationToken);
        }

        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa thể gửi email đặt lại mật khẩu. Vui lòng kiểm tra cấu hình SMTP."
            });
        }

        if (!userExists)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        }

        return Ok(new
        {
            message = "Đã gửi liên kết đặt lại mật khẩu đến email của người dùng.",
            userId
        });
    }

    private async Task<string> GenerateEmployeeCodeAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        var prefix = roleName switch
        {
            RoleNames.Manager => "QL",
            RoleNames.HallManager => "QLS",
            RoleNames.Coordinator => "DP",
            _ => "NV"
        };

        var employeeCodes = await _context.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeCode.StartsWith(prefix))
            .Select(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken);

        var highestNumber = 0;

        foreach (var employeeCode in employeeCodes)
        {
            if (!employeeCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                employeeCode.Length <= prefix.Length)
            {
                continue;
            }

            var numericPart = employeeCode[prefix.Length..];

            if (int.TryParse(numericPart, out var number) &&
                number > highestNumber)
            {
                highestNumber = number;
            }
        }

        return $"{prefix}{highestNumber + 1:D3}";
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

        employee.DataStatusId = status.DataStatusId;

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

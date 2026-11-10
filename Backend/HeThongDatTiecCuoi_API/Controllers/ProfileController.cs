using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Profile;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager + "," + RoleNames.HallManager + "," + RoleNames.Coordinator)]
public sealed class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmployeeCodeGenerator _employeeCodeGenerator;

    public ProfileController(
        ApplicationDbContext context,
        IEmployeeCodeGenerator employeeCodeGenerator)
    {
        _context = context;
        _employeeCodeGenerator = employeeCodeGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var user = await LoadUserAsync(userId, true, cancellationToken);
        return user is null ? NotFound(new { message = "Không tìm thấy tài khoản." }) : Ok(ToResponse(user));
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!EmailAddressHelper.TryNormalize(request.Email, out var email, out var emailError))
            return BadRequest(new { message = emailError });
        var fullName = request.FullName.Trim();
        var phoneNumber = PhoneNumberHelper.Normalize(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest(new { message = "Vui lòng nhập họ và tên." });
        if (phoneNumber.Length != 10 || !phoneNumber.StartsWith('0') || !phoneNumber.All(char.IsDigit))
            return BadRequest(new { message = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0." });

        var duplicatedPhone = await _context.Employees.AnyAsync(
                employee => employee.UserId != userId && employee.PhoneNumber == phoneNumber,
                cancellationToken) ||
            await _context.Customers.AnyAsync(
                customer => customer.UserId != userId && customer.PhoneNumber == phoneNumber,
                cancellationToken);
        if (duplicatedPhone)
            return Conflict(new { message = "Số điện thoại đã được sử dụng." });
        if (await _context.Users.AnyAsync(
                existingUser => existingUser.UserId != userId && existingUser.Email == email,
                cancellationToken))
            return Conflict(new { message = "Email đã được sử dụng." });

        var user = await LoadUserAsync(userId, false, cancellationToken);
        if (user is null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        var oldData = new
        {
            Email = user.Email,
            FullName = user.Employee?.FullName,
            PhoneNumber = user.Employee?.PhoneNumber
        };

        user.Email = email;

        if (user.Employee is null)
        {
            if (user.Role.RoleName != RoleNames.Admin)
                return BadRequest(new { message = "Tài khoản chưa có hồ sơ nhân viên." });

            var existingDataStatus = await _context.DataStatuses.SingleAsync(
                status => status.DataStatusCode == DataStatusCodes.Existing,
                cancellationToken);
            user.Employee = new Employee
            {
                UserId = user.UserId,
                EmployeeCode = await _employeeCodeGenerator.GenerateAsync(RoleNames.Admin, cancellationToken),
                FullName = fullName,
                PhoneNumber = phoneNumber,
                DataStatusId = existingDataStatus.DataStatusId
            };
            _context.Employees.Add(user.Employee);
        }
        else
        {
            user.Employee.FullName = fullName;
            user.Employee.PhoneNumber = phoneNumber;
        }

        var newData = new
        {
            Email = email,
            FullName = fullName,
            PhoneNumber = phoneNumber
        };

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditActions.UpdateAccount,
            EntityName = AuditEntityNames.User,
            EntityId = userId,
            OldData = System.Text.Json.JsonSerializer.Serialize(oldData),
            NewData = System.Text.Json.JsonSerializer.Serialize(newData),
            Timestamp = DateTime.Now,
            Notes = $"Nhân viên {fullName} (UserId: {userId}) đã tự cập nhật hồ sơ cá nhân."
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Cập nhật hồ sơ cá nhân thành công.", profile = ToResponse(user) });
    }

    private Task<User?> LoadUserAsync(int userId, bool noTracking, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _context.Users
            .Include(user => user.Role)
            .Include(user => user.Status)
            .Include(user => user.Employee);
        if (noTracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
    }

    private static ProfileResponse ToResponse(User user) => new(
        user.UserId,
        user.Email,
        user.Role.RoleName,
        user.Status.StatusName,
        user.CreatedAt,
        user.Employee?.EmployeeCode,
        user.Employee?.FullName,
        user.Employee?.PhoneNumber);

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.DTOs.Banner;
using HeThongDatTiecCuoi_API.DTOs.Profile;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Phase2FeatureTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AuthService_Login_With_Phone_Succeeds_And_EmployeeCode_Is_Rejected()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        var role = new Role { RoleId = 2, RoleName = RoleNames.Manager };
        var status = new Status { StatusId = 101, StatusGroup = StatusGroups.Account, StatusCode = AccountStatusCodes.Active, StatusName = "Hoạt động" };

        db.DataStatuses.Add(dataStatus);
        db.Roles.Add(role);
        db.Statuses.Add(status);

        var user = new User
        {
            UserId = 10,
            RoleId = 2,
            Email = "manager@riverside.test",
            StatusId = 101,
            DataStatusId = 1,
            CreatedAt = DateTime.Now
        };
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, "Password123@");

        var employee = new Employee
        {
            EmployeeId = 5,
            UserId = 10,
            EmployeeCode = "QL0001",
            FullName = "Nguyễn Văn Quản Lý",
            PhoneNumber = "0987654321",
            DataStatusId = 1
        };

        db.Users.Add(user);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var opt = Microsoft.Extensions.Options.Options.Create(new AccountProvisioningOptions());

        var authService = new AuthService(
            db,
            hasher,
            new StubJwtTokenService(),
            new StubStatusService(),
            new StubHttpClientFactory(),
            new NotificationService(db),
            new StubFirstPasswordOtpService(),
            opt,
            NullLogger<AuthService>.Instance);

        var phoneResult = await authService.LoginAsync(new LoginRequest
        {
            Identifier = "0987654321",
            Password = "Password123@"
        }, default);

        Assert.True(phoneResult.Succeeded);
        Assert.NotNull(phoneResult.Value);
        Assert.Equal("mock_token", phoneResult.Value.AccessToken);

        var employeeCodeResult = await authService.LoginAsync(new LoginRequest
        {
            Identifier = "ql0001",
            Password = "Password123@"
        }, default);

        Assert.False(employeeCodeResult.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, employeeCodeResult.StatusCode);
    }

    [Fact]
    public async Task ProfileController_Update_Records_AuditLog()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        var role = new Role { RoleId = 2, RoleName = RoleNames.Manager };
        var status = new Status { StatusId = 101, StatusGroup = StatusGroups.Account, StatusCode = AccountStatusCodes.Active, StatusName = "Hoạt động" };

        db.DataStatuses.Add(dataStatus);
        db.Roles.Add(role);
        db.Statuses.Add(status);

        var user = new User
        {
            UserId = 20,
            RoleId = 2,
            Email = "oldemail@riverside.test",
            StatusId = 101,
            DataStatusId = 1,
            CreatedAt = DateTime.Now
        };

        var employee = new Employee
        {
            EmployeeId = 8,
            UserId = 20,
            EmployeeCode = "QL0002",
            FullName = "Trần Quản Lý",
            PhoneNumber = "0911222333",
            DataStatusId = 1
        };

        db.Users.Add(user);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var controller = new ProfileController(db, new StubEmployeeCodeGenerator());

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "20"), new Claim(ClaimTypes.Role, RoleNames.Manager) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var updateReq = new UpdateProfileRequest
        {
            FullName = "Trần Quản Lý Đã Đổi Tên",
            Email = "newemail@riverside.test",
            PhoneNumber = "0999888777"
        };

        var response = await controller.Update(updateReq, default);
        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.NotNull(okResult);

        // Verify AuditLog in DB
        var auditLog = await db.AuditLogs.SingleOrDefaultAsync(x => x.UserId == 20);
        Assert.NotNull(auditLog);
        Assert.Equal(AuditActions.UpdateAccount, auditLog.Action);
        Assert.Equal(AuditEntityNames.User, auditLog.EntityName);
        Assert.Equal(20, auditLog.EntityId);
        Assert.Contains("newemail@riverside.test", auditLog.NewData!);
        Assert.Contains("oldemail@riverside.test", auditLog.OldData!);
    }

    [Fact]
    public async Task BannersController_Create_With_Empty_CTA_Defaults_To_NONE()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var controller = new BannersController(db, new StubWebHostEnvironment());

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, RoleNames.Manager) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var request = new BannerUpsertRequest
        {
            Title = "Banner Khuyến Mãi Mùa Hè",
            Subtitle = "Ưu đãi tiệc cưới",
            Description = "Mô tả chi tiết banner hè",
            NavigationLabel = "Hè Rực Rỡ",
            ImageAlt = "Ảnh banner hè",
            PrimaryActionType = "", // empty string from frontend
            SecondaryActionType = "   ", // whitespace
            SortOrder = 1
        };

        var actionResult = await controller.Create(request, default);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var dto = Assert.IsType<BannerDto>(createdResult.Value);

        Assert.Equal("NONE", dto.PrimaryActionType);
        Assert.Equal("NONE", dto.SecondaryActionType);

        var savedBanner = await db.HomeBanners.SingleAsync(x => x.BannerId == dto.BannerId);
        Assert.Equal("NONE", savedBanner.PrimaryActionType);
        Assert.Equal("NONE", savedBanner.SecondaryActionType);
    }

    private sealed class StubJwtTokenService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, bool rememberMe) =>
            ("mock_token", DateTime.UtcNow.AddHours(1));
    }

    private sealed class StubStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken) =>
            Task.FromResult(101);

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken) =>
            Task.FromResult<Status?>(new Status { StatusId = 101, StatusGroup = statusGroup, StatusCode = statusCode });
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubFirstPasswordOtpService : IFirstPasswordOtpService
    {
        public Task<ServiceResult<object>> ValidateAsync(int userId, string? otpCode, CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<object>.Success(new { }));

        public Task<ServiceResult<object>> SendAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<object>.Success(new { }));
    }

    private sealed class StubEmployeeCodeGenerator : IEmployeeCodeGenerator
    {
        public Task<string> GenerateAsync(string roleName, CancellationToken cancellationToken) =>
            Task.FromResult("AD9999");
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "C:\\test\\wwwroot";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "C:\\test";
        public string EnvironmentName { get; set; } = "Development";
    }
}


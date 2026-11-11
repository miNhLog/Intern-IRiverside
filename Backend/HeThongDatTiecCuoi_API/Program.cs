using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Helpers;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

// Cố định InvariantCulture cho toàn bộ thread để RangeAttribute(typeof(decimal), ...)
// và decimal parsing không phụ thuộc culture Windows (vi-VN dùng dấu phẩy thập phân).
var invariantCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = invariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = invariantCulture;

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(new ApiErrorResponse("Dữ liệu không hợp lệ.", errors));
    };
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<DevelopmentAccountsOptions>(
    builder.Configuration.GetSection(DevelopmentAccountsOptions.SectionName));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<MailSettings>(
    builder.Configuration.GetSection(MailSettings.SectionName));
builder.Services.Configure<PasswordResetOptions>(
    builder.Configuration.GetSection(PasswordResetOptions.SectionName));
builder.Services.Configure<ReviewQrOptions>(
    builder.Configuration.GetSection(ReviewQrOptions.SectionName));
builder.Services
    .AddOptions<AccountProvisioningOptions>()
    .Bind(builder.Configuration.GetSection(AccountProvisioningOptions.SectionName))
    .Validate(options => PasswordPolicy.IsValid(options.DefaultStaffPassword),
        "AccountProvisioning:DefaultStaffPassword bị thiếu hoặc không đáp ứng chính sách mật khẩu.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? throw new InvalidOperationException("Thiếu cấu hình Jwt.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var versionValue = context.Principal?.FindFirst("session_version")?.Value;
                if (!int.TryParse(userIdValue, out var userId) ||
                    !long.TryParse(versionValue, out var tokenVersion))
                {
                    context.Fail("Phiên đăng nhập không hợp lệ.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var updatedAt = await db.Users.AsNoTracking()
                    .Where(user => user.UserId == userId)
                    .Select(user => user.UpdatedAt)
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                var currentVersion = updatedAt?.ToUniversalTime().Ticks ?? 0L;
                if (currentVersion != tokenVersion)
                {
                    context.Fail("Phiên đăng nhập đã bị thu hồi.");
                }
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<FirstPasswordOtp>, PasswordHasher<FirstPasswordOtp>>();
builder.Services.AddScoped<IPasswordHasher<ScheduleChangeOtp>, PasswordHasher<ScheduleChangeOtp>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<BookingStatusBusinessRules>();
builder.Services.AddScoped<CustomMenuPricingService>();
builder.Services.AddSingleton<IPaymentPolicyProvider, PaymentPolicyProvider>();
builder.Services.AddSingleton<IHallPricingPolicyService, HallPricingPolicyService>();
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<PayOSOptions>(builder.Configuration.GetSection("PayOS"));
builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOSOptions>>().Value;
    return new PayOS.PayOSClient(opt.ClientId, opt.ApiKey, opt.ChecksumKey);
});
builder.Services.AddScoped<IPaymentService, PayOSPaymentService>();
builder.Services.AddScoped<ISmsSender, ConfigurableSmsSender>();
builder.Services.AddScoped<IScheduleChangePolicyService, ScheduleChangePolicyService>();
builder.Services.AddScoped<IScheduleChangeService, ScheduleChangeService>();
builder.Services.AddHostedService<ScheduleChangeExpirationWorker>();
builder.Services.AddScoped<IContractTemplateProvider, ContractTemplateProvider>();
builder.Services.AddScoped<IContractCodeGenerator, ContractCodeGenerator>();
builder.Services.AddScoped<IContractPdfGenerator, ContractPdfGenerator>();
builder.Services.AddSingleton<IInvoiceDigitalSignatureService, InvoiceDigitalSignatureService>();
builder.Services.AddScoped<IInvoicePdfGenerator, InvoicePdfGenerator>();
builder.Services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<ICoordinationAssignmentService, CoordinationAssignmentService>();
builder.Services.AddScoped<ChecklistService>();
builder.Services.AddScoped<IReviewQrEligibilityPolicy, ReviewQrEligibilityPolicy>();
builder.Services.AddScoped<IReviewQrService, ReviewQrService>();
builder.Services.AddScoped<IReviewStatisticsService, ReviewStatisticsService>();
builder.Services.AddScoped<IHallManagerDashboardService, HallManagerDashboardService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IFirstPasswordOtpService, FirstPasswordOtpService>();
builder.Services.AddScoped<DevelopmentAuthSeeder>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<IAiCatalogSyncService, AiCatalogSyncService>();
builder.Services.AddHttpClient();
builder.Services
    .AddOptions<AiServiceOptions>()
    .Bind(builder.Configuration.GetSection(AiServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PricingPolicyOptions>()
    .Bind(builder.Configuration.GetSection(PricingPolicyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient("AiService", (provider, client) =>
{
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebFrontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("ai-chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("wedding-review", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{httpContext.Connection.RemoteIpAddress}:{httpContext.Request.RouteValues["token"]}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Riverside Palace API",
        Version = "v1",
        Description = "API quản lý và khuyến nghị đặt tiệc cưới Riverside Palace"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"] = "1; mode=block";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }
        return Task.CompletedTask;
    });
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentAuthSeeder>();
    await seeder.SeedAsync();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("WebFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

app.Run();

public partial class Program;

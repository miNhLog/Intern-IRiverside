using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Net;
using System.Net.Http.Headers;
using System.Globalization;

const string externalCookieScheme = "External";

var builder = WebApplication.CreateBuilder(args);
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddHttpClient<IRiversideApiClient, RiversideApiClient>(client =>
    {
        var baseUrl = builder.Configuration["RiversideApi:BaseUrl"]
            ?? throw new InvalidOperationException("Thiếu RiversideApi:BaseUrl.");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(20);
    });

builder.Services.AddHttpClient("RiversideApi", client =>
{
    var baseUrl = builder.Configuration["RiversideApi:BaseUrl"]
        ?? throw new InvalidOperationException("Thiếu RiversideApi:BaseUrl.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/khong-co-quyen";
        options.Cookie.Name = "rp_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.Events.OnValidatePrincipal = async context =>
        {
            var accessToken = context.Request.Cookies["rp_api_token"];
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                context.RejectPrincipal();
                return;
            }

            try
            {
                var client = context.HttpContext.RequestServices
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient("RiversideApi");
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    context.RejectPrincipal();
                    context.Response.Cookies.Delete("rp_api_token");
                }
            }
            catch (HttpRequestException)
            {
                // A temporary API outage must not destroy an otherwise valid local session.
            }
        };
    })
    .AddCookie(externalCookieScheme, options =>
    {
        options.Cookie.Name = "rp_external_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle("Google", options =>
    {
        options.SignInScheme = externalCookieScheme;
        options.ClientId = string.IsNullOrWhiteSpace(googleClientId)
            ? "google-client-id-not-configured"
            : googleClientId;
        options.ClientSecret = string.IsNullOrWhiteSpace(googleClientSecret)
            ? "google-client-secret-not-configured"
            : googleClientSecret;
        options.SaveTokens = true;
    });
builder.Services.AddAuthorization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var vietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    options.DefaultRequestCulture = new RequestCulture(vietnameseCulture);
    options.SupportedCultures = [vietnameseCulture];
    options.SupportedUICultures = [vietnameseCulture];
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();

// A temporary password is not enough to enter any Back Office route. The flag is
// issued from the authenticated login response and is cleared only after the API
// accepts the password change.
app.Use(async (context, next) =>
{
    var mustChangePassword = bool.TryParse(
        context.User.FindFirst(AuthController.MustChangePasswordClaim)?.Value,
        out var value) && value;
    var path = context.Request.Path;
    var isPasswordRoute = path.StartsWithSegments("/doi-mat-khau");
    var isLogoutRoute = path.StartsWithSegments("/dang-xuat");

    if (context.User.Identity?.IsAuthenticated == true &&
        mustChangePassword && !isPasswordRoute && !isLogoutRoute)
    {
        context.Response.Redirect("/doi-mat-khau");
        return;
    }

    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

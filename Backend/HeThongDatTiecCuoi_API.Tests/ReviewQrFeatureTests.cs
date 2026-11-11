using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.DTOs.Review;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ReviewQrFeatureTests
{
    [Fact]
    public void Eligibility_DoesNotRequirePayment_AndAcceptsPreparingBooking()
    {
        var policy = new ReviewQrEligibilityPolicy(Microsoft.Extensions.Options.Options.Create(new ReviewQrOptions { PaymentGateEnabled = false }));
        var schedule = new HallSchedule { Date = DateTime.Today.AddDays(2), Shift = "Ca trưa" };

        Assert.True(policy.IsEligible(BookingStatusCodes.Preparing, schedule));
    }

    [Theory]
    [InlineData(BookingStatusCodes.Pending)]
    [InlineData(BookingStatusCodes.Confirmed)]
    [InlineData(BookingStatusCodes.Completed)]
    [InlineData(BookingStatusCodes.Cancelled)]
    public void Eligibility_RejectsNonPreparingBooking(string status)
    {
        var policy = new ReviewQrEligibilityPolicy(Microsoft.Extensions.Options.Options.Create(new ReviewQrOptions { PaymentGateEnabled = false }));
        var schedule = new HallSchedule { Date = DateTime.Today.AddDays(2), Shift = "Ca tối" };

        Assert.False(policy.IsEligible(status, schedule));
    }

    [Fact]
    public void Window_FutureEvent_IsPending_AndExpires24HoursAfterShiftEnd()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today.AddDays(3), Shift = "Ca trưa" };

        var window = service.GetWindow(schedule, false);

        Assert.Equal(ReviewQrState.Pending, window.State);
        Assert.Equal(schedule.Date.Date.AddHours(10.5), window.OpensAt);
        Assert.Equal(schedule.Date.Date.AddHours(38), window.ExpiresAt);
    }

    [Fact]
    public void GuestWindow_OpensAtIssuance_AndExpires24HoursAfterShiftEnd()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today.AddDays(3), Shift = "Ca trưa" };
        var issuedAt = RiversideBusinessClock.Now.AddMinutes(-1);

        var window = service.GetGuestWindow(schedule, issuedAt, false);

        Assert.Equal(ReviewQrState.Active, window.State);
        Assert.Equal(issuedAt, window.OpensAt);
        Assert.Equal(schedule.Date.Date.AddHours(38), window.ExpiresAt);
    }

    [Fact]
    public void GuestWindow_PastExpiry_IsExpired_AndLockedAlwaysWins()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today.AddDays(-3), Shift = "Ca trưa" };
        var issuedAt = schedule.Date.Date.AddDays(-1);

        Assert.Equal(ReviewQrState.Expired, service.GetGuestWindow(schedule, issuedAt, false).State);
        Assert.Equal(ReviewQrState.Locked, service.GetGuestWindow(schedule, issuedAt, true).State);
    }

    [Fact]
    public void Window_CancelledOrLockedBooking_IsLockedRegardlessOfTime()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today, Shift = "Ca tối" };

        Assert.Equal(ReviewQrState.Locked, service.GetWindow(schedule, true).State);
    }

    private static ReviewQrService ServiceWithoutDatabase() =>
        new(null!, null!, NullLogger<ReviewQrService>.Instance);

    [Theory]
    [InlineData(5, "Positive")]
    [InlineData(4, "Positive")]
    [InlineData(3, "Neutral")]
    [InlineData(2, "Negative")]
    [InlineData(1, "Negative")]
    public void Statistics_ClassifiesOverallScoreCorrectly(int score, string expected) =>
        Assert.Equal(expected, ReviewStatisticsService.ClassifyScore(score));

    [Fact]
    public void Statistics_EmptySet_HasNoMisleadingPercentage() =>
        Assert.Null(ReviewStatisticsService.CalculateRate(0, 0));

    [Fact]
    public void Statistics_CalculatesPercentageFromActualReviewCount() =>
        Assert.Equal(66.7, ReviewStatisticsService.CalculateRate(2, 3));
}

public sealed class ReviewQrFlowIntegrationTests
{
    private const string ExpiredToken = "expired-token";

    [Fact]
    public async Task Coordinator_IssuesOnce_AndFetchesQrImage()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();
        using var client = factory.CreateClientFor(RoleNames.Coordinator);

        var first = await client.PostAsync("/api/reviews/coordinator/1/issue", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<ReviewQrIssueDto>();
        Assert.NotNull(created);
        Assert.True(created!.Created);
        Assert.False(string.IsNullOrWhiteSpace(created.Token));
        Assert.Contains("/danh-gia/khach-moi/", created.ReviewUrl);

        var second = await client.PostAsync("/api/reviews/coordinator/1/issue", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var existing = await second.Content.ReadFromJsonAsync<ReviewQrIssueDto>();
        Assert.NotNull(existing);
        Assert.False(existing!.Created);
        Assert.Equal(created.Token, existing.Token);

        var image = await client.GetAsync("/api/reviews/coordinator/1/qr.png");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Issuance_Denied_ForOutOfScopeBooking_AndForOtherRoles()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();

        using var coordinator = factory.CreateClientFor(RoleNames.Coordinator);
        var outOfScope = await coordinator.PostAsync("/api/reviews/coordinator/2/issue", null);
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);

        using var manager = factory.CreateClientFor(RoleNames.Manager);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await manager.PostAsync("/api/reviews/coordinator/1/issue", null)).StatusCode);

        using var admin = factory.CreateClientFor(RoleNames.Admin);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsync("/api/reviews/coordinator/1/issue", null)).StatusCode);

        using var anonymous = factory.CreateClientFor(null);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/reviews/coordinator/1/issue", null)).StatusCode);
    }

    [Fact]
    public async Task Guest_RateLimit_PartitionsByValidatedSession_ForSamePeerAndToken()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();
        var token = await IssueTokenAsync(factory);
        using var guest = factory.CreateClientFor(null);

        for (var i = 0; i < 6; i++)
        {
            var sessionId = i.ToString("x32");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/guest/{token}")
            {
                Content = JsonContent.Create(new
                {
                    hallScore = 5, foodScore = 4, serviceScore = 5,
                    soundScore = 3, lightingScore = 4, hygieneScore = 5,
                    comment = "Buổi tiệc chu đáo.", sessionId
                })
            };
            request.Headers.Add("X-Review-Session", sessionId);
            using var response = await guest.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<GuestSubmitResponse>();
            Assert.NotNull(result);
            Assert.True(result!.Success);
            Assert.False(result.Duplicate);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(6, await db.WeddingReviews.CountAsync());
    }

    [Fact]
    public async Task Guest_SubmitsSixScores_DerivesOverall_AndSuppressesDuplicatesPerSession()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();
        var token = await IssueTokenAsync(factory);
        using var guest = factory.CreateClientFor(null);

        var form = await guest.GetAsync($"/api/reviews/guest/{token}");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        var formDto = await form.Content.ReadFromJsonAsync<ReviewFormDto>();
        Assert.NotNull(formDto);
        Assert.True(formDto!.CanSubmit);
        Assert.Equal("Active", formDto.State);

        var body = new
        {
            hallScore = 5, foodScore = 4, serviceScore = 5,
            soundScore = 3, lightingScore = 4, hygieneScore = 5,
            comment = "Buổi tiệc chu đáo.", sessionId = "session-a"
        };
        var submitted = await guest.PostAsJsonAsync($"/api/reviews/guest/{token}", body);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var result = await submitted.Content.ReadFromJsonAsync<GuestSubmitResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.False(result.Duplicate);

        var duplicate = await guest.PostAsJsonAsync($"/api/reviews/guest/{token}", body);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateResult = await duplicate.Content.ReadFromJsonAsync<GuestSubmitResponse>();
        Assert.NotNull(duplicateResult);
        Assert.True(duplicateResult!.Duplicate);

        var otherSession = await guest.PostAsJsonAsync($"/api/reviews/guest/{token}", new
        {
            hallScore = 5, foodScore = 4, serviceScore = 5,
            soundScore = 3, lightingScore = 4, hygieneScore = 5,
            comment = "Buổi tiệc chu đáo.", sessionId = "session-b"
        });
        Assert.Equal(HttpStatusCode.OK, otherSession.StatusCode);
        var otherResult = await otherSession.Content.ReadFromJsonAsync<GuestSubmitResponse>();
        Assert.NotNull(otherResult);
        Assert.False(otherResult!.Duplicate);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reviews = await db.WeddingReviews.ToListAsync();
        Assert.Equal(2, reviews.Count);
        Assert.All(reviews, r =>
        {
            Assert.Equal(ReviewTypes.Guest, r.ReviewerType);
            Assert.Equal(4, r.OverallScore);
            Assert.Equal(5, r.HygieneScore);
            Assert.NotNull(r.SessionHash);
            Assert.Equal(64, r.SessionHash!.Length);
        });
    }

    [Fact]
    public async Task Guest_RejectsMissingScore_AndExpiredToken()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();
        var token = await IssueTokenAsync(factory);
        using var guest = factory.CreateClientFor(null);

        var missing = await guest.PostAsJsonAsync($"/api/reviews/guest/{token}", new
        {
            hallScore = 5, foodScore = 4, serviceScore = 5,
            soundScore = 3, lightingScore = 4, hygieneScore = (int?)null,
            sessionId = "session-missing"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var error = await missing.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("REVIEW_SCORES_REQUIRED", error.GetProperty("code").GetString());

        var expiredForm = await guest.GetAsync($"/api/reviews/guest/{ExpiredToken}");
        Assert.Equal(HttpStatusCode.OK, expiredForm.StatusCode);
        var expiredDto = await expiredForm.Content.ReadFromJsonAsync<ReviewFormDto>();
        Assert.NotNull(expiredDto);
        Assert.False(expiredDto!.CanSubmit);
        Assert.Equal("Expired", expiredDto.State);

        var expiredSubmit = await guest.PostAsJsonAsync($"/api/reviews/guest/{ExpiredToken}", new
        {
            hallScore = 5, foodScore = 4, serviceScore = 5,
            soundScore = 3, lightingScore = 4, hygieneScore = 5,
            sessionId = "session-expired"
        });
        Assert.Equal(HttpStatusCode.Gone, expiredSubmit.StatusCode);
    }

    [Fact]
    public async Task ManagementRead_IsManagerScoped_AndAdminDenied()
    {
        using var factory = new ReviewQrApiFactory();
        factory.Seed();

        using var manager = factory.CreateClientFor(RoleNames.Manager);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/reviews/management")).StatusCode);

        using var admin = factory.CreateClientFor(RoleNames.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/reviews/management")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/reviews/management/1/qr.png")).StatusCode);
    }

    private static async Task<string> IssueTokenAsync(ReviewQrApiFactory factory)
    {
        using var coordinator = factory.CreateClientFor(RoleNames.Coordinator);
        var response = await coordinator.PostAsync("/api/reviews/coordinator/1/issue", null);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ReviewQrIssueDto>();
        return dto!.Token;
    }

    public sealed record GuestSubmitResponse(bool Success, bool Duplicate, string? Message);
}

public sealed class ReviewQrApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"review-qr-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultForbidScheme = TestRoleAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>(
                TestRoleAuthenticationHandler.Scheme, _ => { });
        });
    }

    public HttpClient CreateClientFor(string? role)
    {
        var client = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);
        return client;
    }

    public void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Statuses.Any()) return;

        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
        db.Roles.AddRange(
            new Role { RoleId = 1, RoleName = RoleNames.Admin },
            new Role { RoleId = 2, RoleName = RoleNames.Manager },
            new Role { RoleId = 3, RoleName = RoleNames.HallManager },
            new Role { RoleId = 4, RoleName = RoleNames.Coordinator },
            new Role { RoleId = 5, RoleName = RoleNames.Customer });
        db.Statuses.AddRange(
            S(101, StatusGroups.Account, AccountStatusCodes.Active),
            S(201, StatusGroups.Hall, BusinessStatusCodes.Active),
            S(301, StatusGroups.HallSchedule, BusinessStatusCodes.Booked),
            S(801, StatusGroups.Booking, BookingStatusCodes.Pending),
            S(802, StatusGroups.Booking, BookingStatusCodes.Confirmed),
            S(803, StatusGroups.Booking, BookingStatusCodes.Preparing),
            S(804, StatusGroups.Booking, BookingStatusCodes.Completed),
            S(805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
            S(1101, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Assigned),
            S(1401, StatusGroups.QrReview, "ACTIVE"),
            S(1402, StatusGroups.QrReview, "LOCKED"));
        db.Halls.Add(new Hall
        {
            HallId = 1, HallCode = "S1", HallName = "Sảnh Riverside", MaximumCapacity = 500,
            RentalPrice = 1, StatusId = 201, DataStatusId = 1
        });

        AddEmployee(db, 10, 100, "QLS010", "Quản lý sảnh", 3);
        AddEmployee(db, 20, 999, "DP020", "Điều phối viên", 4);
        AddEmployee(db, 21, 998, "DP021", "Điều phối viên khác", 4);

        var eventDate = RiversideBusinessClock.Today.AddDays(2);
        AddBooking(db, 1, eventDate, "Ca trưa", BookingStatusCodes.Preparing);
        AddBooking(db, 2, eventDate, "Ca tối", BookingStatusCodes.Preparing);
        AddBooking(db, 3, RiversideBusinessClock.Today.AddDays(-2), "Ca trưa", BookingStatusCodes.Preparing);

        db.CoordinationAssignments.AddRange(
            new CoordinationAssignment
            {
                CoordinationAssignmentId = 1, BookingId = 1, CoordinatorEmployeeId = 20,
                AssignedByHallManagerEmployeeId = 10, StatusId = 1101, AssignedAt = DateTime.Today
            },
            new CoordinationAssignment
            {
                CoordinationAssignmentId = 2, BookingId = 2, CoordinatorEmployeeId = 21,
                AssignedByHallManagerEmployeeId = 10, StatusId = 1101, AssignedAt = DateTime.Today
            });

        db.ReviewQrCodes.Add(new ReviewQrCode
        {
            ReviewQrCodeId = 1, BookingId = 3, QrCode = "expired-token",
            CreatedAt = RiversideBusinessClock.Today.AddDays(-3),
            ExpiresAt = RiversideBusinessClock.Today.AddDays(-1), StatusId = 1401
        });

        db.SaveChanges();
    }

    private static Status S(int id, string group, string code) =>
        new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };

    private static void AddEmployee(ApplicationDbContext db, int employeeId, int userId,
        string code, string name, byte roleId)
    {
        db.Users.Add(new User
        {
            UserId = userId, RoleId = roleId, Email = $"u{userId}@test.local",
            PasswordHash = "test", StatusId = 101, DataStatusId = 1
        });
        db.Employees.Add(new Employee
        {
            EmployeeId = employeeId, UserId = userId, EmployeeCode = code, FullName = name,
            PhoneNumber = $"09{userId:00000000}", DataStatusId = 1
        });
    }

    private static void AddBooking(ApplicationDbContext db, int bookingId, DateTime date,
        string shift, string status)
    {
        db.Customers.Add(new Customer
        {
            CustomerId = bookingId, CustomerCode = $"KH{bookingId}", FullName = $"Khách {bookingId}",
            PhoneNumber = $"090{bookingId:0000000}", DataStatusId = 1
        });
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = bookingId, HallId = 1, Date = date.Date, Shift = shift,
            StatusId = 301, DataStatusId = 1
        });
        var statusId = status switch
        {
            BookingStatusCodes.Pending => 801,
            BookingStatusCodes.Preparing => 803,
            BookingStatusCodes.Completed => 804,
            BookingStatusCodes.Cancelled => 805,
            _ => 802
        };
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = bookingId, BookingCode = $"DT-{bookingId}", CustomerId = bookingId,
            HallScheduleId = bookingId, GuestCount = 100, StatusId = statusId, BookedAt = DateTime.Now
        });
    }
}

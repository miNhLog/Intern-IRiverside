using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

/// <summary>
/// UC11 – khảo sát nhu cầu và gợi ý Top 3. Chỉ kiểm tra hành vi của
/// SuggestTop3Combos (validation, quy đổi bàn, sức chứa, kết quả rỗng).
/// Dùng EF InMemory; không cần SQL Server/LocalDB.
/// </summary>
public sealed class Uc11RecommendationTests
{
    private static readonly DateTime ValidDate = DateTime.Today.AddDays(60);
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    [Fact]
    public async Task SuggestTop3_ValidRequest_PersistsSurveyAndReturnsTopCombos()
    {
        await using var db = CreateContext();
        SeedCatalog(db);

        var result = await Controller(db).SuggestTop3Combos(Request(guestCount: 300));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RecommendationResponseDto>(ok.Value);
        Assert.NotEmpty(response.TopCombos);
        Assert.True(response.TopCombos.Count <= 3);
        Assert.All(response.TopCombos, combo => Assert.Equal(30, combo.EstimatedTableCount));

        var persisted = Assert.Single(db.RecommendationRequests);
        Assert.Equal(response.RecommendationRequestId, persisted.RecommendationRequestId);
        Assert.Equal(300, persisted.GuestCount);
        Assert.Equal(ValidDate, persisted.DesiredDate);
        Assert.Null(persisted.CustomerId);
    }

    [Fact]
    public async Task SuggestTop3_ClientTableCountTooLow_IsDerivedFromGuestCount()
    {
        await using var db = CreateContext();
        SeedCatalog(db);

        var request = Request(guestCount: 300);
        // Client khai thấp số bàn (1 bàn) để hạ giá; server phải tự quy đổi 30 bàn.
        request.TableCount = 1;
        request.OfficialTableCount = 1;
        request.SpareTableCount = 0;

        var ok = Assert.IsType<OkObjectResult>(await Controller(db).SuggestTop3Combos(request));
        var response = Assert.IsType<RecommendationResponseDto>(ok.Value);

        Assert.NotEmpty(response.TopCombos);
        Assert.All(response.TopCombos, combo =>
        {
            Assert.Equal(30, combo.EstimatedTableCount);
            Assert.Equal(combo.MenuPricePerTable * 30, combo.MenuTotalPrice);
        });
    }

    [Fact]
    public async Task SuggestTop3_SpareTables_ExcludesHallTooSmallButKeepsSuitableHall()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.Halls.AddRange(
            new Hall { HallId = 2, HallCode = "SMALL", HallName = "Small", MinimumCapacity = 150, MaximumCapacity = 500, RentalPrice = 20_000_000m, StatusId = 201, DataStatusId = 1 },
            new Hall { HallId = 3, HallCode = "FIT", HallName = "Fit", MinimumCapacity = 150, MaximumCapacity = 520, RentalPrice = 20_000_000m, StatusId = 201, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var request = Request(500, 2);
        request.ExpectedBudget = 1_000_000_000m;

        var ok = Assert.IsType<OkObjectResult>(await Controller(db).SuggestTop3Combos(request));
        var response = Assert.IsType<RecommendationResponseDto>(ok.Value);

        Assert.NotEmpty(response.TopCombos);
        Assert.Contains(response.TopCombos, combo => combo.HallId == 3);
        Assert.DoesNotContain(response.TopCombos, combo => combo.HallId == 2);
        Assert.All(response.TopCombos, combo => Assert.Equal(52, combo.EstimatedTableCount));
    }

    [Fact]
    public async Task SuggestTop3_SpareTablesPushOverLargestHall_ReturnsBadRequest()
    {
        await using var db = CreateContext();
        SeedCatalog(db);

        // 2000 khách = 200 bàn chính thức; +1 bàn dự phòng => vượt 200 bàn.
        var result = await Controller(db).SuggestTop3Combos(Request(guestCount: 2000, spareTables: 1));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.RecommendationRequests);
    }

    [Fact]
    public async Task SuggestTop3_NoMatchingHall_ReturnsEmptyResultNotValidationError()
    {
        await using var db = CreateContext();
        SeedCatalog(db, minimumCapacity: 500); // khách 300 không lọt sức chứa tối thiểu

        var result = await Controller(db).SuggestTop3Combos(Request(guestCount: 300));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RecommendationResponseDto>(ok.Value);
        Assert.Empty(response.TopCombos);
        Assert.Equal(0, response.TotalCandidatesEvaluated);
        Assert.NotNull(response.SuggestedNext);
        // Vẫn ghi nhận khảo sát: 0 combo không phải lỗi dữ liệu đầu vào.
        Assert.Single(db.RecommendationRequests);
    }

    [Fact]
    public async Task SuggestTop3_StrictBudget_ExcludesCostAboveBudgetAndAcceptsExactBoundary()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        var request = Request(150);
        request.ExpectedBudget = 103_250_000m;

        var exact = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);
        Assert.NotEmpty(exact.TopCombos);
        Assert.All(exact.TopCombos, combo => Assert.True(combo.TotalEstimatedCost <= request.ExpectedBudget));

        request.ExpectedBudget = 100_000_000m; // 3.25m over, formerly admitted by +10m tolerance.
        var over = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);
        Assert.Empty(over.TopCombos);
        Assert.Equal(0, over.TotalCandidatesEvaluated);
    }

    [Fact]
    public async Task SuggestTop3_ServiceCriterionIsExposedSeparatelyFromLegacyScore()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        var result = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(Request(300))).Value);
        var combo = Assert.Single(result.TopCombos);

        Assert.Equal(100, combo.ServiceMatchScore);
        Assert.NotEqual(combo.ServiceMatchScore, combo.RatingCredibilityScore);
        Assert.Contains(combo.Explanations, reason => reason.Contains("DV ưu tiên"));
    }

    [Fact]
    public async Task SuggestTop3_RanksSameHallOptionsByRawScoreAndStableIds()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.DecorPackages.AddRange(
            new DecorPackage { GoiTrangTriID = 2, MaGoi = "D002", TenGoi = "Same style tie", PhongCach = "Hiện đại / lãng mạn", Gia = 18_000_001m, StatusId = 501, DataStatusId = 1 },
            new DecorPackage { GoiTrangTriID = 3, MaGoi = "D003", TenGoi = "Near score tie", PhongCach = "Hiện đại / lãng mạn", Gia = 18_000_001m, StatusId = 501, DataStatusId = 1 },
            new DecorPackage { GoiTrangTriID = 4, MaGoi = "D004", TenGoi = "Near score tie", PhongCach = "Hiện đại / lãng mạn", Gia = 18_000_002m, StatusId = 501, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var request = Request(150);
        request.ExpectedBudget = 1_000_000_000m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Equal(new[] { 4, 2, 3 }, response.TopCombos.Select(combo => combo.DecorationPackageId));
        Assert.All(response.TopCombos, combo => Assert.Equal(1, combo.HallId));
        Assert.Equal(response.TopCombos[0].OverallMatchScore, response.TopCombos[1].OverallMatchScore);
    }

    [Fact]
    public async Task SuggestTop3_BudgetAndRecoveryIncludeSpareTablesAndSelectedActiveServices()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.ServiceItems.Add(new ServiceItem { DichVuID = 1, MaDichVu = "DV001", TenDichVu = "Live music", Gia = 10_000_000m, StatusId = 601, DataStatusId = 1 });
        db.Statuses.Add(new Status { StatusId = 601, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.Service });
        await db.SaveChangesAsync();
        var request = Request(300, spareTables: 2);
        request.ServiceNeeds = "DV001";
        request.ExpectedBudget = 187_199_999m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Empty(response.TopCombos);
        Assert.Equal(187_200_000m, response.SuggestedNext!.MinRequiredBudget);
        Assert.Equal(3, response.SuggestedNext.SuggestedDates.Count);
        Assert.Equal(32, (await db.RecommendationRequests.SingleAsync()).GuestCount / 10 + 2);
    }

    [Theory]
    [InlineData("DV999", false)]
    [InlineData("DV001", true)]
    public async Task SuggestTop3_UnresolvedOrInactiveRequestedServiceFailsClosed(string code, bool inactive)
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.Statuses.Add(new Status
        {
            StatusId = 601,
            StatusCode = inactive ? BusinessStatusCodes.Inactive : BusinessStatusCodes.Active,
            StatusName = inactive ? "Ngừng hoạt động" : "Hoạt động",
            StatusGroup = StatusGroups.Service
        });
        if (inactive)
            db.ServiceItems.Add(new ServiceItem { DichVuID = 1, MaDichVu = code, TenDichVu = "Unavailable", Gia = 10_000_000m, StatusId = 601, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var request = Request(300);
        request.ServiceNeeds = code;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Empty(response.TopCombos);
        Assert.Equal(0, response.TotalCandidatesEvaluated);
        Assert.Null(response.SuggestedNext!.MinRequiredBudget);
        Assert.Empty(response.SuggestedNext.SuggestedDates);
        Assert.Contains("dịch vụ ACTIVE đã chọn", response.SuggestedNext.Reason);
    }

    [Fact]
    public async Task SuggestTop3_DuplicateServiceCodesAreResolvedAndChargedOnce()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.Statuses.Add(new Status { StatusId = 601, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.Service });
        db.ServiceItems.Add(new ServiceItem { DichVuID = 1, MaDichVu = "DV001", TenDichVu = "Live music", Gia = 10_000_000m, StatusId = 601, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var request = Request(300, spareTables: 2);
        request.ServiceNeeds = "DV001,dv001,DV001";
        request.ExpectedBudget = 187_200_000m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        var combo = Assert.Single(response.TopCombos);
        Assert.Equal(10_000_000m, combo.ServicesTotalPrice);
        Assert.Equal(100, combo.ServiceMatchScore);
        Assert.Single(combo.IncludedServices);
        Assert.Equal(request.ExpectedBudget, combo.TotalEstimatedCost);
    }

    [Fact]
    public async Task SuggestTop3_RecoveryUsesMoreExpensiveHallAvailableWithinWindow()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.Halls.Add(new Hall
        {
            HallId = 2, HallCode = "ALT", HallName = "Alternative", MinimumCapacity = 150,
            MaximumCapacity = 2000, RentalPrice = 30_000_000m, StatusId = 201, DataStatusId = 1
        });
        db.Statuses.Add(new Status { StatusId = 302, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt", StatusGroup = StatusGroups.HallSchedule });
        var firstDate = ValidDate;
        db.HallSchedules.AddRange(Enumerable.Range(0, 11).Select(offset => new HallSchedule
        {
            HallScheduleId = offset + 1, HallId = 2, Date = firstDate.AddDays(offset), Shift = HallShiftNames.Dinner,
            StatusId = 302, DataStatusId = 1
        }));
        // The cheaper hall becomes free only after the three dates that will be returned.
        db.HallSchedules.AddRange(Enumerable.Range(0, 21).Select(offset => new HallSchedule
        {
            HallScheduleId = offset + 100, HallId = 1, Date = firstDate.AddDays(offset), Shift = HallShiftNames.Dinner,
            StatusId = 302, DataStatusId = 1
        }));
        await db.SaveChangesAsync();
        var request = Request(150);
        request.ExpectedBudget = 100_000_000m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Empty(response.TopCombos);
        Assert.Equal(113_250_000m, response.SuggestedNext!.MinRequiredBudget);
        Assert.NotEmpty(response.SuggestedNext.SuggestedDates);
        Assert.All(response.SuggestedNext.SuggestedDates, date => Assert.Contains("Alternative", date.AvailableHalls));
        Assert.Equal(firstDate.AddDays(11).ToString("yyyy-MM-dd"), response.SuggestedNext.SuggestedDates[0].Date);
    }

    [Fact]
    public async Task SuggestTop3_RecoveryHasNoBudgetWhenAllEligibleHallsBlockedAcrossWindow()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        db.Statuses.Add(new Status { StatusId = 302, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt", StatusGroup = StatusGroups.HallSchedule });
        db.HallSchedules.AddRange(Enumerable.Range(0, 61).Select(offset => new HallSchedule
        {
            HallScheduleId = offset + 1, HallId = 1, Date = ValidDate.AddDays(offset), Shift = HallShiftNames.Dinner,
            StatusId = 302, DataStatusId = 1
        }));
        await db.SaveChangesAsync();
        var request = Request(150);
        request.ExpectedBudget = 100_000_000m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Empty(response.TopCombos);
        Assert.Null(response.SuggestedNext!.MinRequiredBudget);
        Assert.Empty(response.SuggestedNext.SuggestedDates);
    }

    [Fact]
    public async Task SuggestTop3_ZeroPricedCatalogIsValidAndMissingCatalogHasNoActionableRecovery()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        var menu = await db.Menus.SingleAsync();
        menu.PricePerTable = 0;
        var decor = await db.DecorPackages.SingleAsync();
        decor.Gia = 0;
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = 1, HallId = 1, Date = ValidDate, Shift = HallShiftNames.Dinner,
            StatusId = 302, DataStatusId = 1,
            Status = new Status { StatusId = 302, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt", StatusGroup = StatusGroups.HallSchedule }
        });
        await db.SaveChangesAsync();
        var request = Request(150);
        request.ExpectedBudget = 100_000_000m;
        var priced = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);
        Assert.NotNull(priced.SuggestedNext);
        Assert.Equal(20_000_000m, priced.SuggestedNext!.MinRequiredBudget);

        db.Menus.Remove(menu);
        await db.SaveChangesAsync();
        var missing = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);
        Assert.Null(missing.SuggestedNext!.MinRequiredBudget);
        Assert.Empty(missing.SuggestedNext.SuggestedDates);
    }

    [Fact]
    public async Task SuggestTop3_RecoveryHonorsGuestMinimumAnd365DayCalendarBoundary()
    {
        await using var db = CreateContext();
        SeedCatalog(db, minimumCapacity: 160);
        var tooFewGuests = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(Request(151))).Value);
        Assert.Empty(tooFewGuests.TopCombos);
        Assert.Empty(tooFewGuests.SuggestedNext!.SuggestedDates);

        var status = await db.Statuses.SingleAsync(item => item.StatusGroup == StatusGroups.HallSchedule);
        status.StatusCode = HallScheduleStatusCodes.Booked;
        var lastDay = DateTime.Today.AddDays(365);
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = 1, HallId = 1, Date = lastDay, Shift = HallShiftNames.Dinner,
            StatusId = status.StatusId, DataStatusId = 1
        });
        await db.SaveChangesAsync();
        var atBoundary = Request(160);
        atBoundary.DesiredDate = lastDay;
        var boundary = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(atBoundary)).Value);
        Assert.Empty(boundary.TopCombos);
        Assert.All(boundary.SuggestedNext!.SuggestedDates, date =>
            Assert.InRange(DateTime.Parse(date.Date), DateTime.Today.AddDays(30), lastDay));
        Assert.DoesNotContain(boundary.SuggestedNext.SuggestedDates, date => date.Date == lastDay.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task SuggestTop3_RecoveryAboveMaximumBudgetDoesNotOfferActionableBudget()
    {
        await using var db = CreateContext();
        SeedCatalog(db);
        var menu = await db.Menus.SingleAsync();
        menu.PricePerTable = 200_000_000m;
        await db.SaveChangesAsync();
        var request = Request(150);
        request.ExpectedBudget = 3_000_000_000m;

        var response = Assert.IsType<RecommendationResponseDto>(Assert.IsType<OkObjectResult>(
            await Controller(db).SuggestTop3Combos(request)).Value);

        Assert.Empty(response.TopCombos);
        Assert.Null(response.SuggestedNext!.MinRequiredBudget);
        Assert.Empty(response.SuggestedNext.SuggestedDates);
        Assert.Contains("tổng chi phí", response.SuggestedNext.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static RecommendationRequestDto Request(int guestCount, int spareTables = 0) => new()
    {
        ExpectedBudget = 250_000_000m,
        GuestCount = guestCount,
        SpareTableCount = spareTables,
        DesiredDate = ValidDate,
        DesiredShift = HallShiftNames.Dinner,
        DesiredStyle = "Hiện đại / lãng mạn",
        ServiceNeeds = null
    };

    private static RecommendationController Controller(ApplicationDbContext db)
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        return new RecommendationController(
            db,
            new StatusService(db),
            new EmptyHttpClientFactory(),
            Microsoft.Extensions.Options.Options.Create(new AiServiceOptions()),
            NullLogger<RecommendationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("Uc11RecommendationTests", DatabaseRoot)
            .Options);
        context.Database.EnsureDeleted();
        return context;
    }

    private static void SeedCatalog(ApplicationDbContext db, int? minimumCapacity = 150)
    {
        db.DataStatuses.Add(new DataStatus
        {
            DataStatusId = 1,
            DataStatusCode = DataStatusCodes.Existing,
            DataStatusName = "Tồn tại"
        });

        db.Statuses.AddRange(
            new Status { StatusId = 201, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.Hall },
            new Status { StatusId = 301, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.HallSchedule },
            new Status { StatusId = 401, StatusCode = MenuStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.Menu },
            new Status { StatusId = 501, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động", StatusGroup = StatusGroups.DecorationPackage });

        db.Halls.Add(new Hall
        {
            HallId = 1,
            HallCode = "GB",
            HallName = "Grand Ballroom",
            MinimumCapacity = minimumCapacity,
            MaximumCapacity = 2000,
            RentalPrice = 20_000_000m,
            StatusId = 201,
            DataStatusId = 1
        });

        db.Menus.Add(new Menu
        {
            MenuId = 1,
            MenuCode = "M001",
            MenuName = "Set tiêu chuẩn",
            MenuType = MenuTypes.Standard,
            PricePerTable = 4_350_000m,
            StatusId = 401,
            DataStatusId = 1
        });

        db.DecorPackages.Add(new DecorPackage
        {
            GoiTrangTriID = 1,
            MaGoi = "D001",
            TenGoi = "Midnight Celestial Blue",
            PhongCach = "Hiện đại / lãng mạn",
            Gia = 18_000_000m,
            StatusId = 501,
            DataStatusId = 1
        });

        db.SaveChanges();
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

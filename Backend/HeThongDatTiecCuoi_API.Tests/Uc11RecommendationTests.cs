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

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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

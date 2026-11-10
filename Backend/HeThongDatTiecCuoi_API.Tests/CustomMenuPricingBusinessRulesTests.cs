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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class CustomMenuPricingBusinessRulesTests
{
    [Fact]
    public async Task TC01_OriginalMenuWithoutCustomization_UsesDatabaseMenuPrice()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync(customRequested: false);

        Assert.True(result.IsValid);
        Assert.False(result.IsCustomized);
        Assert.Equal(4_000_000m, result.OfficialPricePerTable);
    }

    [Fact]
    public async Task TC02_ValidCustomMenuCalculation_UsesBasePricePlusDishDifference()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 2, 4], expectedPrice: 4_300_000m);

        Assert.True(result.IsValid);
        Assert.True(result.IsCustomized);
        Assert.False(result.RequiresPriceConfirmation);
        Assert.Equal(4_300_000m, result.OfficialPricePerTable);
    }

    [Fact]
    public async Task TC03_ClientTamperedCustomMenuPrice_IsNeverAcceptedAsOfficialPrice()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 2, 4], expectedPrice: 100_000m);

        Assert.True(result.IsValid);
        Assert.True(result.RequiresPriceConfirmation);
        Assert.Equal(4_300_000m, result.OfficialPricePerTable);
    }

    [Fact]
    public async Task TC04_ClientTamperedDishPriceString_DoesNotEnterPricingService()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new RegisterPublicBookingDto
        {
            MenuId = 1,
            CustomMenuDishIds = [1, 2, 4],
            CustomMenuItems = ["Món 1 (1 đ)", "Món 2 (1 đ)", "Món 4 (1 đ)"],
            CustomMenuPricePerTable = 4_300_000m
        };

        var result = await fixture.Service.ResolveAsync(
            request.MenuId.Value, null, request.CustomMenuDishIds, true,
            request.CustomMenuPricePerTable);

        Assert.Equal(4_300_000m, result.OfficialPricePerTable);
        Assert.Equal(1_600_000m, result.SelectedDishes.Single(item => item.DishId == 4).Price);
    }

    [Fact]
    public async Task TC05_DuplicateDishIds_AreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 1, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_DISH_IDS_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task TC06_InvalidDishId_RejectsWholeSelection()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 2, 999]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_DISH_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task TC07_InactiveDish_IsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Dishes.FindAsync(4))!.StatusId = 12;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.ResolveAsync([1, 2, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_DISH_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task TC08_DeletedDish_IsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Dishes.FindAsync(4))!.DataStatusId = 2;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.ResolveAsync([1, 2, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_DISH_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task TC09_InvalidBaseMenu_IsRejectedBeforeCustomPriceCalculation()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Menus.FindAsync(1))!.StatusId = 2;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.ResolveAsync([1, 2, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_BASE_MENU", result.ErrorCode);
    }

    [Fact]
    public async Task TC09_BaseMenuWithoutReliableDishList_IsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.MenuDishes.RemoveRange(fixture.Db.MenuDishes);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.ResolveAsync([1, 2, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("BASE_MENU_DISH_DATA_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task TC10_TooManyCustomDishes_AreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 2, 3, 4]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_TOO_MANY_DISHES", result.ErrorCode);
    }

    [Fact]
    public async Task TC11_EmptyCustomMenu_IsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([]);

        Assert.False(result.IsValid);
        Assert.Equal("CUSTOM_MENU_EMPTY", result.ErrorCode);
    }

    [Fact]
    public async Task TC12_CustomPriceMatchesFrontend_AllowsConfirmation()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([1, 2, 4], expectedPrice: 4_300_000m);

        Assert.True(result.IsValid);
        Assert.False(result.RequiresPriceConfirmation);
    }

    [Fact]
    public async Task TC13_PriceChangedBeforeBooking_ReturnsNewOfficialPrice()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Dishes.FindAsync(4))!.Price = 1_800_000m;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.ResolveAsync([1, 2, 4], expectedPrice: 4_300_000m);

        Assert.True(result.RequiresPriceConfirmation);
        Assert.Equal(4_500_000m, result.OfficialPricePerTable);
    }

    [Fact]
    public void TC14_EstimatedTotalConsistency_UsesVerifiedMenuPriceAndAllComponents()
    {
        var total = CustomMenuPricingService.CalculateEstimatedTotal(
            hallPrice: 20_000_000m,
            menuPricePerTable: 4_300_000m,
            tableCount: 22,
            decorationPrice: 18_000_000m,
            servicesTotal: 6_000_000m);

        Assert.Equal(138_600_000m, total);
    }

    [Fact]
    public async Task TC15_InvalidPrice_DoesNotCreateBookingOrOccupySchedule()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Dishes.FindAsync(4))!.StatusId = 12;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.ResolveAsync([1, 2, 4]);

        Assert.False(result.IsValid);
        Assert.Empty(fixture.Db.WeddingBookings);
        Assert.Equal(HallScheduleStatusCodes.Available,
            await fixture.Db.HallSchedules.Select(item => item.Status.StatusCode).SingleAsync());
    }

    [Fact]
    public async Task TC14_RegisterBooking_PersistsSameVerifiedPriceInFinalMenuAndEstimatedTotal()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.ValidBookingRequest(4_300_000m);

        var result = await fixture.Controller().RegisterPublicBooking(request);

        Assert.IsType<OkObjectResult>(result);
        var booking = await fixture.Db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(4_300_000m, booking.FinalMenuPrice);
        Assert.Equal(booking.FinalHallPrice + 4_300_000m * 22, booking.EstimatedTotal);
        Assert.Contains("Món 4 (1,600,000 đ)", booking.SpecialRequests);
        Assert.DoesNotContain("Món 4 (1 đ)", booking.SpecialRequests);
    }

    [Fact]
    public async Task TC15_RegisterBooking_PriceConflictCreatesNoBookingAndDoesNotOccupySchedule()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.ValidBookingRequest(100_000m);

        var result = await fixture.Controller().RegisterPublicBooking(request);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(fixture.Db.WeddingBookings);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(HallScheduleStatusCodes.Available,
            await fixture.Db.HallSchedules.Select(item => item.Status.StatusCode).SingleAsync());
    }

    [Fact]
    public async Task TC17_RestoringOriginalDishSet_RemainsAnOriginalMenu()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.ResolveAsync([3, 1, 2], expectedPrice: 1m);

        Assert.True(result.IsValid);
        Assert.False(result.IsCustomized);
        Assert.False(result.RequiresPriceConfirmation);
        Assert.Equal(4_000_000m, result.OfficialPricePerTable);
    }

    [Fact]
    public async Task CustomerSpecificMenu_IsRejectedForAnotherCustomer()
    {
        await using var fixture = await Fixture.CreateAsync();
        (await fixture.Db.Menus.FindAsync(1))!.CustomerId = 77;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.Service.ResolveAsync(1, 88, [1, 2, 4], true, 4_300_000m);

        Assert.False(result.IsValid);
        Assert.Equal("MENU_ACCESS_DENIED", result.ErrorCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db)
        {
            Db = db;
            Service = new CustomMenuPricingService(db);
        }

        public ApplicationDbContext Db { get; }
        public CustomMenuPricingService Service { get; }
        private static readonly DateTime EventDate = DateTime.Today.AddDays(60);

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings
                    .Ignore(InMemoryEventId.TransactionIgnoredWarning)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options;
            var fixture = new Fixture(new ApplicationDbContext(options));
            var existing = new DataStatus
                { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
            var deleted = new DataStatus
                { DataStatusId = 2, DataStatusCode = DataStatusCodes.Deleted, DataStatusName = "Đã xóa" };
            var menuActive = new Status
                { StatusId = 1, StatusGroup = StatusGroups.Menu, StatusCode = MenuStatusCodes.Active, StatusName = "Hoạt động" };
            var menuInactive = new Status
                { StatusId = 2, StatusGroup = StatusGroups.Menu, StatusCode = MenuStatusCodes.Inactive, StatusName = "Ngừng" };
            var dishActive = new Status
                { StatusId = 11, StatusGroup = StatusGroups.Dish, StatusCode = DishStatusCodes.Active, StatusName = "Hoạt động" };
            var dishInactive = new Status
                { StatusId = 12, StatusGroup = StatusGroups.Dish, StatusCode = DishStatusCodes.Inactive, StatusName = "Ngừng" };
            var hallActive = new Status
                { StatusId = 21, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" };
            var scheduleAvailable = new Status
                { StatusId = 31, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
            var scheduleBooked = new Status
                { StatusId = 32, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" };
            var bookingPending = new Status
                { StatusId = 41, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ" };
            fixture.Db.AddRange(existing, deleted, menuActive, menuInactive, dishActive, dishInactive,
                hallActive, scheduleAvailable, scheduleBooked, bookingPending);

            var menu = new Menu
            {
                MenuId = 1, MenuCode = "TD01", MenuName = "Thực đơn chuẩn", MenuType = "STANDARD",
                PricePerTable = 4_000_000m, StatusId = 1, Status = menuActive,
                DataStatusId = 1, DataStatus = existing
            };
            var dishes = new[]
            {
                Dish(1, "Món 1", 1_000_000m, dishActive, existing),
                Dish(2, "Món 2", 1_200_000m, dishActive, existing),
                Dish(3, "Món 3", 1_300_000m, dishActive, existing),
                Dish(4, "Món 4", 1_600_000m, dishActive, existing)
            };
            fixture.Db.Menus.Add(menu);
            fixture.Db.Dishes.AddRange(dishes);
            fixture.Db.MenuDishes.AddRange(
                Link(1, 1, menu, dishes[0], existing),
                Link(2, 2, menu, dishes[1], existing),
                Link(3, 3, menu, dishes[2], existing));
            var hall = new Hall
            {
                HallId = 1, HallCode = "H01", HallName = "Sảnh Test", MinimumCapacity = 50,
                MaximumCapacity = 500, RentalPrice = 20_000_000m,
                StatusId = hallActive.StatusId, Status = hallActive,
                DataStatusId = existing.DataStatusId, DataStatus = existing
            };
            fixture.Db.Halls.Add(hall);
            fixture.Db.HallSchedules.Add(new HallSchedule
            {
                HallScheduleId = 1, HallId = hall.HallId, Hall = hall,
                Date = EventDate, Shift = HallShiftNames.Dinner,
                StatusId = scheduleAvailable.StatusId, Status = scheduleAvailable,
                DataStatusId = existing.DataStatusId, DataStatus = existing
            });
            await fixture.Db.SaveChangesAsync();
            fixture.Db.ChangeTracker.Clear();
            return fixture;
        }

        public Task<CustomMenuPricingResult> ResolveAsync(
            IReadOnlyCollection<int>? ids = null,
            decimal? expectedPrice = 4_300_000m,
            bool customRequested = true) =>
            Service.ResolveAsync(1, null, ids, customRequested, expectedPrice);

        public RegisterPublicBookingDto ValidBookingRequest(decimal expectedCustomPrice) => new()
        {
            CustomerName = "Nguyễn Khách",
            PhoneNumber = "0901234567",
            Email = "customer@example.com",
            HallScheduleId = 1,
            HallId = 1,
            EventDate = EventDate,
            Shift = HallShiftNames.Dinner,
            MenuId = 1,
            GuestCount = 200,
            TableCount = 22,
            SpareTableCount = 2,
            CustomMenuName = "Thực đơn tùy chỉnh 1",
            CustomMenuDishIds = [1, 2, 4],
            CustomMenuPricePerTable = expectedCustomPrice,
            CustomMenuItems = ["Món 1 (1 đ)", "Món 2 (1 đ)", "Món 4 (1 đ)"]
        };

        public RecommendationController Controller()
        {
            var controller = new RecommendationController(
                Db,
                new StatusServiceStub(),
                new HttpClientFactoryStub(),
                Microsoft.Extensions.Options.Options.Create(new AiServiceOptions()),
                Microsoft.Extensions.Options.Options.Create(new PricingPolicyOptions()),
                NullLogger<RecommendationController>.Instance,
                customMenuPricing: Service);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static Dish Dish(int id, string name, decimal price, Status status, DataStatus dataStatus) =>
            new()
            {
                DishId = id, DishCode = $"MA{id:00}", DishName = name, Category = "Món chính",
                Price = price, StatusId = status.StatusId, Status = status,
                DataStatusId = dataStatus.DataStatusId, DataStatus = dataStatus
            };

        private static MenuDish Link(int id, int dishId, Menu menu, Dish dish, DataStatus dataStatus) =>
            new()
            {
                MenuDishId = id, MenuId = menu.MenuId, Menu = menu,
                DishId = dishId, Dish = dish, SortOrder = id,
                DataStatusId = dataStatus.DataStatusId, DataStatus = dataStatus
            };
    }

    private sealed class StatusServiceStub : IStatusService
    {
        public Task<Status?> GetStatusAsync(string group, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(null);

        public Task<int> GetStatusIdAsync(string group, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult((group, code) switch
            {
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked) => 32,
                (StatusGroups.Booking, BookingStatusCodes.Pending) => 41,
                _ => throw new InvalidOperationException($"Unknown status {group}/{code}")
            });
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

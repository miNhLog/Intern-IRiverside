using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.PricingPolicy;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public class Phase4FeatureTests
{
    [Fact]
    public void HallPricingPolicyService_Calculates_Correct_Multipliers()
    {
        var service = new HallPricingPolicyService();
        decimal basePrice = 60_000_000m;

        // Tuesday (weekday)
        var tuesday = new DateTime(2026, 6, 2); // Tuesday
        var weekdayLunch = service.CalculateHallPrice(basePrice, "Ca trưa", tuesday);
        var weekdayDinner = service.CalculateHallPrice(basePrice, "Ca tối", tuesday);

        // Saturday (weekend)
        var saturday = new DateTime(2026, 6, 6); // Saturday
        var weekendLunch = service.CalculateHallPrice(basePrice, "Ca trưa", saturday);
        var weekendDinner = service.CalculateHallPrice(basePrice, "Ca tối", saturday);

        // Sunday (weekend)
        var sunday = new DateTime(2026, 6, 7); // Sunday
        var sundayDinner = service.CalculateHallPrice(basePrice, "Ca tối", sunday);

        Assert.Equal(60_000_000m, weekdayLunch); // 1.0x
        Assert.Equal(69_000_000m, weekdayDinner); // 1.15x (+15%)
        Assert.Equal(75_000_000m, weekendLunch); // 1.25x (+25%)
        Assert.Equal(75_000_000m, weekendDinner); // 1.25x (+25%)
        Assert.Equal(75_000_000m, sundayDinner); // 1.25x (+25%)

        var breakdown = service.GetShiftPriceBreakdown(basePrice);
        Assert.Equal(60_000_000m, breakdown.BasePrice);
        Assert.Equal(60_000_000m, breakdown.NoonPrice);
        Assert.Equal(69_000_000m, breakdown.EveningPrice);
        Assert.Equal(75_000_000m, breakdown.WeekendPrice);
    }

    [Fact]
    public async Task AdminPricingPolicyController_Returns_Consistent_Shift_Pricing()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "PricingPolicyDb_" + Guid.NewGuid().ToString())
            .Options;

        using var db = new ApplicationDbContext(options);
        var existingStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" };
        var hallStatus = new Status { StatusId = 101, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" };
        db.DataStatuses.Add(existingStatus);
        db.Statuses.Add(hallStatus);

        var hall = new Hall
        {
            HallId = 1,
            HallCode = "H01",
            HallName = "Grand Ballroom",
            RentalPrice = 60_000_000m,
            MinimumCapacity = 200,
            MaximumCapacity = 600,
            DataStatusId = 1,
            DataStatus = existingStatus,
            StatusId = 101,
            Status = hallStatus
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();

        var pricingService = new HallPricingPolicyService();
        var paymentProvider = new PaymentPolicyProvider();
        var controller = new AdminPricingPolicyController(db, paymentProvider, pricingService);

        var result = await controller.GetHallShiftPricings() as OkObjectResult;
        Assert.NotNull(result);

        var val = result.Value;
        Assert.NotNull(val);
        var dataProp = val.GetType().GetProperty("data")?.GetValue(val) as List<HallShiftPricingDto>;
        Assert.NotNull(dataProp);
        Assert.Single(dataProp);

        var dto = dataProp[0];
        Assert.Equal(60_000_000m, dto.GiaGoc);
        Assert.Equal(60_000_000m, dto.GiaCaTrua);
        Assert.Equal(69_000_000m, dto.GiaCaToi);
        Assert.Equal(75_000_000m, dto.GiaCuoiTuan);
    }

    [Fact]
    public async Task Recommendation_Fallback_Applies_Shift_And_Weekend_Hall_Price()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "RecomPricingDb_" + Guid.NewGuid().ToString())
            .Options;

        using var db = new ApplicationDbContext(options);
        var existingStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" };
        var hallActive = new Status { StatusId = 101, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" };
        var schedAvail = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Sẵn sàng" };
        var menuActive = new Status { StatusId = 201, StatusGroup = StatusGroups.Menu, StatusCode = MenuStatusCodes.Active, StatusName = "Hoạt động" };
        var decorActive = new Status { StatusId = 401, StatusGroup = StatusGroups.DecorationPackage, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" };
        var bookingPending = new Status { StatusId = 801, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ duyệt" };

        db.DataStatuses.Add(existingStatus);
        db.Statuses.AddRange(hallActive, schedAvail, menuActive, decorActive, bookingPending);

        var hall = new Hall
        {
            HallId = 1,
            HallCode = "H01",
            HallName = "Sảnh Pearl",
            RentalPrice = 40_000_000m,
            MinimumCapacity = 100,
            MaximumCapacity = 400,
            DataStatusId = 1,
            DataStatus = existingStatus,
            StatusId = 101,
            Status = hallActive
        };
        db.Halls.Add(hall);

        // Future Saturday >= 30 days away
        var saturday = DateTime.Today.AddDays(40);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);

        var schedSaturday = new HallSchedule
        {
            HallScheduleId = 10,
            HallId = 1,
            Hall = hall,
            Date = saturday,
            Shift = "Ca tối",
            StatusId = 301,
            Status = schedAvail,
            DataStatusId = 1,
            DataStatus = existingStatus
        };
        db.HallSchedules.Add(schedSaturday);

        var menu = new Menu
        {
            MenuId = 1,
            MenuCode = "MN01",
            MenuName = "Thực Đơn Hoàng Gia",
            MenuType = MenuTypes.Standard,
            PricePerTable = 5_000_000m,
            DataStatusId = 1,
            DataStatus = existingStatus,
            StatusId = 201,
            Status = menuActive
        };
        db.Menus.Add(menu);

        var decor = new DecorPackage
        {
            GoiTrangTriID = 1,
            MaGoi = "DC01",
            TenGoi = "Gói Hoa Hồng",
            PhongCach = "Hiện đại",
            Gia = 10_000_000m,
            DataStatusId = 1,
            DataStatus = existingStatus,
            StatusId = 401,
            Status = decorActive
        };
        db.DecorPackages.Add(decor);
        await db.SaveChangesAsync();

        var pricingService = new HallPricingPolicyService();
        var statusService = new StatusService(db);
        var pricingOptions = Microsoft.Extensions.Options.Options.Create(new PricingPolicyOptions());
        var aiOptions = Microsoft.Extensions.Options.Options.Create(new AiServiceOptions());

        var controller = new RecommendationController(
            db,
            statusService,
            new FakeHttpClientFactory(),
            aiOptions,
            pricingOptions,
            NullLogger<RecommendationController>.Instance,
            pricingService
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        // Request on Saturday Evening with 20 tables
        var req = new RecommendationRequestDto
        {
            GuestCount = 200,
            TableCount = 20,
            ExpectedBudget = 300_000_000m,
            DesiredDate = saturday,
            DesiredShift = "Ca tối",
            DesiredStyle = "Hiện đại"
        };

        var response = await controller.SuggestTop3Combos(req) as OkObjectResult;
        Assert.NotNull(response);

        var val = response.Value as RecommendationResponseDto;
        Assert.NotNull(val);
        Assert.NotEmpty(val.TopCombos);

        var top = val.TopCombos[0];
        // On Saturday, hall price is +25%: 40,000,000 * 1.25 = 50,000,000!
        Assert.Equal(50_000_000m, top.HallRentalPrice);
        // Menu total: 5,000,000 * 20 = 100,000,000
        // Decor: 10,000,000
        // Total = 50,000,000 + 100,000,000 + 10,000,000 = 160,000,000
        Assert.Equal(160_000_000m, top.TotalEstimatedCost);
    }

    [Fact]
    public async Task GetHallAvailability_Returns_Shift_And_Weekend_Prices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "AvailPricingDb_" + Guid.NewGuid().ToString())
            .Options;

        using var db = new ApplicationDbContext(options);
        var existingStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" };
        var hallActive = new Status { StatusId = 101, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" };
        db.DataStatuses.Add(existingStatus);
        db.Statuses.Add(hallActive);

        var hall = new Hall
        {
            HallId = 1,
            HallCode = "H01",
            HallName = "Sảnh Diamond",
            RentalPrice = 40_000_000m,
            MinimumCapacity = 100,
            MaximumCapacity = 400,
            DataStatusId = 1,
            DataStatus = existingStatus,
            StatusId = 101,
            Status = hallActive
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();

        var pricingService = new HallPricingPolicyService();
        var statusService = new StatusService(db);
        var pricingOptions = Microsoft.Extensions.Options.Options.Create(new PricingPolicyOptions());
        var aiOptions = Microsoft.Extensions.Options.Options.Create(new AiServiceOptions());

        var controller = new RecommendationController(
            db,
            statusService,
            new FakeHttpClientFactory(),
            aiOptions,
            pricingOptions,
            NullLogger<RecommendationController>.Instance,
            pricingService
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        // 1. Check Saturday (weekend): 40M * 1.25 = 50M
        var saturday = DateTime.Today.AddDays(40);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);

        var satRes = await controller.GetHallAvailability(saturday) as OkObjectResult;
        Assert.NotNull(satRes);
        var satDto = satRes.Value as HallAvailabilityResponseDto;
        Assert.NotNull(satDto);
        var satHall = satDto.Halls[0];
        Assert.True(satHall.IsWeekend);
        Assert.Equal(50_000_000m, satHall.RentalPrice);
        Assert.Equal(50_000_000m, satHall.RentalPriceEvening);

        // 2. Check Tuesday (weekday): Noon = 40M, Eve = 40M * 1.15 = 46M
        var tuesday = DateTime.Today.AddDays(40);
        while (tuesday.DayOfWeek != DayOfWeek.Tuesday) tuesday = tuesday.AddDays(1);

        var tueRes = await controller.GetHallAvailability(tuesday) as OkObjectResult;
        Assert.NotNull(tueRes);
        var tueDto = tueRes.Value as HallAvailabilityResponseDto;
        Assert.NotNull(tueDto);
        var tueHall = tueDto.Halls[0];
        Assert.False(tueHall.IsWeekend);
        Assert.Equal(40_000_000m, tueHall.RentalPriceNoon);
        Assert.Equal(46_000_000m, tueHall.RentalPriceEvening);
    }

    private sealed class FakeHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new System.Net.Http.HttpClient();
    }
}

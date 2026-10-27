using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc13SqlServerFixture : IAsyncLifetime
{
    private readonly string _database = $"UC13_Test_{Guid.NewGuid():N}";
    private bool _owned;

    public int HallId { get; private set; }
    public int MenuId { get; private set; }
    public int DecorId { get; private set; }
    public decimal HallPrice { get; private set; } = 20_000_000m;
    public decimal MenuPrice { get; private set; } = 5_000_000m;
    public decimal DecorPrice { get; private set; } = 8_000_000m;
    public decimal ServicePrice { get; private set; } = 1_000_000m;
    public decimal DishOnePrice { get; private set; } = 1_500_000m;
    public decimal DishTwoPrice { get; private set; } = 2_500_000m;

    public ApplicationDbContext Context()
    {
        if (!_database.StartsWith("UC13_Test_", StringComparison.Ordinal) || _database.Length != 42)
            throw new InvalidOperationException("Unsafe test database name.");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = _database,
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
    }

    public async Task InitializeAsync()
    {
        await using var db = Context();
        _owned = await db.Database.EnsureCreatedAsync();
        if (!_owned) throw new InvalidOperationException("Refuse to use an existing database.");

        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = "EXISTING", DataStatusName = "Existing" });
        var statuses = new[]
        {
            (201, StatusGroups.Hall, HallStatusCodes.Active),
            (301, StatusGroups.HallSchedule, HallScheduleStatusCodes.Available),
            (302, StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked),
            (303, StatusGroups.HallSchedule, HallScheduleStatusCodes.Locked),
            (401, StatusGroups.Menu, BusinessStatusCodes.Active),
            (402, StatusGroups.Menu, BusinessStatusCodes.Inactive),
            (501, StatusGroups.Dish, BusinessStatusCodes.Active),
            (601, StatusGroups.DecorationPackage, BusinessStatusCodes.Active),
            (701, StatusGroups.Service, BusinessStatusCodes.Active),
            (801, StatusGroups.Booking, BookingStatusCodes.Pending),
            (802, StatusGroups.Booking, BookingStatusCodes.Confirmed),
            (803, StatusGroups.Booking, BookingStatusCodes.Preparing),
            (805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
            (901, StatusGroups.Contract, BusinessStatusCodes.Draft),
            (1002, StatusGroups.Payment, BusinessStatusCodes.Success)
        };
        foreach (var (id, group, code) in statuses)
            db.Statuses.Add(new Status { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code });

        var hall = new Hall
        {
            HallCode = "UC13-HALL",
            HallName = "Sảnh UC13",
            MinimumCapacity = 150,
            MaximumCapacity = 300,
            RentalPrice = HallPrice,
            StatusId = 201,
            DataStatusId = 1
        };
        var menu = new Menu
        {
            MenuCode = "UC13-MENU",
            MenuName = "Thực đơn UC13",
            MenuType = "STANDARD",
            PricePerTable = MenuPrice,
            StatusId = 401,
            DataStatusId = 1
        };
        var decor = new DecorPackage
        {
            MaGoi = "UC13-DECOR",
            TenGoi = "Trang trí UC13",
            PhongCach = "Hiện đại",
            Gia = DecorPrice,
            StatusId = 601,
            DataStatusId = 1
        };
        var service = new ServiceItem
        {
            MaDichVu = "DV001",
            TenDichVu = "Dịch vụ UC13",
            Gia = ServicePrice,
            StatusId = 701,
            DataStatusId = 1
        };
        var dishes = new[]
        {
            new Dish { DishCode = "MON01", DishName = "Món 1", Category = "Khai vị", Price = DishOnePrice, StatusId = 501, DataStatusId = 1 },
            new Dish { DishCode = "MON02", DishName = "Món 2", Category = "Chính", Price = DishTwoPrice, StatusId = 501, DataStatusId = 1 }
        };
        db.Halls.Add(hall);
        db.Menus.Add(menu);
        db.DecorPackages.Add(decor);
        db.ServiceItems.Add(service);
        db.Dishes.AddRange(dishes);
        await db.SaveChangesAsync();

        HallId = hall.HallId;
        MenuId = menu.MenuId;
        DecorId = decor.GoiTrangTriID;
        DishOneId = dishes[0].DishId;
        DishTwoId = dishes[1].DishId;
    }

    public int DishOneId { get; private set; }
    public int DishTwoId { get; private set; }

    public Task DisposeAsync()
    {
        if (!_owned) return Task.CompletedTask;
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = _database,
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        db.Database.EnsureDeleted();
        SqlConnection.ClearAllPools();
        return Task.CompletedTask;
    }

    public async Task<int> AddBooking(
        int scheduleId,
        int customerId,
        int statusId,
        DateTime bookedAt,
        bool withContract = false,
        bool withPayment = false)
    {
        await using var db = Context();
        var booking = new WeddingBooking
        {
            BookingCode = $"DT-{bookedAt:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            CustomerId = customerId,
            HallScheduleId = scheduleId,
            GuestCount = 200,
            StatusId = statusId,
            BookedAt = bookedAt
        };
        db.WeddingBookings.Add(booking);
        await db.SaveChangesAsync();
        if (withContract || withPayment)
        {
            var contract = new Contract
            {
                BookingId = booking.BookingId,
                ContractCode = $"HD-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                TotalValue = 100_000_000m,
                RequiredDepositAmount = 30_000_000m,
                StatusId = 901
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            if (withPayment)
            {
                db.Payments.Add(new Payment
                {
                    ContractId = contract.ContractId,
                    Amount = 10_000_000m,
                    PaymentDate = bookedAt.AddHours(1),
                    PaymentType = "Đặt cọc",
                    PaymentMethod = "Tiền mặt",
                    StatusId = 1002
                });
                await db.SaveChangesAsync();
            }
        }
        return booking.BookingId;
    }

    public async Task<(int ScheduleId, int CustomerId)> AddScheduleWithCustomer(
        DateTime date,
        string shift,
        int scheduleStatusId,
        string phone)
    {
        await using var db = Context();
        var schedule = new HallSchedule
        {
            HallId = HallId,
            Date = date,
            Shift = shift,
            StatusId = scheduleStatusId,
            DataStatusId = 1
        };
        var customer = new Customer
        {
            CustomerCode = $"KH-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            FullName = "Khách UC13",
            PhoneNumber = phone,
            Email = $"{phone}@example.invalid",
            CreatedAt = DateTime.Now,
            DataStatusId = 1
        };
        db.HallSchedules.Add(schedule);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (schedule.HallScheduleId, customer.CustomerId);
    }
}

public sealed class Uc13BookingReservationTests : IClassFixture<Uc13SqlServerFixture>
{
    private readonly Uc13SqlServerFixture _fixture;
    private static int _dateOffset;

    public Uc13BookingReservationTests(Uc13SqlServerFixture fixture) => _fixture = fixture;

    private RecommendationController Controller(ApplicationDbContext db) =>
        new(db, new StatusService(db), new EmptyHttpClientFactory(),
            Microsoft.Extensions.Options.Options.Create(new AiServiceOptions()), NullLogger<RecommendationController>.Instance);

    private RegisterPublicBookingDto Request(string phone, DateTime eventDate, string? shift = "Ca tối")
    {
        var suffix = phone[^4..];
        return new RegisterPublicBookingDto
        {
            CustomerName = $"Khách {suffix}",
            PhoneNumber = phone,
            Email = $"{phone}@example.invalid",
            HallId = _fixture.HallId,
            EventDate = eventDate,
            Shift = shift,
            MenuId = _fixture.MenuId,
            DecorationPackageId = _fixture.DecorId,
            GuestCount = 200,
            TableCount = 20,
            SpareTableCount = 0,
            ServiceCodes = ["DV001"]
        };
    }

    private static DateTime NextDate() =>
        DateTime.Today.AddDays(60 + Interlocked.Increment(ref _dateOffset));

    [Fact]
    public async Task ValidRegistration_CreatesPendingBooking_WithServerPricingAndLookupCode()
    {
        var date = NextDate();
        var phone = "0901000001";
        await using var db = _fixture.Context();

        var result = await Controller(db).RegisterPublicBooking(Request(phone, date));

        var ok = Assert.IsType<OkObjectResult>(result);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var code = json.RootElement.GetProperty("bookingCode").GetString();
        Assert.Matches(@"^DT-\d{8}-[A-Z0-9]{8}$", code);
        Assert.Equal(BookingStatusCodes.Pending, json.RootElement.GetProperty("statusCode").GetString());
        Assert.True(json.RootElement.GetProperty("holdExpiresAt").GetDateTime() > DateTime.Now.AddHours(47));

        await using var verify = _fixture.Context();
        var booking = await verify.WeddingBookings
            .Include(item => item.Status)
            .Include(item => item.HallSchedule)
            .SingleAsync(item => item.BookingCode == code);
        Assert.Equal(BookingStatusCodes.Pending, booking.Status.StatusCode);
        Assert.Equal(_fixture.MenuId, booking.MenuId);
        Assert.Equal(_fixture.MenuPrice, booking.FinalMenuPrice);
        Assert.Equal(_fixture.HallPrice + _fixture.MenuPrice * 20
            + _fixture.DecorPrice + _fixture.ServicePrice, booking.EstimatedTotal);
        Assert.Equal("Ca tối", booking.HallSchedule.Shift);
        Assert.Single(verify.WeddingBookingServices.Where(item => item.BookingId == booking.BookingId));
    }

    [Fact]
    public async Task CustomMenuPrice_IsRecomputedFromDishIds_AndClientPriceIgnored()
    {
        var date = NextDate();
        var request = Request("0901000002", date);
        request.MenuId = null; // custom mode: no standard menu reference
        request.CustomMenuName = "Thực đơn tự chọn";
        request.CustomMenuPricePerTable = 1m; // client cố tình gửi giá thấp
        request.CustomMenuDishIds = [_fixture.DishOneId, _fixture.DishTwoId];
        await using var db = _fixture.Context();

        var result = await Controller(db).RegisterPublicBooking(request);

        Assert.IsType<OkObjectResult>(result);
        await using var verify = _fixture.Context();
        var booking = await verify.WeddingBookings.OrderByDescending(item => item.BookingId).FirstAsync();
        var expectedMenu = _fixture.DishOnePrice + _fixture.DishTwoPrice;
        Assert.Null(booking.MenuId);
        Assert.Equal(expectedMenu, booking.FinalMenuPrice);
        Assert.Equal(_fixture.HallPrice + expectedMenu * 20
            + _fixture.DecorPrice + _fixture.ServicePrice, booking.EstimatedTotal);
    }

    [Fact]
    public async Task StandardMenu_AndCustomDishIds_Together_AreRejected_WithoutPersisting()
    {
        var date = NextDate();
        var phone = "0901000005";
        var request = Request(phone, date);
        request.CustomMenuName = "Thực đơn tự chọn";
        request.CustomMenuDishIds = [_fixture.DishOneId];

        await using var db = _fixture.Context();
        var result = await Controller(db).RegisterPublicBooking(request);

        Assert.IsType<BadRequestObjectResult>(result);
        await using var verify = _fixture.Context();
        Assert.False(await verify.Customers.AnyAsync(item => item.PhoneNumber == phone));
        Assert.False(await verify.WeddingBookings.AnyAsync(item => item.Customer.PhoneNumber == phone));
    }

    [Fact]
    public async Task InvalidPhone_OrMissingCustomDishIds_IsRejected()
    {
        await using var db = _fixture.Context();
        var controller = Controller(db);

        var badPhone = Request("12345", NextDate());
        Assert.IsType<BadRequestObjectResult>(await controller.RegisterPublicBooking(badPhone));

        var noDishIds = Request("0901000003", NextDate());
        noDishIds.CustomMenuName = "Thực đơn tự chọn";
        noDishIds.CustomMenuPricePerTable = 10_000m;
        Assert.IsType<BadRequestObjectResult>(await controller.RegisterPublicBooking(noDishIds));

        var badService = Request("0901000004", NextDate());
        badService.ServiceCodes = ["KHONG-TON-TAI"];
        Assert.IsType<BadRequestObjectResult>(await controller.RegisterPublicBooking(badService));
    }

    [Fact]
    public async Task ConcurrentRequests_ForSameSlot_CreateExactlyOneBooking()
    {
        var date = NextDate();
        await using var firstDb = _fixture.Context();
        await using var secondDb = _fixture.Context();

        var first = Controller(firstDb).RegisterPublicBooking(Request("0901000010", date));
        var second = Controller(secondDb).RegisterPublicBooking(Request("0901000011", date));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());

        await using var verify = _fixture.Context();
        var bookings = await verify.WeddingBookings
            .Include(item => item.Status)
            .Include(item => item.HallSchedule)
            .Where(item => item.HallSchedule.Date == date && item.HallSchedule.HallId == _fixture.HallId)
            .ToListAsync();
        Assert.Single(bookings);
        Assert.Equal(BookingStatusCodes.Pending, bookings[0].Status.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequests_ForExistingAvailableSchedule_CreateExactlyOneBooking()
    {
        var date = NextDate();
        int scheduleId;
        await using (var setup = _fixture.Context())
        {
            var schedule = new HallSchedule
            {
                HallId = _fixture.HallId,
                Date = date,
                Shift = "Ca tối",
                StatusId = 301,
                DataStatusId = 1
            };
            setup.HallSchedules.Add(schedule);
            await setup.SaveChangesAsync();
            scheduleId = schedule.HallScheduleId;
        }

        await using var firstDb = _fixture.Context();
        await using var secondDb = _fixture.Context();
        var firstRequest = Request("0901000012", date);
        firstRequest.HallScheduleId = scheduleId;
        var secondRequest = Request("0901000013", date);
        secondRequest.HallScheduleId = scheduleId;

        var results = await Task.WhenAll(
            Controller(firstDb).RegisterPublicBooking(firstRequest),
            Controller(secondDb).RegisterPublicBooking(secondRequest));

        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());
        await using var verify = _fixture.Context();
        Assert.Single(await verify.WeddingBookings.Where(item => item.HallScheduleId == scheduleId).ToListAsync());
        Assert.Equal(302, (await verify.HallSchedules.FindAsync(scheduleId))!.StatusId);
    }

    [Fact]
    public async Task Expiration_CancelsOnlyEligiblePending_AndReleasesBookedSchedule()
    {
        var old = DateTime.Now.AddHours(-49);
        var (pendingSchedule, pendingCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(250), "Ca trưa", 302, "0901000020");
        var pendingId = await _fixture.AddBooking(pendingSchedule, pendingCustomer, 801, old);

        var (confirmedSchedule, confirmedCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(251), "Ca trưa", 302, "0901000021");
        var confirmedId = await _fixture.AddBooking(confirmedSchedule, confirmedCustomer, 802, old);

        var (contractSchedule, contractCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(252), "Ca trưa", 302, "0901000022");
        var contractId = await _fixture.AddBooking(contractSchedule, contractCustomer, 801, old, withContract: true);

        var (paymentSchedule, paymentCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(253), "Ca trưa", 302, "0901000023");
        var paymentId = await _fixture.AddBooking(paymentSchedule, paymentCustomer, 801, old, withPayment: true);

        var (lockedSchedule, lockedCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(254), "Ca trưa", 303, "0901000024");
        var lockedBookingId = await _fixture.AddBooking(lockedSchedule, lockedCustomer, 801, old);

        var (freshSchedule, freshCustomer) = await _fixture.AddScheduleWithCustomer(
            DateTime.Today.AddDays(255), "Ca trưa", 302, "0901000025");
        var freshId = await _fixture.AddBooking(freshSchedule, freshCustomer, 801, DateTime.Now.AddHours(-1));

        await using var db = _fixture.Context();
        var expired = await BookingHoldExpirationService.ExpireDueAsync(db, DateTime.Now);
        Assert.Equal(2, expired); // pending thường + pending trên lịch LOCKED

        await using var verify = _fixture.Context();
        Assert.Equal(805, (await verify.WeddingBookings.FindAsync(pendingId))!.StatusId);
        Assert.Equal(805, (await verify.WeddingBookings.FindAsync(lockedBookingId))!.StatusId);
        Assert.Equal(802, (await verify.WeddingBookings.FindAsync(confirmedId))!.StatusId);
        Assert.Equal(801, (await verify.WeddingBookings.FindAsync(contractId))!.StatusId);
        Assert.Equal(801, (await verify.WeddingBookings.FindAsync(paymentId))!.StatusId);
        Assert.Equal(801, (await verify.WeddingBookings.FindAsync(freshId))!.StatusId);

        Assert.Equal(301, (await verify.HallSchedules.FindAsync(pendingSchedule))!.StatusId);
        Assert.Equal(303, (await verify.HallSchedules.FindAsync(lockedSchedule))!.StatusId);
        Assert.Equal(302, (await verify.HallSchedules.FindAsync(confirmedSchedule))!.StatusId);
        Assert.Equal(302, (await verify.HallSchedules.FindAsync(contractSchedule))!.StatusId);
    }

    [Fact]
    public async Task LockedSchedule_IsRejectedForNewPublicBooking()
    {
        var date = NextDate();
        await using (var db = _fixture.Context())
        {
            db.HallSchedules.Add(new HallSchedule
            {
                HallId = _fixture.HallId,
                Date = date,
                Shift = "Ca tối",
                StatusId = 303,
                DataStatusId = 1
            });
            await db.SaveChangesAsync();
        }

        await using var context = _fixture.Context();
        var result = await Controller(context).RegisterPublicBooking(Request("0901000030", date));
        Assert.IsType<ConflictObjectResult>(result);
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

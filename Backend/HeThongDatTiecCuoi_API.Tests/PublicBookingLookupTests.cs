using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PublicBookingLookupTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly ApplicationDbContext _db;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public PublicBookingLookupTests()
    {
        _db = CreateDbContext();
        SeedBooking();
    }

    [Fact]
    public void LookupRequestDto_ValidationAcceptsLowercaseBookingCode()
    {
        var request = Request("dt-20261022-abcd1234", "0901234567");
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.True(isValid, string.Join("; ", validationResults.Select(result => result.ErrorMessage)));
    }

    [Fact]
    public async Task Lookup_WithMatchingCodeAndPhone_ReturnsOnlyCustomerFacingBookingAndActualContractPayments()
    {
        var result = await Controller("192.0.2.1").Lookup(Request("dt-20261022-abcd1234", "0901234567"));

        var response = Assert.IsType<OkObjectResult>(result);
        var data = ReadData(response.Value!);
        Assert.Equal("DT-20261022-ABCD1234", data.BookingCode);
        Assert.Equal(BookingStatusCodes.Confirmed, data.StatusCode);
        Assert.Equal(1000m, data.ContractTotal);
        Assert.Equal(300m, data.DepositPaid);
        Assert.Equal(400m, data.TotalPaid);
        Assert.Equal(600m, data.RemainingBalance);
        Assert.DoesNotContain("PhoneNumber", data.GetType().GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Email", data.GetType().GetProperties().Select(p => p.Name));
        Assert.Equal(2, data.SuccessfulPayments.Count);
    }

    [Theory]
    [InlineData("DT-20261022-ABCD1234", "0900000000")]
    [InlineData("DT-20261022-ABCD9999", "0901234567")]
    public async Task Lookup_WithMismatchedProof_ReturnsSameNotFoundResponse(string code, string phone)
    {
        var result = await Controller("192.0.2.2").Lookup(Request(code, phone));

        var response = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("không chính xác", response.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_WithoutContract_ShowsEstimateAndDoesNotInventBalance()
    {
        var booking = await _db.WeddingBookings.Include(item => item.Contract).SingleAsync();
        _db.Payments.RemoveRange(booking.Contract!.Payments);
        _db.Contracts.Remove(booking.Contract!);
        booking.Contract = null;
        await _db.SaveChangesAsync();

        var result = await Controller("192.0.2.5").Lookup(Request("DT-20261022-ABCD1234", "0901234567"));

        var data = ReadData(Assert.IsType<OkObjectResult>(result).Value!);
        Assert.False(data.HasContract);
        Assert.Equal(1200m, data.EstimatedTotal);
        Assert.Null(data.ContractTotal);
        Assert.Null(data.RemainingBalance);
        Assert.Empty(data.SuccessfulPayments);
    }

    [Fact]
    public async Task Lookup_WithMalformedProof_ReturnsBadRequestAndNoStoreHeaders()
    {
        var controller = Controller("192.0.2.3");
        controller.ModelState.AddModelError(nameof(PublicBookingLookupRequestDto.BookingCode), "Invalid booking code.");
        var result = await controller.Lookup(Request("bad", ""));

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Equal("no-store, no-cache, max-age=0", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Lookup_LimitsSixthFailedProofWithinFifteenMinuteWindow()
    {
        var controller = Controller("192.0.2.4");
        for (var i = 0; i < 5; i++)
            Assert.IsType<NotFoundObjectResult>(await controller.Lookup(Request("DT-20261022-ABCD1234", "0900000000")));

        var limited = Assert.IsType<ObjectResult>(await controller.Lookup(Request("DT-20261022-ABCD1234", "0900000000")));
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Lookup_SuccessDoesNotResetFailedAttemptLimit()
    {
        var ip = "192.0.2.6";
        Assert.IsType<NotFoundObjectResult>(await Controller(ip).Lookup(Request("DT-20261022-ABCD1234", "0900000000")));
        Assert.IsType<OkObjectResult>(await Controller(ip).Lookup(Request("DT-20261022-ABCD1234", "0901234567")));

        for (var i = 0; i < 4; i++)
            Assert.IsType<NotFoundObjectResult>(await Controller(ip).Lookup(Request("DT-20261022-ABCD1234", "0900000000")));

        var limited = Assert.IsType<ObjectResult>(await Controller(ip).Lookup(Request("DT-20261022-ABCD1234", "0900000000")));
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Lookup_ConcurrentProofsReserveOnlyFiveAttemptsPerIp()
    {
        const string ip = "192.0.2.7";
        const int concurrentRequests = 15;
        var interceptor = new FirstFiveLookupsBarrier();
        using var startRequests = new ManualResetEventSlim();

        async Task<IActionResult> SendLookupAsync(bool validProof)
        {
            using var db = CreateDbContext(interceptor);
            startRequests.Wait();
            var phone = validProof ? "0901234567" : "0900000000";
            return await Controller(db, ip).Lookup(Request("DT-20261022-ABCD1234", phone));
        }

        var firstBatch = Enumerable.Range(0, 5)
            .Select(_ => Task.Factory.StartNew(() => SendLookupAsync(true), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap())
            .ToArray();

        startRequests.Set();
        await interceptor.FirstFiveReached.WaitAsync(TimeSpan.FromSeconds(10));

        var additionalBatch = Enumerable.Range(0, concurrentRequests - 5)
            .Select(_ => Task.Factory.StartNew(() => SendLookupAsync(false), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap())
            .ToArray();

        IActionResult[] additionalResults;
        try
        {
            additionalResults = await Task.WhenAll(additionalBatch).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            interceptor.ReleaseFirstFive();
        }
        var firstResults = await Task.WhenAll(firstBatch).WaitAsync(TimeSpan.FromSeconds(10));
        var allResults = firstResults.Concat(additionalResults).ToArray();

        Assert.Equal(5, allResults.OfType<OkObjectResult>().Count());
        Assert.Equal(concurrentRequests - 5, allResults.OfType<ObjectResult>()
            .Count(result => result.StatusCode == StatusCodes.Status429TooManyRequests));
    }

    [Fact]
    public async Task LegacyBookingListRead_IsRetired()
    {
        var legacy = new MyBookingsController();

        var result = legacy.GetMyBookings();

        Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    private PublicBookingController Controller(string ip) => Controller(_db, ip);

    private PublicBookingController Controller(ApplicationDbContext context, string ip)
    {
        var controller = new PublicBookingController(context, new StatusService(context), _cache,
            NullLogger<PublicBookingController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        return controller;
    }

    private ApplicationDbContext CreateDbContext(IMaterializationInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot);
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new ApplicationDbContext(options.Options);
    }

    private static PublicBookingLookupRequestDto Request(string code, string phone) => new()
    {
        BookingCode = code,
        PhoneNumber = phone
    };

    private static PublicBookingDetailDto ReadData(object wrapper) =>
        (PublicBookingDetailDto)wrapper.GetType().GetProperty("data")!.GetValue(wrapper)!;

    private void SeedBooking()
    {
        _db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" });
        var bookingStatus = new Status { StatusId = 802, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };
        var scheduleStatus = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = "BOOKED", StatusName = "Đã đặt" };
        var hallStatus = new Status { StatusId = 201, StatusGroup = StatusGroups.Hall, StatusCode = "ACTIVE", StatusName = "Hoạt động" };
        var contractStatus = new Status { StatusId = 902, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Hiệu lực" };
        var success = new Status { StatusId = 1002, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" };
        var failed = new Status { StatusId = 1003, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Failed, StatusName = "Thất bại" };
        _db.Statuses.AddRange(bookingStatus, scheduleStatus, hallStatus, contractStatus, success, failed);

        var booking = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-20261022-ABCD1234",
            GuestCount = 200,
            EstimatedTotal = 1200m,
            Status = bookingStatus,
            Customer = new Customer { CustomerId = 1, CustomerCode = "KH-TEST", FullName = "Test Customer", PhoneNumber = "0901234567", Email = "private@example.invalid", DataStatusId = 1 },
            HallSchedule = new HallSchedule
            {
                HallScheduleId = 1, Date = new DateTime(2027, 1, 15), Shift = "Ca trưa", Status = scheduleStatus, DataStatusId = 1,
                Hall = new Hall { HallId = 1, HallCode = "S-TEST", HallName = "Test Hall", MaximumCapacity = 300, RentalPrice = 500m, Status = hallStatus, DataStatusId = 1 }
            },
            Contract = new Contract
            {
                ContractId = 1, ContractCode = "HD-TEST", TotalValue = 1000m, RequiredDepositAmount = 200m,
                PaymentTerms = "Terms saved on contract", Status = contractStatus,
                Payments =
                [
                    new Payment { PaymentId = 1, PaymentType = PaymentTypes.Deposit, Amount = 300m, PaymentDate = new DateTime(2026, 10, 23), Status = success },
                    new Payment { PaymentId = 2, PaymentType = PaymentTypes.Installment, Amount = 100m, PaymentDate = new DateTime(2026, 10, 24), Status = success },
                    new Payment { PaymentId = 3, PaymentType = PaymentTypes.Settlement, Amount = 50m, PaymentDate = new DateTime(2026, 10, 25), Status = failed }
                ]
            }
        };
        _db.WeddingBookings.Add(booking);
        _db.SaveChanges();
    }

    private sealed class FirstFiveLookupsBarrier : IMaterializationInterceptor
    {
        private readonly TaskCompletionSource _firstFiveReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _releaseFirstFive = new();
        private int _arrivals;

        public Task FirstFiveReached => _firstFiveReached.Task;

        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            if (entity is not WeddingBooking)
                return entity;

            var arrival = Interlocked.Increment(ref _arrivals);
            if (arrival == 5)
                _firstFiveReached.TrySetResult();
            if (arrival <= 5 && !_releaseFirstFive.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Timed out waiting for the concurrent lookup batch.");
            return entity;
        }

        public void ReleaseFirstFive() => _releaseFirstFive.Set();
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }
}

public sealed class PublicBookingLookupApiRouteTests : IClassFixture<PublicBookingLookupApiFactory>
{
    private readonly HttpClient _client;

    public PublicBookingLookupApiRouteTests(PublicBookingLookupApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task LookupRoute_IsAnonymous_AndValidationResponseIsNotCached()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/public/bookings/lookup", new { bookingCode = "bad", phoneNumber = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task LookupRoute_LimitsTotalAttemptsPerMinute()
    {
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            last?.Dispose();
            last = await _client.PostAsJsonAsync("/api/v1/public/bookings/lookup", new { bookingCode = "bad", phoneNumber = "" });
        }

        using (last)
            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [Fact]
    public async Task LegacyGet_DoesNotDiscloseBookings()
    {
        using var response = await _client.GetAsync("/api/my-bookings?userId=1");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}

public sealed class PublicBookingLookupApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
}

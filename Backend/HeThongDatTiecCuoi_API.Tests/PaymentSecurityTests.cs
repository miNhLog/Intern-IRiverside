using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AiChat;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class PaymentSecurityTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(ApplicationDbContext Db, Contract TestContract)> SetupContractAsync()
    {
        var options = CreateOptions();
        var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        var activeContractStatus = new Status { StatusId = 301, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Có hiệu lực" };
        var successPaymentStatus = new Status { StatusId = 401, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" };
        var bookingStatus = new Status { StatusId = 101, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };

        db.DataStatuses.Add(dataStatus);
        db.Statuses.AddRange(activeContractStatus, successPaymentStatus, bookingStatus);

        var customer = new Customer { CustomerId = 1, FullName = "Trần Khách", PhoneNumber = "0988776655", DataStatusId = 1 };
        var booking = new WeddingBooking { BookingId = 10, CustomerId = 1, Customer = customer, PaidAmount = 10000000, StatusId = 101, Status = bookingStatus, BookingCode = "DT-TEST-001" };

        var contract = new Contract
        {
            ContractId = 100,
            ContractCode = "HD-TEST-001",
            BookingId = 10,
            Booking = booking,
            TotalValue = 100000000, // 100 triệu
            CreatedDate = DateTime.Now,
            StatusId = 301,
            Status = activeContractStatus
        };

        // Thanh toán cọc 10 triệu thành công trước đó
        var depositPayment = new Payment
        {
            PaymentId = 1,
            ContractId = 100,
            Contract = contract,
            PaymentType = PaymentTypes.Deposit,
            Amount = 10000000,
            StatusId = 401,
            Status = successPaymentStatus,
            PaymentDate = DateTime.Now.AddDays(-5),
            PaymentMethod = "VIETQR_PAYOS",
            TransactionCode = "COC-001"
        };

        db.Customers.Add(customer);
        db.WeddingBookings.Add(booking);
        booking.Contract = contract;
        db.Contracts.Add(contract);
        db.Payments.Add(depositPayment);
        await db.SaveChangesAsync();

        return (db, contract);
    }

    [Fact]
    public async Task CreateContractPayment_OverRemainingBalance_ThrowsInvalidOperationException()
    {
        var (db, contract) = await SetupContractAsync();
        // Remaining balance is: 100m - 10m = 90m.
        // Attacker or manager tries to create PayOS link for 95m (> 90m remaining)
        var payOsClient = new PayOSClient("test-cid", "test-key", "test-checksum");
        var payOsOptions = Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 10000 });
        var service = new PayOSPaymentService(db, payOsClient, payOsOptions, new StubStatusService(), null!, null!, null!, null!, NullLogger<PayOSPaymentService>.Instance);

        var request = new CreateContractPayOsLinkRequestDto
        {
            ContractId = contract.ContractId,
            Amount = 95000000, // 95 triệu > 90 triệu
            PaymentType = PaymentTypes.Installment
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(request));
        Assert.Contains("vượt quá số dư công nợ còn lại", ex.Message);
    }

    [Fact]
    public async Task CreateContractPayment_BelowMinimum2000_ThrowsInvalidOperationException()
    {
        var (db, contract) = await SetupContractAsync();
        var payOsClient = new PayOSClient("test-cid", "test-key", "test-checksum");
        var payOsOptions = Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 10000 });
        var service = new PayOSPaymentService(db, payOsClient, payOsOptions, new StubStatusService(), null!, null!, null!, null!, NullLogger<PayOSPaymentService>.Instance);

        var request = new CreateContractPayOsLinkRequestDto
        {
            ContractId = contract.ContractId,
            Amount = 1000, // < 2000 đ
            PaymentType = PaymentTypes.Installment
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateContractPaymentAsync(request));
        Assert.Contains("tối thiểu là 2.000 đ", ex.Message);
    }

    [Fact]
    public async Task RecordCashPayment_OverRemainingBalance_ReturnsBadRequest()
    {
        var (db, contract) = await SetupContractAsync();
        var controller = new ManagerPaymentController(
            db, new StubStatusService(), null!, NullLogger<ManagerPaymentController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "99"),
                    new Claim(ClaimTypes.Role, RoleNames.Manager)
                }))
            }
        };

        var request = new RecordCashPaymentRequestDto
        {
            ContractId = contract.ContractId,
            Amount = 120000000, // 120 triệu > 90 triệu
            PaymentType = PaymentTypes.Installment,
            Note = "Test"
        };

        var result = await controller.RecordCashPayment(request, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task RecordCashPayment_ValidAmount_Succeeds()
    {
        var (db, contract) = await SetupContractAsync();
        var controller = new ManagerPaymentController(
            db, new StubStatusService(), null!, NullLogger<ManagerPaymentController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "99"),
                    new Claim(ClaimTypes.Role, RoleNames.Manager)
                }))
            }
        };

        var request = new RecordCashPaymentRequestDto
        {
            ContractId = contract.ContractId,
            Amount = 40000000, // 40 triệu <= 90 triệu
            PaymentType = PaymentTypes.Installment,
            Note = "Đợt 2 tiền mặt"
        };

        var result = await controller.RecordCashPayment(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var booking = await db.WeddingBookings.FindAsync(10);
        Assert.Equal(50000000, booking!.PaidAmount); // 10m cọc + 40m đợt 2
    }

    [Fact]
    public async Task AiAssistant_MessageOver2000Chars_ReturnsBadRequest()
    {
        var stubAiService = new StubAiChatService();
        var controller = new AiAssistantController(stubAiService, NullLogger<AiAssistantController>.Instance);

        var hugeMessage = new string('A', 2500); // 2500 ký tự > 2000
        var request = new AiChatSendRequestDto { Message = hugeMessage };

        var result = await controller.Send(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CreateCustomerContractPayment_InvalidPhone_ThrowsUnauthorizedAccessException()
    {
        var (db, contract) = await SetupContractAsync();
        var payOsClient = new PayOSClient("test-cid", "test-key", "test-checksum");
        var payOsOptions = Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 10000 });
        var service = new PayOSPaymentService(db, payOsClient, payOsOptions, new StubStatusService(), null!, null!, null!, null!, NullLogger<PayOSPaymentService>.Instance);

        var request = new CreateCustomerContractPaymentRequestDto
        {
            BookingId = contract.BookingId,
            BookingCode = contract.Booking.BookingCode,
            PhoneNumber = "0999999999" // Wrong phone
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateCustomerContractPaymentAsync(request, null));
        Assert.Contains("không khớp", ex.Message);
    }

    [Fact]
    public async Task CreateCustomerContractPayment_AmountExceedsRemainingDebt_ThrowsInvalidOperationException()
    {
        var (db, contract) = await SetupContractAsync();
        var payOsClient = new PayOSClient("test-cid", "test-key", "test-checksum");
        var payOsOptions = Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 10000 });
        var service = new PayOSPaymentService(db, payOsClient, payOsOptions, new StubStatusService(), null!, null!, null!, null!, NullLogger<PayOSPaymentService>.Instance);

        var request = new CreateCustomerContractPaymentRequestDto
        {
            BookingId = contract.BookingId,
            BookingCode = contract.Booking.BookingCode,
            PhoneNumber = "0988776655", // Correct phone
            Amount = 95000000 // > 90m debt
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerContractPaymentAsync(request, null));
        Assert.Contains("vượt quá số dư công nợ còn lại", ex.Message);
    }

    private sealed class StubStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(401);

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(new Status { StatusId = 401, StatusGroup = statusGroup, StatusCode = statusCode });
    }

    private sealed class StubAiChatService : IAiChatService
    {
        public Task<AiChatReplyDto> SendChatAsync(string message, List<object> history, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiChatReplyDto { Reply = "Ok" });

        public Task<AiChatEntitiesDto?> ExtractEntitiesAsync(List<object> history, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiChatEntitiesDto?>(new AiChatEntitiesDto());
    }
}

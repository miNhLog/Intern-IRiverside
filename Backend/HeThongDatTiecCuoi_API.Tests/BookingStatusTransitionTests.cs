using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class BookingStatusTransitionTests
{
    private static readonly DateTime PastEvent = new(2026, 1, 1);
    private static readonly DateTime FutureEvent = new(2026, 1, 3);
    private static readonly DateTime BusinessNow = new(2026, 1, 2, 8, 0, 0);

    [Fact]
    public async Task TC01_PendingToConfirmed_IsAllowedWithoutDeposit()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Pending, PastEvent);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Confirmed);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Confirmed, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
        Assert.Equal(0, (await fixture.BookingAsync()).PaidAmount);
    }

    [Theory]
    [InlineData(BookingStatusCodes.Pending, BookingStatusCodes.Preparing)]
    [InlineData(BookingStatusCodes.Pending, BookingStatusCodes.Completed)]
    [InlineData(BookingStatusCodes.Confirmed, BookingStatusCodes.Completed)]
    [InlineData(BookingStatusCodes.Completed, BookingStatusCodes.Confirmed)]
    [InlineData(BookingStatusCodes.Cancelled, BookingStatusCodes.Confirmed)]
    [InlineData(BookingStatusCodes.Cancelled, BookingStatusCodes.Preparing)]
    public void TC02_03_05_08_09_10_InvalidEdges_AreRejected(string from, string to)
    {
        var result = BookingStatusBusinessRules.ValidateTransition(from, to);

        Assert.False(result.Allowed);
        Assert.Equal(BookingStatusRuleFailureKind.InvalidTransition, result.FailureKind);
        Assert.Equal("ERR_BOOKING_STATUS_TRANSITION_INVALID", result.Code);
    }

    [Fact]
    public async Task TC04_ConfirmedToPreparing_IsAllowedWhenBusinessConditionsPass()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Confirmed, FutureEvent);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Preparing);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Preparing, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
    }

    [Fact]
    public async Task TC06_PreparingToCompleted_IsAllowedAfterEventEnded()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Preparing, PastEvent);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Completed);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Completed, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
        Assert.Equal(1, fixture.Coordination.Synchronizations);
    }

    [Fact]
    public async Task TC07_PreparingToCompleted_IsRejectedBeforeEventEnds()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Preparing, FutureEvent);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Completed);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Preparing, await fixture.BookingStatusAsync());
        Assert.Empty(fixture.Db.AuditLogs);
        Assert.Equal(0, fixture.Coordination.Synchronizations);
    }

    [Fact]
    public async Task TC11_UnpaidBooking_CanBeCancelledAndReleasesUnusedSchedule()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Confirmed, FutureEvent);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Cancelled, "Khách thay đổi kế hoạch");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Cancelled, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Available, await fixture.ScheduleStatusAsync());
        Assert.Equal("Khách thay đổi kế hoạch", (await fixture.BookingAsync()).CancellationReason);
        Assert.Equal(1, fixture.Coordination.Synchronizations);
    }

    [Fact]
    public async Task TC12_PaidBooking_CannotUseOrdinaryCancellation()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Confirmed, FutureEvent);
        (await fixture.Db.WeddingBookings.SingleAsync()).PaidAmount = 1_000_000;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.UpdateAsync(BookingStatusCodes.Cancelled);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Confirmed, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
        Assert.Empty(fixture.Db.AuditLogs);
    }

    [Fact]
    public async Task TC13_ActiveContract_BlocksOrdinaryCancellation()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Confirmed, FutureEvent, ContractStatusCodes.Active);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Cancelled);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Confirmed, await fixture.BookingStatusAsync());
        Assert.Equal(ContractStatusCodes.Active, await fixture.ContractStatusAsync());
    }

    [Fact]
    public async Task TC14_SameState_IsIdempotentWithoutAuditOrSynchronization()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Confirmed, FutureEvent);

        var result = Assert.IsType<OkObjectResult>(await fixture.UpdateAsync(BookingStatusCodes.Confirmed));

        Assert.Contains("unchanged = True", result.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Db.AuditLogs);
        Assert.Equal(0, fixture.Coordination.Synchronizations);
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
    }

    [Fact]
    public async Task TC15_InvalidTransition_DoesNotChangeRelatedModules()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Pending, FutureEvent, ContractStatusCodes.Draft, withChecklist: true);
        var checklistVersion = (await fixture.Db.PartyChecklists.SingleAsync()).Version;

        var result = await fixture.UpdateAsync(BookingStatusCodes.Completed);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Pending, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Booked, await fixture.ScheduleStatusAsync());
        Assert.Equal(ContractStatusCodes.Draft, await fixture.ContractStatusAsync());
        Assert.Equal(checklistVersion, (await fixture.Db.PartyChecklists.SingleAsync()).Version);
        Assert.Equal(0, fixture.Coordination.Synchronizations);
        Assert.Empty(fixture.Db.AuditLogs);
    }

    [Fact]
    public async Task TC16_ConcurrentRequests_CannotSkipStateMachine()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        await using (var seed = await Fixture.CreateAsync(
            BookingStatusCodes.Pending, PastEvent, databaseName: databaseName, root: root)) { }
        await using var first = await Fixture.OpenAsync(databaseName, root);
        await using var second = await Fixture.OpenAsync(databaseName, root);

        await Task.WhenAll(
            first.UpdateAsync(BookingStatusCodes.Confirmed),
            second.UpdateAsync(BookingStatusCodes.Completed));

        await using var assertion = await Fixture.OpenAsync(databaseName, root);
        Assert.Equal(BookingStatusCodes.Confirmed, await assertion.BookingStatusAsync());
        Assert.Single(assertion.Db.AuditLogs);
    }

    [Fact]
    public async Task TC18_CompleteBooking_DoesNotCompleteContractOrClearDebt()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Preparing, PastEvent, ContractStatusCodes.Active);
        var booking = await fixture.Db.WeddingBookings.SingleAsync();
        booking.EstimatedTotal = 20_000_000;
        booking.PaidAmount = 2_000_000;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.UpdateAsync(BookingStatusCodes.Completed);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Completed, await fixture.BookingStatusAsync());
        Assert.Equal(ContractStatusCodes.Active, await fixture.ContractStatusAsync());
        Assert.Equal(2_000_000, (await fixture.BookingAsync()).PaidAmount);
    }

    [Fact]
    public async Task TC19_Cancellation_DoesNotUnlockAdministrativelyLockedSchedule()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Pending, FutureEvent, scheduleStatusCode: HallScheduleStatusCodes.Locked);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Cancelled);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Cancelled, await fixture.BookingStatusAsync());
        Assert.Equal(HallScheduleStatusCodes.Locked, await fixture.ScheduleStatusAsync());
    }

    [Fact]
    public async Task TC20_NormalConfirmation_DoesNotMutateExistingPayOsTransaction()
    {
        await using var fixture = await Fixture.CreateAsync(BookingStatusCodes.Pending, FutureEvent);
        fixture.Db.PaymentTransactions.Add(new PaymentTransaction
        {
            GiaoDichId = 1,
            DatTiecId = 1,
            TransactionType = PaymentTransactionTypes.Deposit,
            OrderCode = 20260102001,
            SoTienGiaoDich = 2_000_000,
            TrangThai = PaymentTransactionStatusCodes.Pending,
            NgayTao = BusinessNow
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.UpdateAsync(BookingStatusCodes.Confirmed);

        var transaction = await fixture.Db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Equal(2_000_000, transaction.SoTienGiaoDich);
        Assert.Equal(0, (await fixture.BookingAsync()).PaidAmount);
    }

    [Fact]
    public async Task ConfirmedToPreparing_IsRejectedWhenContractWasCancelled()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Confirmed, FutureEvent, ContractStatusCodes.Cancelled);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Preparing);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Confirmed, await fixture.BookingStatusAsync());
        Assert.Equal(ContractStatusCodes.Cancelled, await fixture.ContractStatusAsync());
    }

    [Fact]
    public async Task PreparingToCompleted_IsRejectedWhenChecklistIsNotApproved()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingStatusCodes.Preparing, PastEvent, withChecklist: true);

        var result = await fixture.UpdateAsync(BookingStatusCodes.Completed);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Preparing, await fixture.BookingStatusAsync());
        Assert.Equal("active", (await fixture.Db.PartyChecklists.AsNoTracking().SingleAsync()).State);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TimeProvider _timeProvider;

        private Fixture(ApplicationDbContext db, TimeProvider timeProvider)
        {
            Db = db;
            _timeProvider = timeProvider;
            Coordination = new CoordinationStub();
        }

        public ApplicationDbContext Db { get; }
        public CoordinationStub Coordination { get; }

        public static async Task<Fixture> CreateAsync(
            string bookingStatusCode,
            DateTime eventDate,
            string? contractStatusCode = null,
            bool withChecklist = false,
            string scheduleStatusCode = HallScheduleStatusCodes.Booked,
            string? databaseName = null,
            InMemoryDatabaseRoot? root = null)
        {
            var fixture = await OpenAsync(databaseName ?? Guid.NewGuid().ToString(), root ?? new InMemoryDatabaseRoot());
            var statuses = StatusData();
            fixture.Db.Statuses.AddRange(statuses);
            fixture.Db.DataStatuses.Add(new DataStatus
                { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });

            var scheduleStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.HallSchedule && item.StatusCode == scheduleStatusCode);
            var bookingStatus = statuses.Single(item =>
                item.StatusGroup == StatusGroups.Booking && item.StatusCode == bookingStatusCode);
            var hall = new Hall
            {
                HallId = 1, HallCode = "H01", HallName = "Riverside", MaximumCapacity = 500,
                StatusId = 31, DataStatusId = 1
            };
            var schedule = new HallSchedule
            {
                HallScheduleId = 1, HallId = 1, Hall = hall, Date = eventDate,
                Shift = "Ca tối", StatusId = scheduleStatus.StatusId,
                Status = scheduleStatus, DataStatusId = 1
            };
            var booking = new WeddingBooking
            {
                BookingId = 1, BookingCode = "DT-STATE-001", CustomerId = 1,
                Customer = new Customer
                {
                    CustomerId = 1, CustomerCode = "KH01", FullName = "Khách thử nghiệm",
                    PhoneNumber = "0901000001", DataStatusId = 1
                },
                HallScheduleId = 1, HallSchedule = schedule, GuestCount = 200,
                StatusId = bookingStatus.StatusId, Status = bookingStatus,
                EstimatedTotal = 20_000_000, PaidAmount = 0, BookedAt = BusinessNow
            };
            fixture.Db.Halls.Add(hall);
            fixture.Db.HallSchedules.Add(schedule);
            fixture.Db.WeddingBookings.Add(booking);

            if (contractStatusCode is not null)
            {
                var contractStatus = statuses.Single(item =>
                    item.StatusGroup == StatusGroups.Contract && item.StatusCode == contractStatusCode);
                fixture.Db.Contracts.Add(new Contract
                {
                    ContractId = 1, BookingId = 1, Booking = booking, ContractCode = "HD-001",
                    CreatedDate = BusinessNow, TotalValue = 20_000_000,
                    StatusId = contractStatus.StatusId, Status = contractStatus
                });
            }

            if (withChecklist)
                fixture.Db.PartyChecklists.Add(new PartyChecklist
                {
                    BookingId = 1, Booking = booking, State = "active", Version = 3
                });

            await fixture.Db.SaveChangesAsync();
            fixture.Db.ChangeTracker.Clear();
            return fixture;
        }

        public static Task<Fixture> OpenAsync(string databaseName, InMemoryDatabaseRoot root)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName, root)
                .ConfigureWarnings(warnings => warnings
                    .Ignore(InMemoryEventId.TransactionIgnoredWarning)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options;
            var vietnamNow = new DateTimeOffset(BusinessNow, TimeSpan.FromHours(7));
            return Task.FromResult(new Fixture(
                new ApplicationDbContext(options),
                new FixedTimeProvider(vietnamNow.ToUniversalTime())));
        }

        public async Task<IActionResult> UpdateAsync(string target, string? reason = null)
        {
            var controller = new AdminBookingController(
                Db,
                new StatusServiceStub(),
                Coordination,
                bookingStatusRules: new BookingStatusBusinessRules(Db, _timeProvider));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "9")], "test"))
                }
            };
            return await controller.UpdateBookingStatus(1, new UpdateBookingStatusRequestDto
            {
                StatusCode = target,
                CancellationReason = reason
            });
        }

        public async Task<WeddingBooking> BookingAsync()
        {
            Db.ChangeTracker.Clear();
            return await Db.WeddingBookings.AsNoTracking().SingleAsync();
        }

        public async Task<string> BookingStatusAsync()
        {
            Db.ChangeTracker.Clear();
            return await Db.WeddingBookings.AsNoTracking().Select(item => item.Status.StatusCode).SingleAsync();
        }

        public async Task<string> ScheduleStatusAsync()
        {
            Db.ChangeTracker.Clear();
            return await Db.HallSchedules.AsNoTracking().Select(item => item.Status.StatusCode).SingleAsync();
        }

        public async Task<string> ContractStatusAsync()
        {
            Db.ChangeTracker.Clear();
            return await Db.Contracts.AsNoTracking().Select(item => item.Status.StatusCode).SingleAsync();
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static Status[] StatusData() =>
        [
            new() { StatusId = 1, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ" },
            new() { StatusId = 2, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Xác nhận" },
            new() { StatusId = 3, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Preparing, StatusName = "Chuẩn bị" },
            new() { StatusId = 4, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Completed, StatusName = "Hoàn tất" },
            new() { StatusId = 5, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new() { StatusId = 11, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" },
            new() { StatusId = 12, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new() { StatusId = 13, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "Đã khóa" },
            new() { StatusId = 21, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Nháp" },
            new() { StatusId = 22, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Hiệu lực" },
            new() { StatusId = 23, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Completed, StatusName = "Hoàn tất" },
            new() { StatusId = 24, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new() { StatusId = 31, StatusGroup = StatusGroups.Hall, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" }
        ];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StatusServiceStub : IStatusService
    {
        public Task<Status?> GetStatusAsync(string group, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(null);

        public Task<int> GetStatusIdAsync(string group, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult((group, code) switch
            {
                (StatusGroups.Booking, BookingStatusCodes.Pending) => 1,
                (StatusGroups.Booking, BookingStatusCodes.Confirmed) => 2,
                (StatusGroups.Booking, BookingStatusCodes.Preparing) => 3,
                (StatusGroups.Booking, BookingStatusCodes.Completed) => 4,
                (StatusGroups.Booking, BookingStatusCodes.Cancelled) => 5,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Available) => 11,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked) => 12,
                _ => throw new InvalidOperationException($"Unknown status {group}/{code}")
            });
    }

    public sealed class CoordinationStub : ICoordinationAssignmentService
    {
        public int Synchronizations { get; private set; }

        public Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode,
            int? actorUserId, string? reason, CancellationToken cancellationToken)
        {
            Synchronizations++;
            return Task.CompletedTask;
        }

        public Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId, HallManagerCoordinationQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId, CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId, ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId, CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId, int? excludedBookingId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

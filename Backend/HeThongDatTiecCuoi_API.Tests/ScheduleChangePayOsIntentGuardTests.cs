using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ScheduleChangePayOsIntentGuardTests
{
    [Theory]
    [InlineData(PaymentTransactionStatusCodes.Creating)]
    [InlineData(PaymentTransactionStatusCodes.Pending)]
    [InlineData(PaymentTransactionStatusCodes.ReconciliationRequired)]
    public async Task AcceptQuote_BlockedByOutstandingContractPayOsIntent(string transactionStatus)
    {
        await using var db = await Seed(transactionStatus, PaymentTransactionTypes.Installment);
        var service = Service(db);

        var error = await Assert.ThrowsAsync<ScheduleChangeException>(() => service.AcceptQuoteAsync(
            999,
            new AcceptScheduleChangeQuoteRequest
            {
                BookingCode = "DT-1", PhoneNumber = "0900000001",
                VerificationToken = "token", QuoteId = 1
            },
            default));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("PayOS", error.Message);
    }

    [Fact]
    public async Task AcceptQuote_NotBlockedByTerminalContractPayOsIntent()
    {
        await using var db = await Seed(PaymentTransactionStatusCodes.Cancelled, PaymentTransactionTypes.Installment);
        var service = Service(db);

        var error = await Assert.ThrowsAsync<ScheduleChangeException>(() => service.AcceptQuoteAsync(
            999,
            new AcceptScheduleChangeQuoteRequest
            {
                BookingCode = "DT-1", PhoneNumber = "0900000001",
                VerificationToken = "token", QuoteId = 1
            },
            default));

        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task AcceptQuote_NotBlockedByContractlessNonfinancialUc18History()
    {
        await using var db = await Seed(PaymentTransactionStatusCodes.Cancelled, PaymentTransactionTypes.Installment);
        db.Set<Uc18BookingOperation>().Add(new Uc18BookingOperation
        {
            RequestId = Guid.NewGuid(), BookingId = 1, ActorUserId = 1,
            Kind = Uc18BookingOperation.Reschedule, PayloadHash = "nonfinancial", RecordedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        var error = await Assert.ThrowsAsync<ScheduleChangeException>(() => service.AcceptQuoteAsync(
            999,
            new AcceptScheduleChangeQuoteRequest
            {
                BookingCode = "DT-1", PhoneNumber = "0900000001",
                VerificationToken = "token", QuoteId = 1
            },
            default));

        Assert.Equal(404, error.StatusCode);
    }

    private static ScheduleChangeService Service(ApplicationDbContext db) => new(db, null!,
        new ScheduleChangePolicyService(db), null!, null!, null!, null!, null!, null!, null!, null!);

    private static async Task<ApplicationDbContext> Seed(string transactionStatus, string transactionType)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.DataStatuses.Add(new DataStatus
        {
            DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại"
        });
        db.Statuses.AddRange(
            new Status { StatusId = 101, StatusGroup = StatusGroups.Hall, StatusCode = BusinessStatusCodes.Active, StatusName = "Active" },
            new Status { StatusId = 102, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Available" },
            new Status { StatusId = 103, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Confirmed" });
        db.Customers.Add(new Customer
        {
            CustomerId = 1, CustomerCode = "KH1", FullName = "Khách thử",
            PhoneNumber = "0900000001", DataStatusId = 1
        });
        db.Halls.Add(new Hall
        {
            HallId = 1, HallCode = "S1", HallName = "Sảnh 1", MaximumCapacity = 500,
            StatusId = 101, DataStatusId = 1
        });
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = 1, HallId = 1, Date = RiversideBusinessClock.Today.AddDays(10),
            Shift = "Ca trưa", StatusId = 102, DataStatusId = 1
        });
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 1, BookingCode = "DT-1", CustomerId = 1, HallScheduleId = 1,
            GuestCount = 100, StatusId = 103
        });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            GiaoDichId = 1, DatTiecId = 1, TransactionType = transactionType,
            OrderCode = 20260101001, SoTienGiaoDich = 100_000, SoTienGhiNhan = 100_000,
            TrangThai = transactionStatus, NgayTao = RiversideBusinessClock.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}

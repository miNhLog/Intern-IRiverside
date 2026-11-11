using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class HallScheduleOccupancyLockTests
{
    [Fact]
    public async Task UpdateStatus_RejectsOccupyingBookingWithoutChangingScheduleOrAudit()
    {
        await using var db = CreateDatabase();
        Seed(db, withBooking: true);

        var result = await CreateController(db).UpdateHallScheduleStatus(1,
            new StatusRequest { Status = HallScheduleStatusCodes.Locked }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(HallScheduleStatusCodes.Available,
            (await db.HallSchedules.Include(item => item.Status).SingleAsync()).Status.StatusCode);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatus_ChangesUnoccupiedScheduleAndAuditsAtomically()
    {
        await using var db = CreateDatabase();
        Seed(db, withBooking: false);

        var result = await CreateController(db).UpdateHallScheduleStatus(1,
            new StatusRequest { Status = HallScheduleStatusCodes.Locked }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(HallScheduleStatusCodes.Locked,
            (await db.HallSchedules.Include(item => item.Status).SingleAsync()).Status.StatusCode);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(true, 409)]
    [InlineData(false, 200)]
    public async Task UpdateStatus_RejectsOnlyAnActiveNonexpiredScheduleHold(bool activeHold, int expectedStatus)
    {
        await using var db = CreateDatabase();
        Seed(db, withBooking: false);
        AddHold(db, activeHold ? DateTime.Now.AddMinutes(5) : DateTime.Now.AddMinutes(-5));

        var result = await CreateController(db).UpdateHallScheduleStatus(1,
            new StatusRequest { Status = HallScheduleStatusCodes.Locked }, CancellationToken.None);

        Assert.Equal(expectedStatus, result is ConflictObjectResult ? 409 : 200);
        Assert.Equal(activeHold ? HallScheduleStatusCodes.Available : HallScheduleStatusCodes.Locked,
            (await db.HallSchedules.Include(item => item.Status).SingleAsync()).Status.StatusCode);
    }

    private static ApplicationDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static HallScheduleController CreateController(ApplicationDbContext db) =>
        new(db, new StatusService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "10")], "Test"))
                }
            }
        };

    private static void Seed(ApplicationDbContext db, bool withBooking)
    {
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Existing" });
        db.Statuses.AddRange(
            new Status { StatusId = 1, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Active" },
            new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Available" },
            new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "Locked" },
            new Status { StatusId = 801, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Confirmed" });
        db.Halls.Add(new Hall { HallId = 1, HallCode = "H1", HallName = "Hall", MaximumCapacity = 500, StatusId = 1, DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 1, HallId = 1, Date = DateTime.Today.AddDays(10),
            Shift = "Ca tối", StatusId = 301, DataStatusId = 1 });
        if (withBooking)
            db.WeddingBookings.Add(new WeddingBooking { BookingId = 1, BookingCode = "B1", HallScheduleId = 1,
                StatusId = 801, CustomerId = 1, GuestCount = 100, BookedAt = DateTime.Now });
        db.SaveChanges();
    }

    private static void AddHold(ApplicationDbContext db, DateTime expiresAt)
    {
        db.Statuses.AddRange(
            new Status { StatusId = 901, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Cancelled" },
            new Status { StatusId = 1301, StatusGroup = StatusGroups.ScheduleChange, StatusCode = ScheduleChangeStatusCodes.Pending, StatusName = "Pending" });
        db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "C1", FullName = "Customer", PhoneNumber = "0900000000", DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking { BookingId = 1, BookingCode = "B1", CustomerId = 1, HallScheduleId = 1,
            StatusId = 901, GuestCount = 100, BookedAt = DateTime.Now });
        db.ScheduleChangePolicies.Add(new ScheduleChangePolicy { PolicyId = 1, PolicyCode = "P1", Version = 1,
            OnlineMinimumDays = 7, NewDateMinimumDays = 14, PaymentWindowMinutes = 10, HoldWindowMinutes = 10,
            Status = ScheduleChangePolicyStatusCodes.Active, EffectiveFrom = DateTime.Today, CreatedAt = DateTime.Now });
        db.WeddingScheduleChanges.Add(new WeddingScheduleChange { ScheduleChangeId = 1, BookingId = 1,
            OldHallScheduleId = 1, NewHallScheduleId = 1, StatusId = 1301, RequestedAt = DateTime.Now,
            PolicyId = 1, PolicyVersion = 1, SubmittedDaysBeforeEvent = 10 });
        db.ScheduleChangeHolds.Add(new ScheduleChangeHold { ScheduleChangeId = 1, HallScheduleId = 1,
            Status = ScheduleChangeHoldStatusCodes.Active, StartedAt = DateTime.Now, ExpiresAt = expiresAt });
        db.SaveChanges();
    }
}

using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class HallManagerScheduleChangeLookupTests
{
    [Fact]
    public async Task Options_OnlyListsEligibleScopedBookings_AndReturnsPolicyDates()
    {
        await using var db = await Seed(); var service = Service(db);
        var dto = await service.GetHallManagerCreateOptionsAsync(100, default);
        Assert.Equal(1, Assert.Single(dto.Bookings).BookingId);
        Assert.Equal("DT-1", dto.Bookings[0].BookingCode); Assert.Equal("Khách thử", dto.Bookings[0].CustomerName);
        Assert.Equal(RiversideBusinessClock.Today.AddDays(2), dto.MinimumNewDate);
        Assert.Equal(7, dto.ExceptionThresholdDays);
        Assert.Empty((await service.GetHallManagerCreateOptionsAsync(200, default)).Bookings);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Availability_RejectsForeignBookingAndOpenRequest()
    {
        await using var db = await Seed(); var service = Service(db);
        var foreign = await Assert.ThrowsAsync<ScheduleChangeException>(() => service.GetHallManagerAvailabilityAsync(100, 2, RiversideBusinessClock.Today.AddDays(5), "Ca trưa", default));
        Assert.Equal(404, foreign.StatusCode);
        var open = await Assert.ThrowsAsync<ScheduleChangeException>(() => service.GetHallManagerAvailabilityAsync(100, 5, RiversideBusinessClock.Today.AddDays(5), "Ca trưa", default));
        Assert.Equal(409, open.StatusCode);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Availability_ReportsCapacityLocksOccupancyAndHolds_WithoutCreatingSchedules()
    {
        await using var db = await Seed(); var service = Service(db); var before = await db.HallSchedules.CountAsync();
        var result = await service.GetHallManagerAvailabilityAsync(100, 1, RiversideBusinessClock.Today.AddDays(5), "Ca trưa", default);
        Assert.True(result.Single(h => h.HallId == 1).Available); // Missing slot is available; lookup must not insert it.
        Assert.Contains("sức chứa", result.Single(h => h.HallId == 2).UnavailableReason!);
        Assert.Contains("khóa", result.Single(h => h.HallId == 3).UnavailableReason!);
        Assert.Contains("booking khác", result.Single(h => h.HallId == 4).UnavailableReason!);
        Assert.Contains("giữ tạm", result.Single(h => h.HallId == 5).UnavailableReason!);
        var same = await service.GetHallManagerAvailabilityAsync(100, 1, RiversideBusinessClock.Today.AddDays(10), "Ca trưa", default);
        Assert.Contains("trùng hoàn toàn", same.Single(h => h.HallId == 1).UnavailableReason!);
        await Assert.ThrowsAsync<ScheduleChangeException>(() => service.GetHallManagerAvailabilityAsync(100, 1, RiversideBusinessClock.Today, "Ca trưa", default));
        Assert.Equal(before, await db.HallSchedules.CountAsync()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Availability_ExplainsMinimumAndMaximum_WithActualGuestCount_AndAcceptsExactLimits()
    {
        await using var db = await Seed(); var service = Service(db);
        var hall = await db.Halls.SingleAsync(h => h.HallId == 1);
        hall.MinimumCapacity = 150; await db.SaveChangesAsync();
        async Task<HeThongDatTiecCuoi_API.DTOs.ScheduleChange.ScheduleChangeAvailabilityDto> Read() =>
            (await service.GetHallManagerAvailabilityAsync(100, 1, RiversideBusinessClock.Today.AddDays(5), "Ca trưa", default)).Single(h => h.HallId == 1);
        var low = await Read();
        Assert.False(low.Available); Assert.Equal(150, low.MinimumCapacity);
        Assert.Equal("Tiệc có 100 khách, chưa đạt sức chứa tối thiểu 150 khách của sảnh.", low.UnavailableReason);
        hall.MinimumCapacity = 100; await db.SaveChangesAsync(); Assert.True((await Read()).Available);
        var booking = await db.WeddingBookings.SingleAsync(b => b.BookingId == 1);
        booking.GuestCount = 500; await db.SaveChangesAsync(); Assert.True((await Read()).Available);
        booking.GuestCount = 501; await db.SaveChangesAsync();
        var high = await Read(); Assert.False(high.Available);
        Assert.Equal("Tiệc có 501 khách, vượt sức chứa tối đa 500 khách của sảnh.", high.UnavailableReason);
        booking.GuestCount = 50; hall.MinimumCapacity = null; await db.SaveChangesAsync();
        var noMinimum = await Read(); Assert.True(noMinimum.Available); Assert.Null(noMinimum.MinimumCapacity);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static ScheduleChangeService Service(ApplicationDbContext db) => new(db, null!, new ScheduleChangePolicyService(db),
        null!, null!, null!, null!, null!, null!, null!, null!);

    private static async Task<ApplicationDbContext> Seed()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.DataStatuses.Add(new() { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
        db.Roles.Add(new Role { RoleId = 3, RoleName = RoleNames.HallManager });
        db.Statuses.AddRange(S(101, StatusGroups.Account, AccountStatusCodes.Active), S(201, StatusGroups.Hall, BusinessStatusCodes.Active),
            S(301, StatusGroups.HallSchedule, HallScheduleStatusCodes.Available), S(302, StatusGroups.HallSchedule, HallScheduleStatusCodes.Locked),
            S(801, StatusGroups.Booking, BookingStatusCodes.Pending), S(802, StatusGroups.Booking, BookingStatusCodes.Confirmed),
            S(1001, StatusGroups.ScheduleChange, ScheduleChangeStatusCodes.Pending));
        db.Users.Add(new User { UserId = 100, Email = "test@example.test", PasswordHash = "test", RoleId = 3, StatusId = 101, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = 10, UserId = 100, EmployeeCode = "QL10", FullName = "Quản lý", PhoneNumber = "0900000000", DataStatusId = 1 });
        for (var i = 1; i <= 5; i++) db.Halls.Add(new Hall { HallId = i, HallCode = $"S{i}", HallName = $"Sảnh {i}", MaximumCapacity = i == 2 ? 20 : 500, StatusId = 201, DataStatusId = 1 });
        db.HallManagerAssignments.Add(new() { HallId = 1, HallManagerEmployeeId = 10, FromDate = RiversideBusinessClock.Today.AddDays(-5) });
        db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "KH1", FullName = "Khách thử", PhoneNumber = "0900000001", DataStatusId = 1 });
        for (var i = 1; i <= 5; i++)
        {
            db.HallSchedules.Add(new HallSchedule { HallScheduleId = i, HallId = i == 2 ? 2 : 1, Date = RiversideBusinessClock.Today.AddDays(i == 3 ? -1 : 10+i-1), Shift = "Ca trưa", StatusId = 301, DataStatusId = 1 });
            db.WeddingBookings.Add(new WeddingBooking { BookingId = i, BookingCode = $"DT-{i}", CustomerId = 1, HallScheduleId = i, GuestCount = 100, StatusId = i == 4 ? 801 : 802 });
        }
        db.ScheduleChangePolicies.Add(new ScheduleChangePolicy { PolicyId = 1, Version = 1, Status = ScheduleChangePolicyStatusCodes.Active,
            EffectiveFrom = RiversideBusinessClock.Today.AddDays(-10), NewDateMinimumDays = 2, OnlineMinimumDays = 7 });
        db.WeddingScheduleChanges.Add(new WeddingScheduleChange { ScheduleChangeId = 42, BookingId = 5, OldHallScheduleId = 5, NewHallScheduleId = 1, StatusId = 1001, PolicyId = 1, Reason = "Đang xử lý" });
        for (var i = 3; i <= 5; i++) db.HallSchedules.Add(new HallSchedule { HallScheduleId = 10+i, HallId = i, Date = RiversideBusinessClock.Today.AddDays(5), Shift = "Ca trưa", StatusId = i == 3 ? 302 : 301, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking { BookingId = 6, BookingCode = "DT-6", CustomerId = 1, HallScheduleId = 14, GuestCount = 100, StatusId = 802 });
        db.ScheduleChangeHolds.Add(new ScheduleChangeHold { HoldId = 1, ScheduleChangeId = 42, HallScheduleId = 15, Status = ScheduleChangeHoldStatusCodes.Active, ExpiresAt = RiversideBusinessClock.Now.AddHours(1) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }
    private static Status S(int id, string group, string code) => new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };
}

using System.Security.Claims;
using System.Net.Http.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Booking;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ManagerBookingChangesTests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    private const string DatabaseName = "ManagerBookingChangesTests";
    [Fact]
    public async Task Reschedule_CrossHallAcceptsAttestedCountsAtMinimumAndMaximumBoundariesAndUpdatesOnlyHallDelta()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        var booking = await db.WeddingBookings.SingleAsync();
        booking.BilledTableCount = 21;
        booking.SpareTableCount = 1;
        db.Halls.Single(x => x.HallId == 2).MinimumCapacity = 200;
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.Manager, 10);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(await controller.Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto { NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2 }, CancellationToken.None)).Value);
        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(900m, updated.FinalHallPrice);
        Assert.Equal(1400m, updated.EstimatedTotal);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        var operation = Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Equal(Uc18BookingOperation.Reschedule, operation.Kind);
        Assert.Null(operation.ContractId);
        Assert.Contains("PreviousEvent", operation.SnapshotJson);
        Assert.Contains("CurrentEvent", operation.SnapshotJson);
    }

    [Fact]
    public async Task ManagerDetail_ExposesNullableStructuredTableCounts()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        var booking = await db.WeddingBookings.SingleAsync();
        booking.BilledTableCount = 22;
        booking.SpareTableCount = 2;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Detail(1, CancellationToken.None);

        var detail = Assert.IsType<BookingDetailDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(22, detail.BilledTableCount);
        Assert.Equal(2, detail.SpareTableCount);
    }

    [Theory]
    [InlineData(21, 1, "PASS")]
    [InlineData(20, 1, "FAIL")]
    [InlineData(22, 1, "FAIL")]
    public async Task RescheduleQuote_CrossHallUsesBothPersistedCountsAndDestinationBounds(int billed, int spare, string expected)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        var booking = await db.WeddingBookings.SingleAsync();
        booking.BilledTableCount = billed;
        booking.SpareTableCount = spare;
        await db.SaveChangesAsync();
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(await CreateController(db, RoleNames.Manager, 10)
            .Quote(1, QuoteRequest(2), CancellationToken.None)).Value);
        Assert.Equal(expected, quote.CapacityCheck);
        Assert.Equal(spare, quote.SpareTableCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task RescheduleQuote_RejectsOnlyAnActiveNonexpiredUc15DestinationHold(bool activeHold, bool rejected)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40),
            Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        AddScheduleChangeHold(db, 2, activeHold ? DateTime.Now.AddMinutes(5) : DateTime.Now.AddMinutes(-5));
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto { NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2 },
            CancellationToken.None);

        if (rejected)
        {
            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("UC15", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
        }
        else
        {
            Assert.IsType<OkObjectResult>(result);
        }
    }

    [Theory]
    [InlineData(ScheduleChangeStatusCodes.Pending, true)]
    [InlineData(ScheduleChangeStatusCodes.Expired, false)]
    [InlineData(ScheduleChangeStatusCodes.Approved, false)]
    public async Task RescheduleQuote_RejectsOpenUc15RequestsButNotExpiredOrApprovedMirrors(string uc15Status, bool rejected)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddScheduleChange(db, uc15Status);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(2), CancellationToken.None);

        if (rejected) Assert.IsType<ConflictObjectResult>(result);
        else Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RescheduleQuote_RejectsAuthoritativeUc15PaymentHistory()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            GiaoDichId = 1, DatTiecId = 1, TransactionType = PaymentTransactionTypes.ScheduleChange,
            OrderCode = 123, SoTienGiaoDich = 100, SoTienGhiNhan = 100, TrangThai = PaymentTransactionStatusCodes.Paid
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(2), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Reschedule_MenuOnlyCustomSelectionSnapshotsServerPricesAndLeavesSlotAndOtherChargesUntouched()
    {
        await using var db = CreateDatabase();
        Seed(db, "keep these notes");
        SeedMenuCatalog(db);
        SetValidTableCounts(db);
        db.ServiceItems.Add(new ServiceItem { DichVuID = 11, MaDichVu = "S11", TenDichVu = "Service", Gia = 75m, StatusId = 701, DataStatusId = 1 });
        db.WeddingBookingServices.Add(new WeddingBookingService { BookingServiceId = 1, BookingId = 1, ServiceId = 11, Quantity = 2, FinalUnitPrice = 75m, DataStatusId = 1 });
        var booking = await db.WeddingBookings.SingleAsync();
        booking.FinalDecorationPrice = 250m;
        var originalScheduleStatus = (await db.HallSchedules.SingleAsync()).StatusId;
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(await controller.Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto
            {
                NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1,
                ChangeMenu = true, CustomDishIds = [2, 1]
            }, CancellationToken.None)).Value);
        Assert.False(quote.RequiresPriceConfirmation);
        var request = new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1,
            ChangeMenu = true, CustomDishIds = [2, 1], MenuPriceDifferenceConfirmed = true, ConfirmedMenuPriceDifference = 100m
        };

        var result = await controller.Reschedule(1, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(200m, updated.FinalMenuPrice);
        Assert.Null(updated.MenuId);
        Assert.Contains("Dish A", updated.CustomMenuSnapshotJson);
        Assert.Contains("Dish B", updated.CustomMenuSnapshotJson);
        Assert.Equal("keep these notes", updated.SpecialRequests);
        Assert.Equal(250m, updated.FinalDecorationPrice);
        var service = Assert.Single(await db.WeddingBookingServices.AsNoTracking().ToListAsync());
        Assert.Equal((2, 75m), (service.Quantity, service.FinalUnitPrice));
        Assert.Equal(3000m, updated.EstimatedTotal);
        Assert.Equal(1, updated.HallScheduleId);
        Assert.Equal(originalScheduleStatus, (await db.HallSchedules.AsNoTracking().SingleAsync()).StatusId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        var audit = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal("BOOKING_MENU_CHANGED_BY_STAFF", audit.Action);
        Assert.Contains("FinalMenuPrice", audit.OldData);
    }

    [Fact]
    public async Task Reschedule_RejectsDisabledCatalogueMenuWithoutChanges()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        SeedMenuCatalog(db);
        SetValidTableCounts(db);
        db.Menus.Single().StatusId = 402;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1, ChangeMenu = true, MenuId = 1
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_MENU_UNAVAILABLE", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(100m, (await db.WeddingBookings.AsNoTracking().SingleAsync()).FinalMenuPrice);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_MenuConfirmationMustMatchCurrentCataloguePrice()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        SeedMenuCatalog(db);
        SetValidTableCounts(db);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quoted = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(await controller.Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1, ChangeMenu = true, MenuId = 1 }, CancellationToken.None)).Value);
        db.Menus.Single().PricePerTable = 250m;
        await db.SaveChangesAsync();

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1, ChangeMenu = true, MenuId = 1,
            MenuPriceDifferenceConfirmed = true, ConfirmedMenuPriceDifference = quoted.MenuPriceDifference
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_MENU_PRICE_CONFIRMATION_REQUIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(100m, (await db.WeddingBookings.AsNoTracking().SingleAsync()).FinalMenuPrice);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_MenuOnlyAcceptsActiveCatalogueMenuAndClearsCustomSnapshot()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        SeedMenuCatalog(db);
        SetValidTableCounts(db);
        var booking = await db.WeddingBookings.SingleAsync();
        booking.CustomMenuSnapshotJson = "[{\"DishId\":9,\"DishName\":\"old\",\"Price\":10}]";
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1, ChangeMenu = true, MenuId = 1,
            MenuPriceDifferenceConfirmed = true, ConfirmedMenuPriceDifference = 60m
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(1, updated.MenuId);
        Assert.Equal(160m, updated.FinalMenuPrice);
        Assert.Null(updated.CustomMenuSnapshotJson);
        Assert.Equal(2200m, updated.EstimatedTotal);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_MenuChangeRejectsNegativeEstimatedTotalWithoutApplyingChanges()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        SeedMenuCatalog(db);
        SetValidTableCounts(db);
        var booking = await db.WeddingBookings.SingleAsync();
        booking.FinalMenuPrice = 500m;
        booking.EstimatedTotal = 100m;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1,
            ChangeMenu = true, CustomDishIds = [1, 2], MenuPriceDifferenceConfirmed = true, ConfirmedMenuPriceDifference = -300m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_ESTIMATED_TOTAL_INVALID", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        var unchanged = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(500m, unchanged.FinalMenuPrice);
        Assert.Equal(100m, unchanged.EstimatedTotal);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_MenuChangeCannotPriceWithoutBothStructuredTableCounts()
    {
        await using var db = CreateDatabase();
        Seed(db, "legacy menu marker must not be read");
        SeedMenuCatalog(db);
        var request = new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1,
            ChangeMenu = true, CustomDishIds = [1, 2], MenuPriceDifferenceConfirmed = true, ConfirmedMenuPriceDifference = 100m
        };

        var quote = await CreateController(db, RoleNames.Manager, 10).Quote(1, request, CancellationToken.None);
        var commit = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, request, CancellationToken.None);

        var quoteConflict = Assert.IsType<ConflictObjectResult>(quote);
        var commitConflict = Assert.IsType<ConflictObjectResult>(commit);
        Assert.Equal("ERR_TABLE_COUNTS_UNKNOWN", quoteConflict.Value!.GetType().GetProperty("code")!.GetValue(quoteConflict.Value));
        Assert.Equal("ERR_TABLE_COUNTS_UNKNOWN", commitConflict.Value!.GetType().GetProperty("code")!.GetValue(commitConflict.Value));
        var unchanged = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Null(unchanged.CustomMenuSnapshotJson);
        Assert.Equal(100m, unchanged.FinalMenuPrice);
        Assert.Equal(1000m, unchanged.EstimatedTotal);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_SameHallSlotChangeRequiresExactFreshQuoteAndRejectsOmittedConfirmation()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var request = new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1
        };
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(await controller.Quote(1, request, CancellationToken.None)).Value);
        Assert.True(quote.RequiresPriceConfirmation);
        db.Halls.Single(x => x.HallId == 1).RentalPrice = 550m;
        await db.SaveChangesAsync();

        var stale = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = request.NewDate, NewShift = request.NewShift, NewHallId = request.NewHallId,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference
        }, CancellationToken.None);
        var omitted = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = request.NewDate, NewShift = request.NewShift, NewHallId = request.NewHallId
        }, CancellationToken.None);

        var staleConflict = Assert.IsType<ConflictObjectResult>(stale);
        var omittedConflict = Assert.IsType<ConflictObjectResult>(omitted);
        Assert.Equal("ERR_PRICE_CONFIRMATION_REQUIRED", staleConflict.Value!.GetType().GetProperty("code")!.GetValue(staleConflict.Value));
        Assert.Equal("ERR_PRICE_CONFIRMATION_REQUIRED", omittedConflict.Value!.GetType().GetProperty("code")!.GetValue(omittedConflict.Value));
        var unchanged = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(500m, unchanged.FinalHallPrice);
        Assert.Equal(1000m, unchanged.EstimatedTotal);
        Assert.Equal(1, unchanged.HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Theory]
    [InlineData("[Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("Ghi chú tùy ý; [Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: 1", "UNKNOWN", null)]
    [InlineData("Ghi chú Bàn chính: 18; bàn dự phòng: 1", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: x", "UNKNOWN", null)]
    [InlineData("Bàn chính: 18; bàn dự phòng: 1; [Bàn dự phòng: 1]", "UNKNOWN", null)]
    [InlineData("Ghi chú tùy ý; [Bàn dự phòng: 2]; Ngân sách dự kiến 1.000 đ; [Dịch vụ đi kèm: DV1]", "UNKNOWN", null)]
    [InlineData("Ghi chú [Bàn dự phòng: 2]", "UNKNOWN", null)]
    [InlineData("[Bàn dự phòng: 2]; [Bàn dự phòng: 3]", "UNKNOWN", null)]
    [InlineData("[Bàn dự phòng: x]", "UNKNOWN", null)]
    [InlineData(null, "UNKNOWN", null)]
    public async Task RescheduleQuote_OnlyTrustsSinglePersistedSpareTableMarker(string? specialRequests, string capacityCheck, int? expectedSpareTables)
    {
        await using var db = CreateDatabase();
        Seed(db, specialRequests);
        var controller = CreateController(db, RoleNames.Manager, 10);

        var result = await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2
        }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(capacityCheck, quote.CapacityCheck);
        Assert.Equal(expectedSpareTables, quote.SpareTableCount);
        Assert.Equal(500m, quote.SavedHallPrice);
        Assert.Equal(900m, quote.CurrentHallPrice);
        Assert.Equal(400m, quote.PriceDifference);
    }

    [Fact]
    public async Task Reschedule_WithContract_IsRejectedWithoutChangingBooking()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(2), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("ERR_CONTRACT_NOT_AMENDABLE", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task PendingExpiredBooking_CannotBeRescheduledBeforeExpirationWorkerRuns()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 800;
        booking.BookedAt = DateTime.Now.AddHours(-49);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(1), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_BOOKING_HOLD_EXPIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task Reschedule_HallChangeWithUnknownSpareTablesFailsClosed()
    {
        await using var db = CreateDatabase();
        Seed(db, "Ghi chú bàn dự phòng: không rõ");

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_CAPACITY_UNKNOWN", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task SameHallQuote_UnknownSpareTablesIsNotReportedAsCapacityPass()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1
        }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("UNKNOWN", quote.CapacityCheck);
        Assert.Null(quote.SpareTableCount);
        Assert.Contains("giữ nguyên sảnh", quote.CapacityMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reschedule_CrossHallRejectsCustomerForgedSpareTableMarkerWithoutChangingBookingOrSchedules()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        await db.SaveChangesAsync();
        var bookingBefore = await db.WeddingBookings.AsNoTracking().SingleAsync();
        var scheduleBefore = await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => new { x.HallScheduleId, x.HallId, x.Date, x.Shift, x.StatusId }).ToListAsync();

        var quoteResult = await CreateController(db, RoleNames.Manager, 10).Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2
        }, CancellationToken.None);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(quoteResult).Value);
        Assert.Equal("UNKNOWN", quote.CapacityCheck);
        Assert.Null(quote.SpareTableCount);

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_CAPACITY_UNKNOWN", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Contains("cùng sảnh", conflict.Value.GetType().GetProperty("message")!.GetValue(conflict.Value)!.ToString(), StringComparison.OrdinalIgnoreCase);
        var bookingAfter = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(bookingBefore.HallScheduleId, bookingAfter.HallScheduleId);
        Assert.Equal(bookingBefore.FinalHallPrice, bookingAfter.FinalHallPrice);
        Assert.Equal(scheduleBefore, await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId)
            .Select(x => new { x.HallScheduleId, x.HallId, x.Date, x.Shift, x.StatusId }).ToListAsync());
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Empty(await db.Contracts.ToListAsync());
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_FinanciallyLinkedBookingWithForgedMarkerLeavesBookingSchedulesContractAndPaymentUnchanged()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 1]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 2, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        db.Statuses.Add(new Status { StatusId = 901, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Draft" });
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        db.Payments.Add(new Payment { PaymentId = 1, ContractId = 1, PaymentType = "Đặt cọc", Amount = 100m, PaymentDate = DateTime.Today, StatusId = 1 });
        await db.SaveChangesAsync();
        var scheduleStatusBefore = await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => x.StatusId).ToListAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 400m,
            RequestId = Guid.NewGuid(), QuoteToken = "forged-marker-guard"
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_CAPACITY_UNKNOWN", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Equal(scheduleStatusBefore, await db.HallSchedules.AsNoTracking().OrderBy(x => x.HallScheduleId).Select(x => x.StatusId).ToListAsync());
        Assert.Equal("HD-1", (await db.Contracts.AsNoTracking().SingleAsync()).ContractCode);
        Assert.Equal(100m, (await db.Payments.AsNoTracking().SingleAsync()).Amount);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_TerminalBookingCannotBeMoved()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 805;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1, QuoteRequest(1), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task PendingExpiredBooking_CannotBeConfirmedBeforeExpirationWorkerRuns()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 800;
        booking.BookedAt = DateTime.Now.AddHours(-49);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "10"), new Claim(ClaimTypes.Role, RoleNames.Manager)
        ], "Test"));
        var controller = new AdminBookingController(db, new StatusService(db),
            new CoordinationAssignmentService(db, new NotificationService(db)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        var result = await controller.UpdateBookingStatus(1, new UpdateBookingStatusRequestDto { StatusCode = BookingStatusCodes.Confirmed });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_BOOKING_HOLD_EXPIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(BookingStatusCodes.Pending, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
    }

    [Fact]
    public async Task Reschedule_HallPriceConfirmationMustMatchFreshQuote()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");

        var controller = CreateController(db, RoleNames.Manager, 10);
        var quoted = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, QuoteRequest(2), CancellationToken.None)).Value);
        var hall = await db.Halls.SingleAsync(x => x.HallId == 2);
        hall.RentalPrice = 950m;
        await db.SaveChangesAsync();

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = 2,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quoted.PriceDifference
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("ERR_PRICE_CONFIRMATION_REQUIRED", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
    }

    [Fact]
    public async Task Reschedule_ToLockedSchedule_RollsBackWithoutHistoryOrAudit()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 1, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 303, DataStatusId = 1 });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Equal(HallScheduleStatusCodes.Locked, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 2)).Status.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MovesSlotAndAuditsActorWithoutChangingFinancialSnapshot()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Statuses.Add(new Status { StatusId = 1401, StatusGroup = StatusGroups.CoordinationAssignment,
            StatusCode = CoordinationAssignmentStatusCodes.Assigned, StatusName = "Assigned" });
        db.Roles.Add(new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
        db.Users.Add(new User { UserId = 40, RoleId = 4, Email = "coordinator@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = 40, UserId = 40, EmployeeCode = "C40", FullName = "Coordinator",
            PhoneNumber = "0900000040", DataStatusId = 1 });
        db.CoordinationAssignments.Add(new CoordinationAssignment { CoordinationAssignmentId = 1, BookingId = 1,
            CoordinatorEmployeeId = 40, AssignedByHallManagerEmployeeId = 40, StatusId = 1401, AssignedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m, Reason = "Điều chỉnh lịch"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var booking = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.NotEqual(1, booking.HallScheduleId);
        Assert.Equal(500m, booking.FinalHallPrice);
        Assert.Equal(1000m, booking.EstimatedTotal);
        Assert.Empty(await db.WeddingScheduleChanges.AsNoTracking().ToListAsync());
        var operation = Assert.Single(await db.Set<Uc18BookingOperation>().AsNoTracking().ToListAsync());
        Assert.Equal(Uc18BookingOperation.Reschedule, operation.Kind);
        Assert.Contains("Reason", operation.SnapshotJson);
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(10, audit.UserId);
        Assert.Equal("BOOKING_RESCHEDULED_BY_STAFF", audit.Action);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 1)).Status.StatusCode);
        var notification = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(40, notification.RecipientUserId);
        Assert.Equal("BOOKING_RESCHEDULED", notification.Type);
    }

    [Fact]
    public async Task Cancel_RejectsContractAndTerminalBookings()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        await db.SaveChangesAsync();

        var withContract = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Yêu cầu hủy" }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(withContract).StatusCode);
        Assert.Equal(BookingStatusCodes.Confirmed, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);

        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 805;
        await db.SaveChangesAsync();
        var terminal = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Lặp lại" }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(terminal).StatusCode);
    }

    [Fact]
    public async Task Cancel_EligibleBookingReleasesOnlyBookedScheduleAndAuditsActor()
    {
        await using var db = CreateDatabase();
        Seed(db, null);

        var result = await CreateController(db, RoleNames.Manager, 10).Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Đổi kế hoạch" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var booking = await db.WeddingBookings.Include(x => x.Status).SingleAsync();
        Assert.Equal(BookingStatusCodes.Cancelled, booking.Status.StatusCode);
        Assert.Equal("Đổi kế hoạch", booking.CancellationReason);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(10, audit.UserId);
        Assert.Equal("BOOKING_CANCELLED_BY_STAFF", audit.Action);
    }

    [Fact]
    public async Task Reschedule_ToOccupiedScheduleRejectsWithoutPartialChanges()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        db.Customers.Add(new Customer { CustomerId = 2, CustomerCode = "C2", FullName = "Khách hai", PhoneNumber = "0900000001", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 2, HallId = 1, Date = DateTime.Today.AddDays(40), Shift = "Ca tối", StatusId = 302, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 2, BookingCode = "DT-TEST-2", CustomerId = 2, HallScheduleId = 2, GuestCount = 100,
            StatusId = 801, BookedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync(x => x.BookingId == 1)).HallScheduleId);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 2)).Status.StatusCode);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task HallManager_CannotQuoteBookingOutsideActiveHallAssignment()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        AddHallManager(db, 20, 1);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Quote(1, QuoteRequest(2), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData("quote-financial")]
    [InlineData("quote-terminal")]
    [InlineData("reschedule-financial")]
    [InlineData("reschedule-terminal")]
    [InlineData("cancel-financial")]
    [InlineData("cancel-terminal")]
    public async Task HallManager_CannotLearnFinancialOrTerminalStateOfBookingOutsideAssignment(string operation)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddHallManager(db, 20, 2);
        if (operation.EndsWith("financial", StringComparison.Ordinal))
            db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000, StatusId = 901 });
        if (operation.EndsWith("terminal", StringComparison.Ordinal))
            (await db.WeddingBookings.SingleAsync()).StatusId = 805;
        await db.SaveChangesAsync();

        var controller = CreateController(db, RoleNames.HallManager, 20);
        IActionResult result = operation.StartsWith("quote", StringComparison.Ordinal)
            ? await controller.Quote(1, QuoteRequest(1), CancellationToken.None)
            : operation.StartsWith("reschedule", StringComparison.Ordinal)
                ? await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
                {
                    NewDate = DateTime.Today.AddDays(40), NewShift = "Ca tối", NewHallId = 1,
                    PriceDifferenceConfirmed = true, ConfirmedPriceDifference = 0m
                }, CancellationToken.None)
                : await controller.Cancel(1, new ManagerBookingCancelRequestDto { Reason = "Yêu cầu hủy" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Empty(await db.WeddingScheduleChanges.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(999, false)]
    public async Task HallManager_CannotProbeDestinationOutsideAssignment(int destinationHallId, bool inactive)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddHallManager(db, 20, 1);
        if (inactive)
        {
            db.Statuses.Add(new Status { StatusId = 202, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Inactive, StatusName = "Ngừng hoạt động" });
            db.Halls.Single(x => x.HallId == destinationHallId).StatusId = 202;
        }
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Quote(1, QuoteRequest(destinationHallId), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
    }

    [Fact]
    public async Task HallManager_CanQuoteWithinActiveHallAssignment()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn dự phòng: 2]");
        AddHallManager(db, 20, 1);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Quote(1, QuoteRequest(1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ManagerAttestation_PersistsZeroSpareAndAuditsWithoutChangingBookingFinancialsOrStatus()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn chính: 20; bàn dự phòng: 0]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.BilledTableCount = null;
        booking.SpareTableCount = null;
        await db.SaveChangesAsync();
        var before = new { booking.StatusId, booking.EstimatedTotal, booking.FinalHallPrice, booking.HallScheduleId, booking.GuestCount };

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, AttestationRequest(20, 0), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.WeddingBookings.AsNoTracking().SingleAsync();
        Assert.Equal(20, saved.BilledTableCount);
        Assert.Equal(0, saved.SpareTableCount);
        Assert.Equal(before, new { saved.StatusId, saved.EstimatedTotal, saved.FinalHallPrice, saved.HallScheduleId, saved.GuestCount });
        var audit = Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(10, audit.UserId);
        Assert.Equal("BOOKING_TABLE_COUNTS_ATTESTED", audit.Action);
        Assert.Contains("records/booking-1", audit.NewData);
        Assert.Contains("paper record", audit.NewData);
    }

    [Fact]
    public async Task ManagerAttestation_DoesNotTrustSpecialRequestsAndRejectsReplayWithoutDuplicateAudit()
    {
        await using var db = CreateDatabase();
        Seed(db, "[Bàn chính: 20; bàn dự phòng: 0]");
        var booking = await db.WeddingBookings.SingleAsync();
        booking.BilledTableCount = null;
        booking.SpareTableCount = null;
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.Manager, 10);

        var first = await controller.AttestTableCounts(1, AttestationRequest(20, 0), CancellationToken.None);
        var replay = await controller.AttestTableCounts(1, AttestationRequest(20, 0), CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(replay).StatusCode);
        Assert.Equal(20, (await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAttestation_RejectsInvalidCounts()
    {
        await using var db = CreateDatabase();
        Seed(db, null);

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, AttestationRequest(20, 20), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<BadRequestObjectResult>(result).StatusCode);
        Assert.Null((await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAttestation_RejectsGuestOrCurrentHallCapacityMismatch()
    {
        await using var db = CreateDatabase();
        Seed(db, null);

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, AttestationRequest(41, 0), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Null((await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAttestation_RequiresBilledCountMinusSpareToCoverGuestDerivedTables()
    {
        await using var db = CreateDatabase();
        Seed(db, null);

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, AttestationRequest(20, 1), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Null((await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAttestation_RejectsMissingSpareCountWhenInvokedDirectly()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        var request = AttestationRequest(20, 0);
        request.SpareTableCount = null;

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<BadRequestObjectResult>(result).StatusCode);
        Assert.Null((await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAttestation_RejectsTerminalBookingWithoutWritingAudit()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        (await db.WeddingBookings.SingleAsync()).StatusId = 805;
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).AttestTableCounts(1, AttestationRequest(20, 0), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Null((await db.WeddingBookings.AsNoTracking().SingleAsync()).BilledTableCount);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public void Controller_RequiresManagerOrHallManagerRole()
    {
        var attribute = Assert.Single(typeof(ManagerBookingChangesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal($"{RoleNames.Manager},{RoleNames.HallManager}", attribute.Roles);
        var attestationAuthorization = Assert.Single(typeof(ManagerBookingChangesController).GetMethod(nameof(ManagerBookingChangesController.AttestTableCounts))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, attestationAuthorization.Roles);
    }

    [Fact]
    public async Task FinancialCancelQuote_ActiveContractRequiresVerifiedPolicyAndBothPartyReference()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        var controller = CreateController(db, RoleNames.Manager, 10);

        var missingApproval = await controller.CancelQuote(1, new ManagerBookingCancelQuoteRequestDto
        {
            Origin = "CUSTOMER"
        }, CancellationToken.None);
        var valid = await controller.CancelQuote(1, ApprovedCancellationQuote(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(missingApproval);
        Assert.IsType<OkObjectResult>(valid);
        var methodAuthorization = Assert.Single(typeof(ManagerBookingChangesController).GetMethod(nameof(ManagerBookingChangesController.CancelQuote))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, methodAuthorization.Roles);
    }

    [Fact]
    public async Task HallManager_CannotQuoteOrCommitFinancialCancellationEvenWithinAssignedHall()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddHallManager(db, 20, 1);
        var controller = CreateController(db, RoleNames.HallManager, 20);

        var cancel = await controller.Cancel(1, new ManagerBookingCancelRequestDto { Reason = "escalate" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(cancel);
        var quoteAuthorization = Assert.Single(typeof(ManagerBookingChangesController).GetMethod(nameof(ManagerBookingChangesController.CancelQuote))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, quoteAuthorization.Roles);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
    }

    [Fact]
    public async Task FinancialCancel_StaleQuoteAfterReceiptChangeDoesNotMutateBookingOrContract()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingCancelQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.CancelQuote(1, ApprovedCancellationQuote(), CancellationToken.None)).Value);
        db.Payments.Single().Amount = 120m;
        await db.SaveChangesAsync();

        var result = await controller.Cancel(1, ApprovedCancellationRequest(quote.QuoteToken), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(BookingStatusCodes.Confirmed, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(ContractStatusCodes.Active, (await db.Contracts.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task FinancialCancel_ReplayReturnsStoredResultAndPreservesReceiptAndLockedSchedule()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Draft);
        var schedule = await db.HallSchedules.SingleAsync();
        schedule.StatusId = 303;
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingCancelQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.CancelQuote(1, new ManagerBookingCancelQuoteRequestDto { Origin = "CUSTOMER" }, CancellationToken.None)).Value);
        var request = new ManagerBookingCancelRequestDto
        {
            RequestId = Guid.NewGuid(), QuoteToken = quote.QuoteToken, Origin = "CUSTOMER", Reason = "Cancelled by customer"
        };
        var first = await controller.Cancel(1, request, CancellationToken.None);
        var replay = await controller.Cancel(1, request, CancellationToken.None);
        var changedPayload = await controller.Cancel(1, new ManagerBookingCancelRequestDto
        {
            RequestId = request.RequestId, QuoteToken = request.QuoteToken, Origin = request.Origin, Reason = "different reason"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        var replayResult = Assert.IsType<ManagerBookingCancelResultDto>(Assert.IsType<OkObjectResult>(replay).Value);
        Assert.True(replayResult.Replayed);
        Assert.IsType<ConflictObjectResult>(changedPayload);
        Assert.Equal(BookingStatusCodes.Cancelled, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(ContractStatusCodes.Cancelled, (await db.Contracts.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(HallScheduleStatusCodes.Locked, (await db.HallSchedules.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task FinancialCancel_RecordsPolicyAmountsWithoutChangingOriginalContractOrPayment()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingCancelQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.CancelQuote(1, ApprovedCancellationQuote(), CancellationToken.None)).Value);
        var paymentBefore = await db.Payments.AsNoTracking().SingleAsync();
        var contractBefore = await db.Contracts.AsNoTracking().SingleAsync();

        var result = await controller.Cancel(1, ApprovedCancellationRequest(quote.QuoteToken), CancellationToken.None);

        var cancellation = Assert.IsType<ManagerBookingCancelResultDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(50m, cancellation.RetainedDeposit);
        Assert.Equal(50m, cancellation.PrincipalRefundDue);
        Assert.Equal(50m, cancellation.DepositPrincipalPortion);
        Assert.Equal(paymentBefore.Amount, (await db.Payments.AsNoTracking().SingleAsync()).Amount);
        var savedContract = await db.Contracts.AsNoTracking().SingleAsync();
        Assert.Equal(contractBefore.TotalValue, savedContract.TotalValue);
        Assert.Equal(ContractStatusCodes.Cancelled, (await db.Contracts.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(BookingStatusCodes.Cancelled, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(HallScheduleStatusCodes.Available, (await db.HallSchedules.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        var operation = Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Equal("UC18-CANCEL-V1", operation.PolicyVersion);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task LinkedReschedule_UsesFreshFinancialQuoteAndExactReplayWithoutRewritingContractOrPayment()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None)).Value);
        Assert.True(quote.IsFinancialAmendment);
        Assert.Equal("UC18-RESCHEDULE-V1", quote.PolicyVersion);
        Assert.Equal(0m, quote.FeeAmount);
        Assert.Equal(1_000m, quote.EffectiveTotalAfterChange);
        var contractBefore = await db.Contracts.AsNoTracking().SingleAsync();
        var paymentBefore = await db.Payments.AsNoTracking().SingleAsync();
        var request = new ManagerBookingRescheduleRequestDto
        {
            NewDate = quote.NewDate, NewShift = quote.NewShift, NewHallId = quote.NewHallId,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference,
            PolicyVersion = "UC18-RESCHEDULE-V1", CustomerAgreementReference = "customer-proof-1",
            ManagerAgreementReference = "manager-proof-1", RequestId = Guid.NewGuid(), QuoteToken = quote.QuoteToken,
            Reason = "Agreed schedule change"
        };

        var first = Assert.IsType<ManagerBookingRescheduleResultDto>(Assert.IsType<OkObjectResult>(
            await controller.Reschedule(1, request, CancellationToken.None)).Value);
        var replay = Assert.IsType<ManagerBookingRescheduleResultDto>(Assert.IsType<OkObjectResult>(
            await controller.Reschedule(1, request, CancellationToken.None)).Value);
        var operation = Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<Uc18ContractAmendmentSnapshot>(operation.SnapshotJson!);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(Uc18BookingOperation.Reschedule, operation.Kind);
        Assert.Equal("CUSTOMER:customer-proof-1|MANAGER:manager-proof-1", operation.ManagerAgreementReference);
        Assert.NotNull(snapshot?.OriginalEvent);
        Assert.NotNull(snapshot?.PreviousEvent);
        Assert.NotNull(snapshot?.CurrentEvent);
        Assert.Equal(contractBefore.TotalValue, (await db.Contracts.AsNoTracking().SingleAsync()).TotalValue);
        Assert.Equal(paymentBefore.Amount, (await db.Payments.AsNoTracking().SingleAsync()).Amount);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task LinkedReschedule_ReceiptChangeMakesQuoteStaleWithoutMutation()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None)).Value);
        db.Payments.Single().Amount = 110m;
        await db.SaveChangesAsync();

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = quote.NewDate, NewShift = quote.NewShift, NewHallId = quote.NewHallId,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference,
            PolicyVersion = "UC18-RESCHEDULE-V1", CustomerAgreementReference = "customer-proof-1",
            ManagerAgreementReference = "manager-proof-1", RequestId = Guid.NewGuid(), QuoteToken = quote.QuoteToken,
            Reason = "Agreed schedule change"
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(29, 5_000_000)]
    [InlineData(30, 0)]
    public async Task LinkedRescheduleQuote_AppliesFirstMoveFeeAtTheThirtyDayBoundary(int noticeDays, decimal expectedFee)
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        db.HallSchedules.Single().Date = DateTime.Today.AddDays(noticeDays);
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(expectedFee, quote.FeeAmount);
        Assert.Equal(1_000m + expectedFee, quote.EffectiveTotalAfterChange);
    }

    [Fact]
    public async Task LinkedMenuOnlyQuote_HasNoRescheduleFee()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        SetValidTableCounts(db);
        SeedMenuCatalog(db);

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto
            {
                NewDate = DateTime.Today.AddDays(60), NewShift = "Ca trưa", NewHallId = 1,
                ChangeMenu = true, MenuId = 1
            }, CancellationToken.None);

        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(0m, quote.FeeAmount);
    }

    [Fact]
    public async Task LinkedRescheduleEndpoints_RequireManagerEvenForAssignedHallManager()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        AddHallManager(db, 20, 1);
        var controller = CreateController(db, RoleNames.HallManager, 20);
        var quote = await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
        { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None);
        var commit = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None);

        Assert.IsType<ForbidResult>(quote);
        Assert.IsType<ForbidResult>(commit);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
    }

    [Fact]
    public async Task LinkedRescheduleQuote_AllocatesOtherReceiptsBeforeDepositRefund()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        db.Contracts.Single().TotalValue = 100m;
        db.Payments.AddRange(
            new Payment { PaymentId = 1, ContractId = 1, Amount = 60m, PaymentType = PaymentTypes.Deposit, PaymentDate = DateTime.Now, StatusId = 1001 },
            new Payment { PaymentId = 2, ContractId = 1, Amount = 70m, PaymentType = PaymentTypes.Installment, PaymentDate = DateTime.Now, StatusId = 1001 });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.Manager, 10).Quote(1,
            new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(result).Value);

        Assert.Equal(30m, quote.RefundDue);
        Assert.Equal(0m, quote.DepositPrincipalRefundDue);
    }

    [Fact]
    public async Task LinkedReschedule_RejectsMissingTwoPartyAssentWithoutMutation()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None)).Value);

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = quote.NewDate, NewShift = quote.NewShift, NewHallId = quote.NewHallId,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference,
            PolicyVersion = "UC18-RESCHEDULE-V1", RequestId = Guid.NewGuid(), QuoteToken = quote.QuoteToken,
            Reason = "No assent references"
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
    }

    [Fact]
    public async Task LinkedReschedule_ToLockedScheduleLeavesFinancialAndBookingStateUntouched()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddContract(db, ContractStatusCodes.Active);
        AddSuccessfulDeposit(db, 100m);
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = 2, HallId = 1, Date = DateTime.Today.AddDays(60), Shift = "Ca tối",
            StatusId = 303, DataStatusId = 1
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.Manager, 10);
        var quote = Assert.IsType<ManagerBookingRescheduleQuoteDto>(Assert.IsType<OkObjectResult>(
            await controller.Quote(1, new ManagerBookingRescheduleQuoteRequestDto
            { NewDate = DateTime.Today.AddDays(60), NewShift = "Ca tối", NewHallId = 1 }, CancellationToken.None)).Value);

        var result = await controller.Reschedule(1, new ManagerBookingRescheduleRequestDto
        {
            NewDate = quote.NewDate, NewShift = quote.NewShift, NewHallId = quote.NewHallId,
            PriceDifferenceConfirmed = true, ConfirmedPriceDifference = quote.PriceDifference,
            PolicyVersion = "UC18-RESCHEDULE-V1", CustomerAgreementReference = "customer-proof-1",
            ManagerAgreementReference = "manager-proof-1", RequestId = Guid.NewGuid(), QuoteToken = quote.QuoteToken,
            Reason = "Agreed schedule change"
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, (await db.WeddingBookings.AsNoTracking().SingleAsync()).HallScheduleId);
        Assert.Equal(HallScheduleStatusCodes.Locked,
            (await db.HallSchedules.Include(x => x.Status).SingleAsync(x => x.HallScheduleId == 2)).Status.StatusCode);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
    }

    [Fact]
    public async Task HallManagerHandoff_NotifiesManagerAndReplaysWithoutChangingBooking()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddHallManager(db, 20, 1);
        db.Roles.Add(new Role { RoleId = 1, RoleName = RoleNames.Manager });
        db.Users.Add(new User
        {
            UserId = 10, RoleId = 1, Email = "manager@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, RoleNames.HallManager, 20);
        var request = new ManagerBookingHandoffRequestDto
        {
            RequestId = Guid.NewGuid(), Operation = "RESCHEDULE", Reason = "Needs financial approval"
        };

        var first = Assert.IsType<ManagerBookingHandoffResultDto>(Assert.IsType<OkObjectResult>(
            await controller.Handoff(1, request, new NotificationService(db), CancellationToken.None)).Value);
        var replay = Assert.IsType<ManagerBookingHandoffResultDto>(Assert.IsType<OkObjectResult>(
            await controller.Handoff(1, request, new NotificationService(db), CancellationToken.None)).Value);
        var changed = await controller.Handoff(1, new ManagerBookingHandoffRequestDto
        {
            RequestId = request.RequestId, Operation = request.Operation, Reason = "Different request"
        }, new NotificationService(db), CancellationToken.None);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.IsType<ConflictObjectResult>(changed);
        Assert.Equal(BookingStatusCodes.Confirmed, (await db.WeddingBookings.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Equal(1, (await db.WeddingBookings.SingleAsync()).HallScheduleId);
        Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
        var notification = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(10, notification.RecipientUserId);
        Assert.Equal("BOOKING_HANDOFF", notification.Type);
        var authorization = Assert.Single(typeof(ManagerBookingChangesController).GetMethod(nameof(ManagerBookingChangesController.Handoff))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.HallManager, authorization.Roles);
    }

    [Fact]
    public async Task HallManagerHandoff_ExpiredPendingRescheduleCanBeHandedOff()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddHallManager(db, 20, 1);
        db.Roles.Add(new Role { RoleId = 1, RoleName = RoleNames.Manager });
        db.Users.Add(new User { UserId = 10, RoleId = 1, Email = "manager@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1 });
        db.Statuses.Add(new Status { StatusId = 1100, StatusGroup = StatusGroups.ScheduleChange,
            StatusCode = ScheduleChangeStatusCodes.Pending, StatusName = "Pending" });
        var booking = await db.WeddingBookings.SingleAsync();
        booking.StatusId = 800;
        booking.BookedAt = DateTime.Now.AddHours(-BookingHoldExpirationService.HoldHours - 1);
        db.WeddingScheduleChanges.Add(new WeddingScheduleChange
        {
            ScheduleChangeId = 1, BookingId = booking.BookingId, OldHallScheduleId = booking.HallScheduleId,
            NewHallScheduleId = booking.HallScheduleId, StatusId = 1100, RequestedAt = booking.BookedAt
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Handoff(1,
            new ManagerBookingHandoffRequestDto { RequestId = Guid.NewGuid(), Operation = "RESCHEDULE", Reason = "Expired hold" },
            new NotificationService(db), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task HallManagerHandoff_ActiveRescheduleRequestStillBlocksHandoff()
    {
        await using var db = CreateDatabase();
        Seed(db, null);
        AddHallManager(db, 20, 1);
        db.Statuses.Add(new Status { StatusId = 1100, StatusGroup = StatusGroups.ScheduleChange,
            StatusCode = ScheduleChangeStatusCodes.Pending, StatusName = "Pending" });
        var booking = await db.WeddingBookings.SingleAsync();
        db.WeddingScheduleChanges.Add(new WeddingScheduleChange
        {
            ScheduleChangeId = 1, BookingId = booking.BookingId, OldHallScheduleId = booking.HallScheduleId,
            NewHallScheduleId = booking.HallScheduleId, StatusId = 1100, RequestedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db, RoleNames.HallManager, 20).Handoff(1,
            new ManagerBookingHandoffRequestDto { RequestId = Guid.NewGuid(), Operation = "RESCHEDULE", Reason = "Active request" },
            new NotificationService(db), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await db.Set<Uc18BookingOperation>().ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    private static void AddContract(ApplicationDbContext db, string contractStatus)
    {
        db.Statuses.AddRange(
            new Status { StatusId = 901, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Draft" },
            new Status { StatusId = 902, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Active" },
            new Status { StatusId = 903, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Cancelled, StatusName = "Cancelled" },
            new Status { StatusId = 1001, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Success" },
            new Status { StatusId = 1002, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Pending, StatusName = "Pending" });
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "HD-1", TotalValue = 1000m,
            StatusId = contractStatus == ContractStatusCodes.Active ? 902 : 901 });
        db.SaveChanges();
    }

    private static void AddSuccessfulDeposit(ApplicationDbContext db, decimal amount)
    {
        db.Payments.Add(new Payment { PaymentId = 1, ContractId = 1, Amount = amount, PaymentType = PaymentTypes.Deposit,
            PaymentDate = DateTime.Now, StatusId = 1001 });
        db.SaveChanges();
    }

    private static ManagerBookingCancelQuoteRequestDto ApprovedCancellationQuote() => new()
    {
        Origin = "CUSTOMER", PolicyVersion = "UC18-CANCEL-V1", AgreementReference = "both-parties/acceptance-42"
    };

    private static ManagerBookingCancelRequestDto ApprovedCancellationRequest(string quoteToken) => new()
    {
        RequestId = Guid.NewGuid(), QuoteToken = quoteToken, Origin = "CUSTOMER", PolicyVersion = "UC18-CANCEL-V1",
        AgreementReference = "both-parties/acceptance-42", Reason = "Cancellation after approval"
    };

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(DatabaseName, DatabaseRoot)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var reset = new ApplicationDbContext(options);
        reset.Database.EnsureDeleted();
        return new ApplicationDbContext(options);
    }

    private static ManagerBookingRescheduleQuoteRequestDto QuoteRequest(int hallId) => new()
    {
        NewDate = DateTime.Today.AddDays(40), NewShift = "Ca trưa", NewHallId = hallId
    };

    private static AttestBookingTableCountsRequestDto AttestationRequest(int billed, int spare) => new()
    {
        BilledTableCount = billed, SpareTableCount = spare,
        SourceReference = "records/booking-1", Reason = "paper record reviewed"
    };

    private static void Seed(ApplicationDbContext db, string? specialRequests)
    {
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Đang dùng" });
        db.Statuses.AddRange(
            new Status { StatusId = 201, StatusGroup = StatusGroups.Hall, StatusCode = HallStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 401, StatusGroup = StatusGroups.Menu, StatusCode = MenuStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 402, StatusGroup = StatusGroups.Menu, StatusCode = MenuStatusCodes.Inactive, StatusName = "Ngừng hoạt động" },
            new Status { StatusId = 501, StatusGroup = StatusGroups.Dish, StatusCode = DishStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 701, StatusGroup = StatusGroups.Service, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "AVAILABLE" },
            new Status { StatusId = 303, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "LOCKED" },
            new Status { StatusId = 1, StatusGroup = StatusGroups.Account, StatusCode = BusinessStatusCodes.Active, StatusName = "Hoạt động" },
            new Status { StatusId = 800, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" },
            new Status { StatusId = 801, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" },
            new Status { StatusId = 805, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new Status { StatusId = 1302, StatusGroup = StatusGroups.ScheduleChange, StatusCode = ScheduleChangeStatusCodes.Approved, StatusName = "Đã duyệt" });
        db.Halls.AddRange(
            new Hall { HallId = 1, HallCode = "H1", HallName = "Sảnh cũ", MinimumCapacity = 100, MaximumCapacity = 400, RentalPrice = 500m, StatusId = 201, DataStatusId = 1 },
            new Hall { HallId = 2, HallCode = "H2", HallName = "Sảnh mới", MinimumCapacity = 100, MaximumCapacity = 210, RentalPrice = 900m, StatusId = 201, DataStatusId = 1 });
        db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "C1", FullName = "Khách", PhoneNumber = "0900000000", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = 1, HallId = 1, Date = DateTime.Today.AddDays(60), Shift = "Ca trưa", StatusId = 301, DataStatusId = 1 });
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 1, BookingCode = "DT-TEST-1", CustomerId = 1, HallScheduleId = 1, GuestCount = 200,
            FinalHallPrice = 500m, FinalMenuPrice = 100m, EstimatedTotal = 1000m, SpecialRequests = specialRequests,
            StatusId = 801, BookedAt = DateTime.Now
        });
        db.SaveChanges();
    }

    private static void SeedMenuCatalog(ApplicationDbContext db)
    {
        db.Menus.Add(new Menu { MenuId = 1, MenuCode = "M1", MenuName = "Catalogue menu", MenuType = "STANDARD", PricePerTable = 160m, StatusId = 401, DataStatusId = 1 });
        db.Dishes.AddRange(
            new Dish { DishId = 1, DishCode = "D1", DishName = "Dish A", Category = "Món chính", Price = 80m, StatusId = 501, DataStatusId = 1 },
            new Dish { DishId = 2, DishCode = "D2", DishName = "Dish B", Category = "Món chính", Price = 120m, StatusId = 501, DataStatusId = 1 });
        db.SaveChanges();
    }

    private static void AddScheduleChangeHold(ApplicationDbContext db, int targetScheduleId, DateTime expiresAt)
    {
        AddScheduleChange(db, ScheduleChangeStatusCodes.Expired, targetScheduleId);
        db.ScheduleChangeHolds.Add(new ScheduleChangeHold { ScheduleChangeId = 1, HallScheduleId = targetScheduleId,
            Status = ScheduleChangeHoldStatusCodes.Active, StartedAt = DateTime.Now, ExpiresAt = expiresAt });
    }

    private static void AddScheduleChange(ApplicationDbContext db, string status, int targetScheduleId = 1)
    {
        db.Statuses.Add(new Status { StatusId = 1301, StatusGroup = StatusGroups.ScheduleChange,
            StatusCode = status, StatusName = status });
        db.ScheduleChangePolicies.Add(new ScheduleChangePolicy { PolicyId = 1, PolicyCode = "P1", Version = 1,
            OnlineMinimumDays = 7, NewDateMinimumDays = 14, PaymentWindowMinutes = 10, HoldWindowMinutes = 10,
            Status = ScheduleChangePolicyStatusCodes.Active, EffectiveFrom = DateTime.Today, CreatedAt = DateTime.Now });
        db.WeddingScheduleChanges.Add(new WeddingScheduleChange { ScheduleChangeId = 1, BookingId = 1,
            OldHallScheduleId = 1, NewHallScheduleId = targetScheduleId, StatusId = 1301, RequestedAt = DateTime.Now,
            PolicyId = 1, PolicyVersion = 1, SubmittedDaysBeforeEvent = 40 });
    }

    private static void SetValidTableCounts(ApplicationDbContext db)
    {
        var booking = db.WeddingBookings.Single();
        booking.BilledTableCount = 20;
        booking.SpareTableCount = 0;
        db.SaveChanges();
    }

    private static void AddHallManager(ApplicationDbContext db, int userId, int hallId)
    {
        db.Roles.Add(new Role { RoleId = 3, RoleName = RoleNames.HallManager });
        db.Users.Add(new User { UserId = userId, RoleId = 3, Email = "hm@test.local", PasswordHash = "test", StatusId = 1, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = 30, UserId = userId, EmployeeCode = "HM030", FullName = "Hall Manager", PhoneNumber = "0910000000", DataStatusId = 1 });
        db.HallManagerAssignments.Add(new HallManagerAssignment
        {
            HallManagerAssignmentId = 1, HallId = hallId, HallManagerEmployeeId = 30,
            FromDate = DateTime.Today.AddDays(-1), ToDate = null
        });
    }

    private static ManagerBookingChangesController CreateController(ApplicationDbContext db, string role, int userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)
        ], "Test"));
        return new ManagerBookingChangesController(db, new StatusService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }
}

public sealed class ManagerBookingChangesAuthorizationTests : IClassFixture<EventCoordinationApiFactory>
{
    private readonly EventCoordinationApiFactory _factory;
    public ManagerBookingChangesAuthorizationTests(EventCoordinationApiFactory factory) => _factory = factory;

    private void EnsureExistingDataStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.DataStatuses.Any(x => x.DataStatusCode == DataStatusCodes.Existing))
        {
            db.DataStatuses.Add(new DataStatus
            { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Đang dùng" });
            db.SaveChanges();
        }
    }

    [Fact]
    public void LegacyAdminBooking_CombinedPolicyKeepsAdminReadsAndCreator()
    {
        var controllerType = typeof(AdminBookingController);
        var controllerPolicy = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal($"{RoleNames.Admin},{RoleNames.Manager}", controllerPolicy.Roles);
        Assert.Empty(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.CreateBooking))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Empty(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.GetBookings))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Empty(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.GetBookingDetail))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal(RoleNames.Manager, Assert.Single(typeof(AdminBookingController).GetMethod(nameof(AdminBookingController.UpdateBookingStatus))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(RoleNames.Manager, 200)]
    [InlineData(RoleNames.HallManager, 200)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    [InlineData(RoleNames.Customer, 403)]
    public async Task BookingChangeApi_DeniesRolesOutsideManagerAndHallManager(string? role, int expectedStatus)
    {
        EnsureExistingDataStatus();
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.GetAsync("/api/manager/bookings");

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Manager, 400)]
    [InlineData(RoleNames.HallManager, 403)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    [InlineData(RoleNames.Customer, 403)]
    public async Task TableCountAttestationApi_IsManagerOnly(string role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.PostAsJsonAsync("/api/manager/bookings/1/table-count-attestation", new
        {
            billedTableCount = 20, spareTableCount = 20, sourceReference = "records/booking-1", reason = "paper record reviewed"
        });

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Manager, 409)]
    [InlineData(RoleNames.Admin, 403)]
    [InlineData(RoleNames.HallManager, 403)]
    [InlineData(RoleNames.Coordinator, 403)]
    [InlineData(RoleNames.Customer, 403)]
    public async Task LegacyStatusMutation_CannotBeUsedByAdminOrNonManagerRoles(string role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.PutAsJsonAsync("/api/admin/bookings/1/status", new { statusCode = "CANCELLED" });

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/bookings", RoleNames.Admin, 200)]
    [InlineData("/api/admin/bookings/1", RoleNames.Admin, 0)]
    [InlineData("/api/admin/bookings", RoleNames.Manager, 200)]
    [InlineData("/api/admin/bookings/1", RoleNames.Manager, 0)]
    [InlineData("/api/admin/bookings", RoleNames.HallManager, 403)]
    [InlineData("/api/admin/bookings/1", RoleNames.HallManager, 403)]
    public async Task LegacyBookingReads_AllowAdminsAndManagersOnly(string path, string role, int expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);

        using var response = await client.GetAsync(path);

        if (expectedStatus == 0)
            Assert.NotEqual(403, (int)response.StatusCode);
        else
            Assert.Equal(expectedStatus, (int)response.StatusCode);
    }
}

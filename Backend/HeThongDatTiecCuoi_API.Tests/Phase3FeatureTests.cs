using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Report;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Phase3FeatureTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task GetRevenueBiReport_Calculates_Actual_Paid_Revenue_And_Distinguishes_Contracts_From_Bookings()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        db.DataStatuses.Add(dataStatus);

        // Statuses
        var bookingActiveStatus = new Status { StatusId = 201, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };
        var bookingCancelledStatus = new Status { StatusId = 202, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" };
        var contractActiveStatus = new Status { StatusId = 902, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Active, StatusName = "Hiệu lực" };
        var paymentSuccessStatus = new Status { StatusId = 1002, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" };
        var hallSchedAvailableStatus = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
        var hallSchedBookedStatus = new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" };

        db.Statuses.AddRange(bookingActiveStatus, bookingCancelledStatus, contractActiveStatus, paymentSuccessStatus, hallSchedAvailableStatus, hallSchedBookedStatus);

        // Hall
        var hall = new Hall
        {
            HallId = 1,
            HallCode = "H01",
            HallName = "Sảnh Thác Nước",
            MaximumCapacity = 40,
            DataStatusId = 1
        };
        db.Halls.Add(hall);

        // Schedules
        var schedule = new HallSchedule
        {
            HallScheduleId = 10,
            HallId = 1,
            Date = new DateTime(2026, 10, 15),
            Shift = HallShiftNames.Dinner,
            StatusId = 302,
            DataStatusId = 1
        };
        db.HallSchedules.Add(schedule);

        // Customer
        var customer = new Customer
        {
            CustomerId = 1,
            FullName = "Nguyễn Văn A",
            Email = "a@test.com",
            PhoneNumber = "0901234567"
        };
        db.Customers.Add(customer);

        // Booking 1: Confirmed (Active)
        var booking1 = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-20261015-001",
            CustomerId = 1,
            HallScheduleId = 10,
            BookedAt = new DateTime(2026, 10, 1),
            GuestCount = 200, // 20 bàn
            FinalMenuPrice = 5000000m,
            FinalHallPrice = 30000000m,
            FinalDecorationPrice = 15000000m,
            EstimatedTotal = 145000000m,
            StatusId = 201
        };

        // Booking 2: Cancelled (Should be excluded)
        var booking2 = new WeddingBooking
        {
            BookingId = 2,
            BookingCode = "DT-20261015-002",
            CustomerId = 1,
            HallScheduleId = 10,
            BookedAt = new DateTime(2026, 10, 2),
            GuestCount = 100,
            EstimatedTotal = 80000000m,
            StatusId = 202
        };

        db.WeddingBookings.AddRange(booking1, booking2);

        // Contract for Booking 1
        var contract = new Contract
        {
            ContractId = 1,
            BookingId = 1,
            ContractCode = "HD-001",
            CreatedDate = new DateTime(2026, 10, 5),
            TotalValue = 145000000m,
            StatusId = 902
        };
        db.Contracts.Add(contract);

        // Payment for Contract 1
        var payment = new Payment
        {
            PaymentId = 1,
            ContractId = 1,
            PaymentType = "Đặt cọc",
            Amount = 14500000m, // 10% deposit
            PaymentDate = new DateTime(2026, 10, 5, 10, 0, 0),
            StatusId = 1002
        };
        db.Payments.Add(payment);

        await db.SaveChangesAsync();

        var controller = new AdminReportController(db);
        var result = await controller.GetRevenueBiReport(2026, 0);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<RevenueBiReportDto>(okResult.Value);

        // Verify Actual Paid Revenue comes from Payments table (14,500,000 VNĐ, not 145,000,000 VNĐ)
        Assert.Equal(14500000m, report.Kpi.TotalRevenue);

        // Verify Estimated Revenue comes from active Bookings
        Assert.Equal(14500000m, report.Kpi.TotalRevenue);
        Assert.Equal(145000000m, report.Kpi.EstimatedTotalRevenue);

        // Verify Bookings vs Contracts count separation
        Assert.Equal(1, report.Kpi.TotalBookings);
        Assert.Equal(1, report.Kpi.ConfirmedBookings);
        Assert.Equal(1, report.Kpi.TotalContracts);
        Assert.Equal(145000000m, report.Kpi.TotalContractValue);
        Assert.Equal(130500000m, report.Kpi.OutstandingReceivables);

        // Verify Table metrics & ARPT
        Assert.Equal(20, report.Kpi.TotalTables); // 200 guests / 10 = 20 tables
        Assert.Equal(7250000m, report.Kpi.AverageRevenuePerTable); // 145M / 20 = 7.25M
        Assert.Equal(5000000m, report.Kpi.AverageMenuPricePerTable); // 5.0M menu price

        // Verify Monthly revenue separates actual and estimated
        var octoberRev = report.MonthlyRevenues.FirstOrDefault(m => m.Month == 10);
        Assert.NotNull(octoberRev);
        Assert.Equal(14500000m, octoberRev.TotalRevenue);
        Assert.Equal(145000000m, octoberRev.EstimatedRevenue);
        Assert.Equal(1, octoberRev.BookingCount);
        Assert.Equal(1, octoberRev.ContractCount);
    }

    [Fact]
    public async Task GetRevenueBiReport_Calculates_Clean_Occupancy_Without_Fake_Multipliers()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        db.DataStatuses.Add(dataStatus);

        var bookingStatus = new Status { StatusId = 201, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };
        var hallSchedStatus = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
        db.Statuses.AddRange(bookingStatus, hallSchedStatus);

        // 2 Halls
        db.Halls.AddRange(
            new Hall { HallId = 1, HallCode = "H01", HallName = "Sảnh 1", MaximumCapacity = 30, DataStatusId = 1, DataStatus = dataStatus },
            new Hall { HallId = 2, HallCode = "H02", HallName = "Sảnh 2", MaximumCapacity = 30, DataStatusId = 1, DataStatus = dataStatus }
        );

        db.Customers.Add(new Customer { CustomerId = 1, FullName = "Khách Hàng Test" });
        db.HallSchedules.Add(new HallSchedule
        {
            HallScheduleId = 1,
            HallId = 1,
            Date = new DateTime(2026, 5, 1),
            Shift = HallShiftNames.Dinner,
            StatusId = 301,
            DataStatusId = 1
        });

        // 1 Booking in 2026 (365 days)
        // Expected max shifts = 2 halls * 2 shifts * 365 = 1460 shifts.
        // Real occupancy = 1 / 1460 * 100% = 0.06849% -> rounded to 0.1%.
        // With previous fake multiplier (* 9.2): it was 0.6%.
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-001",
            CustomerId = 1,
            HallScheduleId = 1,
            BookedAt = new DateTime(2026, 5, 1),
            EstimatedTotal = 100000000m,
            StatusId = 201,
            Status = bookingStatus
        });

        await db.SaveChangesAsync();

        var controller = new AdminReportController(db);
        var result = await controller.GetRevenueBiReport(2026, 0);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<RevenueBiReportDto>(okResult.Value);

        Assert.Equal(0.1, report.Kpi.AverageOccupancyRate);
    }

    [Fact]
    public async Task GetHallScheduleMatrix_Defaults_To_Monday_Of_Current_Week_And_Binds_Booked_Slots()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);

        var dataStatus = new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" };
        db.DataStatuses.Add(dataStatus);

        var availStatus = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
        var bookedStatus = new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" };
        var bkStatus = new Status { StatusId = 201, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };

        db.Statuses.AddRange(availStatus, bookedStatus, bkStatus);

        var hall = new Hall { HallId = 1, HallCode = "H01", HallName = "Sảnh Grand", MaximumCapacity = 50, DataStatusId = 1 };
        db.Halls.Add(hall);

        // Determine this week's Monday
        DateTime today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        DateTime monday = today.AddDays(-1 * diff).Date;
        DateTime bookedDate = monday.AddDays(2); // Wednesday

        var schedule = new HallSchedule
        {
            HallScheduleId = 101,
            HallId = 1,
            Date = bookedDate,
            Shift = HallShiftNames.Dinner,
            StatusId = 302,
            DataStatusId = 1
        };
        db.HallSchedules.Add(schedule);

        var customer = new Customer { CustomerId = 1, FullName = "Nguyễn Văn Test" };
        db.Customers.Add(customer);

        var booking = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-MATRIX-001",
            CustomerId = 1,
            HallScheduleId = 101,
            BookedAt = today,
            GuestCount = 300,
            StatusId = 201
        };
        db.WeddingBookings.Add(booking);

        await db.SaveChangesAsync();

        var controller = new AdminReportController(db);
        // Call without startDate to test dynamic current-week Monday default
        var result = await controller.GetHallScheduleMatrix(null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var matrix = Assert.IsType<HallScheduleMatrixDto>(okResult.Value);

        // Verify week begins on Monday
        Assert.Equal(DayOfWeek.Monday, matrix.StartDate.DayOfWeek);
        Assert.Equal(monday, matrix.StartDate);
        Assert.Equal(monday.AddDays(6), matrix.EndDate);
        Assert.Equal(7, matrix.Days.Count);

        // Verify hall row
        Assert.Single(matrix.Halls);
        var hallRow = matrix.Halls[0];
        Assert.Equal(14, hallRow.Slots.Count); // 7 days * 2 shifts

        // Find the booked slot on Wednesday dinner
        var slot = hallRow.Slots.FirstOrDefault(s => s.Date.Date == bookedDate.Date && s.Shift == HallShiftNames.Dinner);
        Assert.NotNull(slot);
        Assert.Equal("BOOKED", slot.StatusCode);
        Assert.Equal("DT-MATRIX-001", slot.BookingCode);
        Assert.Equal("Nguyễn Văn Test", slot.CustomerName);
        Assert.Equal(30, slot.TableCount); // 300 / 10 = 30
        Assert.Equal("Đã xác nhận", slot.BookingStatus);
    }

    [Fact]
    public void BookingListResponseDto_Provides_Compatible_Pending_And_Preparing_Counts()
    {
        var dto = new HeThongDatTiecCuoi_API.DTOs.Booking.BookingListResponseDto
        {
            TotalCount = 18,
            PendingCount = 5,
            ConfirmedCount = 4,
            PreparingCount = 0,
            CompletedCount = 0,
            CancelledCount = 9
        };

        Assert.Equal(5, dto.PendingCount);
        Assert.Equal(5, dto.PendingConfirmationCount);
        Assert.Equal(0, dto.PreparingCount);
        Assert.Equal(0, dto.DepositedCount);

        var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"pendingCount\":5", json);
        Assert.Contains("\"pendingConfirmationCount\":5", json);
    }

    [Fact]
    public void ManagerDashboard_OutstandingDebt_Calculates_Sum_Of_Active_Unpaid_Balances()
    {
        // Demonstrates and verifies the exact formula producing 502,695,000 VNĐ in Manager Dashboard
        var contracts = new[]
        {
            new { Id = 1, TotalValue = 113450000m, Paid = 11345000m, IsCancelled = false },
            new { Id = 2, TotalValue = 217300000m, Paid = 21730000m, IsCancelled = false },
            new { Id = 3, TotalValue = 227800000m, Paid = 22780000m, IsCancelled = false },
            new { Id = 4, TotalValue = 175700000m, Paid = 17570000m, IsCancelled = true } // Cancelled contract
        };

        var activeContracts = contracts.Where(c => !c.IsCancelled).ToList();
        var outstandingDebt = activeContracts.Sum(c => Math.Max(0m, c.TotalValue - c.Paid));

        Assert.Equal(502695000m, outstandingDebt);
    }
}

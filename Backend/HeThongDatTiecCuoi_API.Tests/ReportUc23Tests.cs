using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Report;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

/// <summary>
/// UC23 – báo cáo doanh thu: phạm vi Manager, bộ lọc tháng/sảnh/ca và số liệu thực thu.
/// Dùng in-memory database, không khởi động service hay chạy SQL.
/// </summary>
public sealed class ReportUc23Tests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public void AdminReportController_IsRestrictedToManagerRole()
    {
        var authorize = typeof(AdminReportController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(RoleNames.Manager, authorize!.Roles);
    }

    [Fact]
    public async Task Report_FiltersByMonthHallAndShift_AndSeparatesActualFromEstimatedRevenue()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);
        var seed = await SeedReportData(db);

        var controller = new AdminReportController(db);
        var result = await controller.GetRevenueBiReport(
            year: 2026, quarter: 0, month: 10, hallId: seed.HallId, shift: HallShiftNames.Dinner);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<RevenueBiReportDto>(okResult.Value);

        Assert.Equal(10, report.Month);
        Assert.Equal(seed.HallId, report.HallId);
        Assert.Equal(HallShiftNames.Dinner, report.Shift);

        // 14.5M từ phiếu thu thành công của hợp đồng COMPLETED, không lấy EstimatedTotal 145M.
        Assert.Equal(14_500_000m, report.Kpi.TotalRevenue);
        Assert.Equal(145_000_000m, report.Kpi.EstimatedTotalRevenue);

        // Mẫu số công suất dùng đúng số ca đang chọn (1 ca) chứ không luôn nhân đôi.
        Assert.Equal(3.2, report.Kpi.AverageOccupancyRate);

        // Chỉ một tháng được trả về khi lọc tháng.
        var month = Assert.Single(report.MonthlyRevenues);
        Assert.Equal(10, month.Month);
        Assert.Equal(14_500_000m, month.TotalRevenue);
        Assert.Equal(145_000_000m, month.EstimatedRevenue);

        var hall = Assert.Single(report.HallPerformances);
        Assert.Equal(14_500_000m, hall.TotalRevenue);
        Assert.Equal(145_000_000m, hall.EstimatedRevenue);

        // Sự kiện đếm theo ngày tổ chức: chỉ tiệc đã hoàn tất mới là "đã tổ chức"; 1 tiệc hủy.
        Assert.Equal(1, report.Kpi.EventsHeld);
        Assert.Equal(1, report.Kpi.EventsCancelled);
    }

    [Fact]
    public async Task Report_ComputesShiftOccupancyWithoutFakeMultipliers_AndMarksHygieneUnavailable()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);
        await SeedReportData(db);

        var controller = new AdminReportController(db);
        var result = await controller.GetRevenueBiReport(year: 2026, quarter: 0, month: 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<RevenueBiReportDto>(okResult.Value);

        // 2 sảnh * 31 ngày = 62 ca mỗi ca; ca tối có 1 tiệc đã chiếm.
        var dinner = Assert.Single(report.OccupancyByShift, item => item.Shift == HallShiftNames.Dinner);
        Assert.Equal(1, dinner.OccupiedSlots);
        Assert.Equal(62, dinner.MaxSlots);
        Assert.Equal(1.6, dinner.OccupancyRate);

        // Ca trưa có 1 ca bị khóa theo lịch nên mẫu số giảm còn 61, không tính ca khóa là công suất.
        var lunch = Assert.Single(report.OccupancyByShift, item => item.Shift == HallShiftNames.Lunch);
        Assert.Equal(0, lunch.OccupiedSlots);
        Assert.Equal(61, lunch.MaxSlots);
        Assert.Equal(0, lunch.OccupancyRate);

        // Điểm vệ sinh không tồn tại trong schema nên phải báo không khả dụng.
        Assert.False(report.Quality.HygieneScoreAvailable);
        Assert.Null(report.Quality.AverageHygieneScore);
        Assert.Equal(2, report.Quality.ReviewCount);
        Assert.Equal(4.5, report.Quality.AverageOverallScore!.Value);
        Assert.False(report.Kpi.ForfeitedDepositAvailable);
        Assert.Null(report.Kpi.ForfeitedDepositAmount);
    }

    [Fact]
    public async Task Report_RejectsInvalidFilters()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);
        await SeedReportData(db);
        var controller = new AdminReportController(db);

        Assert.IsType<BadRequestObjectResult>(
            await controller.GetRevenueBiReport(year: 2026, quarter: 1, month: 5));
        Assert.IsType<BadRequestObjectResult>(
            await controller.GetRevenueBiReport(year: 2026, quarter: 0, month: 13));
        Assert.IsType<BadRequestObjectResult>(
            await controller.GetRevenueBiReport(year: 2026, quarter: 0, shift: "Ca sáng"));
        Assert.IsType<BadRequestObjectResult>(
            await controller.GetRevenueBiReport(year: 2026, quarter: 0, hallId: 999));
    }

    [Fact]
    public async Task Report_OnlyCountsSuccessfulPaymentsOfCompletedContracts()
    {
        var options = CreateInMemoryOptions();
        await using var db = new ApplicationDbContext(options);
        await SeedReportData(db);

        // Hợp đồng ACTIVE có phiếu thu thành công không được tính vào doanh thu thực thu.
        var activeStatus = new Status
        {
            StatusId = 903,
            StatusGroup = StatusGroups.Contract,
            StatusCode = ContractStatusCodes.Active,
            StatusName = "Hiệu lực"
        };
        db.Statuses.Add(activeStatus);
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 3,
            BookingCode = "DT-20261020-003",
            CustomerId = 1,
            HallScheduleId = 10,
            BookedAt = new DateTime(2026, 10, 20),
            GuestCount = 80,
            EstimatedTotal = 50_000_000m,
            StatusId = 201
        });
        var extraContract = new Contract
        {
            ContractId = 2,
            BookingId = 3,
            ContractCode = "HD-ACTIVE",
            CreatedDate = new DateTime(2026, 10, 20),
            TotalValue = 50_000_000m,
            StatusId = 903
        };
        db.Contracts.Add(extraContract);
        db.Payments.Add(new Payment
        {
            PaymentId = 2,
            ContractId = 2,
            PaymentType = "Đặt cọc",
            Amount = 5_000_000m,
            PaymentDate = new DateTime(2026, 10, 20),
            StatusId = 1002
        });
        await db.SaveChangesAsync();

        var controller = new AdminReportController(db);
        var result = await controller.GetRevenueBiReport(year: 2026, quarter: 0, month: 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<RevenueBiReportDto>(okResult.Value);
        Assert.Equal(14_500_000m, report.Kpi.TotalRevenue);

        // Sự kiện "đã tổ chức" chỉ tính tiệc hoàn tất: tiệc đã xác nhận ở trên không được cộng vào.
        Assert.Equal(1, report.Kpi.EventsHeld);
    }

    private static async Task<SeedIds> SeedReportData(ApplicationDbContext db)
    {
        db.DataStatuses.Add(new DataStatus
        {
            DataStatusId = 1,
            DataStatusCode = DataStatusCodes.Existing,
            DataStatusName = "Tồn tại"
        });

        var confirmedBooking = new Status { StatusId = 201, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" };
        var cancelledBooking = new Status { StatusId = 202, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" };
        var completedBooking = new Status { StatusId = 203, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Completed, StatusName = "Hoàn tất" };
        var completedContract = new Status { StatusId = 902, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Completed, StatusName = "Hoàn tất" };
        var successPayment = new Status { StatusId = 1002, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" };
        var availableSchedule = new Status { StatusId = 301, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" };
        var bookedSchedule = new Status { StatusId = 302, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" };
        var lockedSchedule = new Status { StatusId = 303, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Locked, StatusName = "Tạm khóa" };
        var qrStatus = new Status { StatusId = 1401, StatusGroup = StatusGroups.QrReview, StatusCode = "ACTIVE", StatusName = "Hiệu lực" };

        db.Statuses.AddRange(
            confirmedBooking, cancelledBooking, completedBooking, completedContract, successPayment,
            availableSchedule, bookedSchedule, lockedSchedule, qrStatus);

        db.Halls.AddRange(
            new Hall { HallId = 1, HallCode = "H01", HallName = "Sảnh Thác Nước", MaximumCapacity = 40, DataStatusId = 1 },
            new Hall { HallId = 2, HallCode = "H02", HallName = "Sảnh Ánh Dương", MaximumCapacity = 40, DataStatusId = 1 });

        // Sảnh 1 tối 15/10: tiệc đã hoàn tất; trưa cùng ngày: tiệc hủy.
        // Sảnh 2 trưa 16/10: ca bị khóa theo lịch (không phải công suất khả dụng).
        db.HallSchedules.AddRange(
            new HallSchedule { HallScheduleId = 10, HallId = 1, Date = new DateTime(2026, 10, 15), Shift = HallShiftNames.Dinner, StatusId = 302, DataStatusId = 1 },
            new HallSchedule { HallScheduleId = 11, HallId = 1, Date = new DateTime(2026, 10, 15), Shift = HallShiftNames.Lunch, StatusId = 302, DataStatusId = 1 },
            new HallSchedule { HallScheduleId = 12, HallId = 2, Date = new DateTime(2026, 10, 16), Shift = HallShiftNames.Lunch, StatusId = 303, DataStatusId = 1 });

        db.Customers.Add(new Customer { CustomerId = 1, FullName = "Nguyễn Văn A", Email = "a@test.com", PhoneNumber = "0901234567" });

        db.WeddingBookings.AddRange(
            new WeddingBooking
            {
                BookingId = 1,
                BookingCode = "DT-20261015-001",
                CustomerId = 1,
                HallScheduleId = 10,
                BookedAt = new DateTime(2026, 10, 1),
                GuestCount = 200,
                FinalMenuPrice = 5_000_000m,
                FinalHallPrice = 30_000_000m,
                FinalDecorationPrice = 15_000_000m,
                EstimatedTotal = 145_000_000m,
                StatusId = 203
            },
            new WeddingBooking
            {
                BookingId = 2,
                BookingCode = "DT-20261015-002",
                CustomerId = 1,
                HallScheduleId = 10,
                BookedAt = new DateTime(2026, 10, 2),
                GuestCount = 100,
                EstimatedTotal = 80_000_000m,
                StatusId = 202
            });

        db.Contracts.Add(new Contract
        {
            ContractId = 1,
            BookingId = 1,
            ContractCode = "HD-001",
            CreatedDate = new DateTime(2026, 10, 5),
            TotalValue = 145_000_000m,
            StatusId = 902
        });

        db.Payments.Add(new Payment
        {
            PaymentId = 1,
            ContractId = 1,
            PaymentType = "Đặt cọc",
            Amount = 14_500_000m,
            PaymentDate = new DateTime(2026, 10, 5, 10, 0, 0),
            StatusId = 1002
        });

        var qr = new ReviewQrCode
        {
            ReviewQrCodeId = 1,
            BookingId = 1,
            QrCode = "QR-001",
            CreatedAt = new DateTime(2026, 10, 16),
            ExpiresAt = new DateTime(2026, 10, 30),
            StatusId = 1401
        };
        db.ReviewQrCodes.Add(qr);
        db.WeddingReviews.AddRange(
            new WeddingReview
            {
                ReviewId = 1,
                ReviewQrCodeId = 1,
                ReviewerType = "Chủ tiệc",
                HallScore = 4,
                FoodScore = 5,
                ServiceScore = 4,
                SoundScore = 4,
                LightingScore = 5,
                OverallScore = 4,
                Comment = "Rất tốt",
                ReviewedAt = new DateTime(2026, 10, 16)
            },
            new WeddingReview
            {
                ReviewId = 2,
                ReviewQrCodeId = 1,
                ReviewerType = "Khách mời",
                HallScore = 5,
                FoodScore = 4,
                ServiceScore = 5,
                SoundScore = 4,
                LightingScore = 4,
                OverallScore = 5,
                Comment = "Hài lòng",
                ReviewedAt = new DateTime(2026, 10, 16)
            });

        await db.SaveChangesAsync();

        return new SeedIds(HallId: 1);
    }

    private sealed record SeedIds(int HallId);
}

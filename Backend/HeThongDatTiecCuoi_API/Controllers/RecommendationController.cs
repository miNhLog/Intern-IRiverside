using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Recommendation;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/recommendations")]
[ApiController]
public sealed class RecommendationController : ControllerBase
{
    private const double BudgetWeight = 35d / 85d;
    private const double SpaceWeight = 25d / 85d;
    private const double StyleWeight = 25d / 85d;

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public RecommendationController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpPost("suggest-top3")]
    public async Task<IActionResult> SuggestTop3Combos([FromBody] RecommendationRequestDto request)
    {
        var desiredShift = request.DesiredShift.Trim();
        if (desiredShift != HallShiftNames.Lunch && desiredShift != HallShiftNames.Dinner)
            return BadRequest(new { message = "Ca tổ chức chỉ có thể là Ca trưa hoặc Ca tối." });
        if (string.IsNullOrWhiteSpace(request.DesiredStyle))
            return BadRequest(new { message = "Phong cách mong muốn không được để trống." });

        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var customerId = await ResolveAuthenticatedCustomerIdAsync(existingDataStatusId);
        var requestEntity = new RecommendationRequestEntity
        {
            CustomerId = customerId,
            ExpectedBudget = request.ExpectedBudget,
            GuestCount = request.GuestCount,
            DesiredDate = request.DesiredDate.Date,
            DesiredShift = desiredShift,
            DesiredStyle = request.DesiredStyle.Trim(),
            ServiceNeeds = NormalizeOptional(request.ServiceNeeds),
            CreatedAt = DateTime.Now
        };
        _context.RecommendationRequests.Add(requestEntity);
        await _context.SaveChangesAsync();

        var targetDate = request.DesiredDate.Date;
        var candidateSchedules = await _context.HallSchedules
            .AsNoTracking()
            .Where(schedule =>
                schedule.Date == targetDate &&
                schedule.Shift == desiredShift &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available &&
                schedule.Hall.DataStatusId == existingDataStatusId &&
                schedule.Hall.Status.StatusGroup == StatusGroups.Hall &&
                schedule.Hall.Status.StatusCode == HallStatusCodes.Active &&
                request.GuestCount <= schedule.Hall.MaximumCapacity &&
                (!schedule.Hall.MinimumCapacity.HasValue || request.GuestCount >= schedule.Hall.MinimumCapacity.Value))
            .Select(schedule => new
            {
                schedule.HallScheduleId,
                Hall = schedule.Hall
            })
            .ToListAsync();

        if (candidateSchedules.Count == 0)
        {
            return Ok(new RecommendationResponseDto
            {
                RecommendationRequestId = requestEntity.RecommendationRequestId,
                TotalCandidatesEvaluated = 0,
                AlgorithmNotice = "Không có lịch sảnh AVAILABLE phù hợp ngày, ca và số lượng khách đã chọn.",
                AnalyzedAt = DateTime.Now
            });
        }

        var activeMenus = await _context.Menus.AsNoTracking()
            .Where(menu =>
                menu.MenuType == MenuTypes.Standard &&
                menu.DataStatusId == existingDataStatusId &&
                menu.Status.StatusGroup == StatusGroups.Menu &&
                menu.Status.StatusCode == MenuStatusCodes.Active)
            .ToListAsync();

        var activeDecors = await _context.DecorPackages.AsNoTracking()
            .Where(decor =>
                decor.DataStatusId == existingDataStatusId &&
                decor.Status.StatusGroup == StatusGroups.DecorationPackage &&
                decor.Status.StatusCode == BusinessStatusCodes.Active)
            .Select(decor => new
            {
                Decor = decor,
                ImageUrl = decor.Images
                    .Where(image => image.IsPrimary && image.DataStatusId == existingDataStatusId)
                    .Select(image => image.ImagePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var estimatedTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var scoredCombos = new List<ComboSuggestionDto>();

        foreach (var schedule in candidateSchedules)
        foreach (var menu in activeMenus)
        foreach (var decorCandidate in activeDecors)
        {
            var hall = schedule.Hall;
            var decor = decorCandidate.Decor;
            var menuTotal = menu.PricePerTable * estimatedTableCount;
            var totalCost = hall.RentalPrice + menuTotal + decor.Gia;
            var budgetDifference = request.ExpectedBudget - totalCost;
            var budgetScore = CalculateBudgetScore(totalCost, request.ExpectedBudget);
            var spaceScore = CalculateSpaceScore(request.GuestCount, hall.MaximumCapacity);
            var styleScore = CalculateStyleScore(request.DesiredStyle, decor.PhongCach);
            var overallScore = Math.Round(
                budgetScore * BudgetWeight + spaceScore * SpaceWeight + styleScore * StyleWeight,
                1);

            var reasons = new List<string>
            {
                $"Sảnh {hall.HallName} còn trống và phù hợp {request.GuestCount} khách.",
                budgetDifference >= 0
                    ? $"Phương án thấp hơn ngân sách dự kiến {budgetDifference:N0} VNĐ."
                    : $"Phương án vượt ngân sách dự kiến {Math.Abs(budgetDifference):N0} VNĐ."
            };
            if (styleScore > 70)
                reasons.Add($"Phong cách {decor.PhongCach} phù hợp với mong muốn {request.DesiredStyle}.");

            scoredCombos.Add(new ComboSuggestionDto
            {
                OverallMatchScore = overallScore,
                BudgetMatchScore = Math.Round(budgetScore, 1),
                SpaceFitScore = Math.Round(spaceScore, 1),
                StyleThemeScore = Math.Round(styleScore, 1),
                HallScheduleId = schedule.HallScheduleId,
                HallId = hall.HallId,
                HallName = hall.HallName,
                HallCode = hall.HallCode,
                HallCapacity = hall.MaximumCapacity,
                HallRentalPrice = hall.RentalPrice,
                HallDescription = hall.Description ?? string.Empty,
                MenuId = menu.MenuId,
                MenuName = menu.MenuName,
                MenuPricePerTable = menu.PricePerTable,
                EstimatedTableCount = estimatedTableCount,
                MenuTotalPrice = menuTotal,
                MenuDescription = menu.Description ?? string.Empty,
                DecorationPackageId = decor.GoiTrangTriID,
                DecorationPackageName = decor.TenGoi,
                DecorationStyle = decor.PhongCach,
                DecorationPrice = decor.Gia,
                DecorationDescription = decor.MoTa ?? string.Empty,
                DecorationImageUrl = decorCandidate.ImageUrl,
                TotalEstimatedCost = totalCost,
                BudgetDifference = budgetDifference,
                BudgetAnalysisText = budgetDifference >= 0 ? "Trong ngân sách" : "Vượt ngân sách",
                RecommendationReasons = reasons
            });
        }

        var topCombos = scoredCombos
            .OrderByDescending(combo => combo.OverallMatchScore)
            .Take(3)
            .ToList();
        for (var index = 0; index < topCombos.Count; index++)
            topCombos[index].Rank = index + 1;

        return Ok(new RecommendationResponseDto
        {
            RecommendationRequestId = requestEntity.RecommendationRequestId,
            TotalCandidatesEvaluated = scoredCombos.Count,
            TopCombos = topCombos,
            AlgorithmNotice = $"Đã đánh giá {scoredCombos.Count} phương án bằng scoring C# và trả về tối đa 3 kết quả.",
            AnalyzedAt = DateTime.Now
        });
    }

    [HttpGet("hall-availability")]
    public async Task<IActionResult> GetHallAvailability([FromQuery] DateTime date)
    {
        var existingDataStatusId = await GetDataStatusIdAsync(DataStatusCodes.Existing);
        var targetDate = date.Date;
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.DataStatusId == existingDataStatusId &&
                hall.Status.StatusGroup == StatusGroups.Hall && hall.Status.StatusCode == HallStatusCodes.Active)
            .OrderBy(hall => hall.HallId)
            .Select(hall => new
            {
                hall.HallId,
                hall.HallName,
                hall.HallCode,
                Capacity = hall.MaximumCapacity,
                hall.RentalPrice
            }).ToListAsync();

        var availableSchedules = await _context.HallSchedules.AsNoTracking()
            .Where(schedule => schedule.Date == targetDate &&
                schedule.DataStatusId == existingDataStatusId &&
                schedule.Status.StatusGroup == StatusGroups.HallSchedule &&
                schedule.Status.StatusCode == HallScheduleStatusCodes.Available &&
                schedule.Hall.DataStatusId == existingDataStatusId &&
                schedule.Hall.Status.StatusGroup == StatusGroups.Hall &&
                schedule.Hall.Status.StatusCode == HallStatusCodes.Active)
            .Select(schedule => new { schedule.HallId, schedule.Shift })
            .ToListAsync();

        return Ok(halls.Select(hall => new
        {
            hallId = hall.HallId,
            hallName = hall.HallName,
            hallCode = hall.HallCode,
            capacity = hall.Capacity,
            rentalPrice = hall.RentalPrice,
            isNoonAvailable = availableSchedules.Any(item => item.HallId == hall.HallId && item.Shift == HallShiftNames.Lunch),
            isEveningAvailable = availableSchedules.Any(item => item.HallId == hall.HallId && item.Shift == HallShiftNames.Dinner)
        }));
    }

    [HttpPost("register-booking")]
    public async Task<IActionResult> RegisterPublicBooking([FromBody] RegisterPublicBookingDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { success = false, message = "Vui lòng cung cấp họ tên và số điện thoại liên hệ." });
        if (request.GuestCount <= 0)
            return BadRequest(new { success = false, message = "Số lượng khách phải lớn hơn 0." });

        var existingDataStatus = await _context.DataStatuses.SingleAsync(item => item.DataStatusCode == DataStatusCodes.Existing);
        var cleanPhone = request.PhoneNumber.Trim();
        var customer = await _context.Customers.FirstOrDefaultAsync(customer =>
            customer.DataStatusId == existingDataStatus.DataStatusId &&
            (customer.PhoneNumber == cleanPhone || (request.UserId.HasValue && customer.UserId == request.UserId.Value)));
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = await GenerateCustomerCodeAsync(),
                FullName = request.CustomerName.Trim(),
                PhoneNumber = cleanPhone,
                Email = NormalizeOptional(request.Email),
                UserId = request.UserId,
                CreatedAt = DateTime.Now,
                DataStatusId = existingDataStatus.DataStatusId
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }
        else if (request.UserId.HasValue && !customer.UserId.HasValue)
        {
            customer.UserId = request.UserId.Value;
            await _context.SaveChangesAsync();
        }

        var hasActiveBooking = await _context.WeddingBookings.AnyAsync(booking =>
            booking.CustomerId == customer.CustomerId &&
            BookingStatusCodes.OccupyingSchedule.Contains(booking.Status.StatusCode));
        if (hasActiveBooking)
            return BadRequest(new { success = false, message = "Khách hàng đã có một booking đang hoạt động." });

        var schedule = await _context.HallSchedules
            .Include(item => item.Hall)
            .Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallScheduleId == request.HallScheduleId &&
                item.DataStatusId == existingDataStatus.DataStatusId &&
                item.Hall.DataStatusId == existingDataStatus.DataStatusId);
        if (schedule == null || schedule.Status.StatusGroup != StatusGroups.HallSchedule ||
            schedule.Status.StatusCode != HallScheduleStatusCodes.Available)
            return Conflict(new { success = false, message = "Lịch sảnh không còn AVAILABLE." });
        if (request.GuestCount > schedule.Hall.MaximumCapacity ||
            (schedule.Hall.MinimumCapacity.HasValue && request.GuestCount < schedule.Hall.MinimumCapacity.Value))
            return BadRequest(new { success = false, message = "Số khách không phù hợp sức chứa sảnh." });

        Menu? menu = null;
        if (request.MenuId.HasValue)
        {
            menu = await _context.Menus.SingleOrDefaultAsync(item =>
                item.MenuId == request.MenuId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (menu == null) return BadRequest(new { success = false, message = "Thực đơn không tồn tại." });
        }

        DecorPackage? decor = null;
        if (request.DecorationPackageId.HasValue)
        {
            decor = await _context.DecorPackages.SingleOrDefaultAsync(item =>
                item.GoiTrangTriID == request.DecorationPackageId && item.DataStatusId == existingDataStatus.DataStatusId);
            if (decor == null) return BadRequest(new { success = false, message = "Gói trang trí không tồn tại." });
        }

        var estimatedTableCount = (int)Math.Ceiling(request.GuestCount / 10m);
        var booking = new WeddingBooking
        {
            BookingCode = await GenerateBookingCodeAsync(schedule.Date),
            CustomerId = customer.CustomerId,
            HallScheduleId = schedule.HallScheduleId,
            MenuId = menu?.MenuId,
            DecorationPackageId = decor?.GoiTrangTriID,
            GuestCount = request.GuestCount,
            FinalHallPrice = schedule.Hall.RentalPrice,
            FinalMenuPrice = menu?.PricePerTable,
            FinalDecorationPrice = decor?.Gia,
            EstimatedTotal = schedule.Hall.RentalPrice + (menu?.PricePerTable ?? 0m) * estimatedTableCount + (decor?.Gia ?? 0m),
            SpecialRequests = NormalizeOptional(request.SpecialRequests),
            StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Pending),
            BookedAt = DateTime.Now
        };
        schedule.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, bookingCode = booking.BookingCode, bookingId = booking.BookingId });
    }

    private async Task<int?> ResolveAuthenticatedCustomerIdAsync(byte existingDataStatusId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawUserId, out var userId)) return null;
        return await _context.Customers.AsNoTracking()
            .Where(customer => customer.UserId == userId && customer.DataStatusId == existingDataStatusId)
            .Select(customer => (int?)customer.CustomerId)
            .SingleOrDefaultAsync();
    }

    private static double CalculateBudgetScore(decimal totalCost, decimal expectedBudget)
    {
        var ratio = (double)(totalCost / (expectedBudget > 0 ? expectedBudget : 1));
        if (ratio is >= 0.80 and <= 1.00) return 100 - (1.0 - ratio) * 30;
        if (ratio is >= 0.65 and < 0.80) return 85 + (ratio - 0.65) / 0.15 * 10;
        if (ratio is > 1.00 and <= 1.10) return 80 - (ratio - 1.0) / 0.10 * 15;
        return ratio < 0.65 ? 70 : 40;
    }

    private static double CalculateSpaceScore(int guestCount, int maximumCapacity)
    {
        var ratio = (double)guestCount / maximumCapacity;
        if (ratio is >= 0.65 and <= 0.88) return 100;
        if (ratio is > 0.88 and <= 1.00) return 85;
        if (ratio is >= 0.45 and < 0.65) return 80;
        return 55;
    }

    private static double CalculateStyleScore(string desiredStyle, string decorStyle) =>
        decorStyle.Contains(desiredStyle, StringComparison.OrdinalIgnoreCase) ||
        desiredStyle.Contains(decorStyle, StringComparison.OrdinalIgnoreCase)
            ? 85
            : 70;

    private async Task<byte> GetDataStatusIdAsync(string code) => await _context.DataStatuses
        .Where(item => item.DataStatusCode == code).Select(item => item.DataStatusId).SingleAsync();

    private async Task<string> GenerateBookingCodeAsync(DateTime eventDate)
    {
        string code;
        do code = $"DT-{eventDate:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.WeddingBookings.AnyAsync(item => item.BookingCode == code));
        return code;
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        string code;
        do code = $"KH-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        while (await _context.Customers.AnyAsync(item => item.CustomerCode == code));
        return code;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

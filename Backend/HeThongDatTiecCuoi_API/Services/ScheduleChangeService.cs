using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;
using PaymentTransactionEntity = HeThongDatTiecCuoi_API.Models.PaymentTransaction;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class ScheduleChangeException : Exception
{
    public ScheduleChangeException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public interface IScheduleChangeService
{
    Task<HallManagerScheduleChangeOptionsDto> GetHallManagerCreateOptionsAsync(int userId, CancellationToken cancellationToken);
    Task<List<ScheduleChangeAvailabilityDto>> GetHallManagerAvailabilityAsync(int userId, int bookingId, DateTime date, string shift, CancellationToken cancellationToken);
    Task<ScheduleChangeEligibilityDto> GetEligibilityAsync(ScheduleChangeLookupRequest request, CancellationToken cancellationToken);
    Task<List<ScheduleChangeAvailabilityDto>> GetAvailabilityAsync(ScheduleChangeAvailabilityRequest request, CancellationToken cancellationToken);
    Task SendOtpAsync(SendScheduleChangeOtpRequest request, string clientIp, CancellationToken cancellationToken);
    Task<OtpVerificationDto> VerifyOtpAsync(VerifyScheduleChangeOtpRequest request, string clientIp, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> CreateCustomerRequestAsync(CreateScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> CreateHallManagerRequestAsync(int userId, HallManagerCreateScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<List<ScheduleChangeDto>> GetPublicRequestsAsync(ScheduleChangeLookupRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> GetPublicRequestAsync(int id, ScheduleChangeLookupRequest request, CancellationToken cancellationToken);
    Task<List<ScheduleChangeDto>> GetManagerRequestsAsync(string? status, CancellationToken cancellationToken);
    Task<List<ScheduleChangeDto>> GetHallManagerRequestsAsync(int userId, string? status, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> GetInternalRequestAsync(int id, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> ReviewAsync(int userId, int id, ReviewScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> QuoteAndConditionallyApproveAsync(int userId, int id, QuoteScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> RejectAsync(int userId, int id, RejectScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<QuoteAcceptanceResultDto> AcceptQuoteAsync(int id, AcceptScheduleChangeQuoteRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> WithdrawAsync(int id, WithdrawScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangePaymentDto> GetPaymentStatusAsync(int id, ScheduleChangeLookupRequest request, CancellationToken cancellationToken);
    Task<bool> ProcessVerifiedPaymentAsync(long orderCode, long paidAmount, string responseCode, string payload, CancellationToken cancellationToken);
    Task<ScheduleChangeDto> ReconcileAsync(int userId, int id, ReconciliationScheduleChangeRequest request, CancellationToken cancellationToken);
    Task<ScheduleChangeFinancialDetailDto> GetFinancialDetailAsync(int id, CancellationToken cancellationToken);
    Task<ScheduleChangeRefundDto> ConfirmManualRefundAsync(int userId, int id, int refundId, ManualRefundConfirmationRequest request, CancellationToken cancellationToken);
    Task ExpireDueHoldsAsync(CancellationToken cancellationToken);
}

public sealed class ScheduleChangeService : IScheduleChangeService
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statuses;
    private readonly IScheduleChangePolicyService _policies;
    private readonly IEmailService _email;
    private readonly IPasswordHasher<ScheduleChangeOtp> _otpHasher;
    private readonly IOptions<SmsOptions> _smsOptions;
    private readonly IOptions<AppPayOSOptions> _payOsOptions;
    private readonly IHostEnvironment _environment;
    private readonly PayOSClient _payOs;
    private readonly INotificationService _notifications;
    private readonly ILogger<ScheduleChangeService> _logger;

    public ScheduleChangeService(ApplicationDbContext context, IStatusService statuses,
        IScheduleChangePolicyService policies, IEmailService email, IPasswordHasher<ScheduleChangeOtp> otpHasher,
        IOptions<SmsOptions> smsOptions, IOptions<AppPayOSOptions> payOsOptions, IHostEnvironment environment,
        PayOSClient payOs, INotificationService notifications, ILogger<ScheduleChangeService> logger)
    {
        _context = context; _statuses = statuses; _policies = policies; _email = email; _otpHasher = otpHasher;
        _smsOptions = smsOptions; _payOsOptions = payOsOptions; _environment = environment; _payOs = payOs;
        _notifications = notifications; _logger = logger;
    }

    public async Task<ScheduleChangeEligibilityDto> GetEligibilityAsync(ScheduleChangeLookupRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        var policy = await _policies.GetActiveAsync(RiversideBusinessClock.Now, cancellationToken);
        var days = ScheduleChangeBusinessRules.DaysBefore(booking.HallSchedule.Date, RiversideBusinessClock.Today);
        var tier = ScheduleChangeBusinessRules.ResolveTier(policy, Math.Max(0, days));
        var eligibleStatus = ScheduleChangeBusinessRules.IsBookingEligible(booking.Status.StatusCode);
        var hasOpen = await HasOpenRequestAsync(booking.BookingId, null, cancellationToken);
        var can = eligibleStatus && days >= policy.OnlineMinimumDays && tier.CustomerOnlineAllowed && !hasOpen;
        var reason = !eligibleStatus ? "Trạng thái booking không cho phép đổi lịch."
            : days < policy.OnlineMinimumDays || !tier.CustomerOnlineAllowed ? $"Khách chỉ được gửi trực tuyến trước tiệc ít nhất {policy.OnlineMinimumDays} ngày."
            : hasOpen ? "Booking đang có một yêu cầu đổi lịch chưa kết thúc." : null;
        return new ScheduleChangeEligibilityDto
        {
            BookingId = booking.BookingId, BookingCode = booking.BookingCode, BookingStatus = booking.Status.StatusCode,
            CurrentDate = booking.HallSchedule.Date, CurrentShift = booking.HallSchedule.Shift,
            CurrentHallId = booking.HallSchedule.HallId, CurrentHallName = booking.HallSchedule.Hall.HallName,
            DaysBeforeEvent = days, CanRequestOnline = can, Reason = reason,
            MinimumNewDate = RiversideBusinessClock.Today.AddDays(policy.NewDateMinimumDays),
            ScheduleChangeFee = tier.FeeAmount, PolicyVersion = policy.Version
        };
    }

    public async Task<List<ScheduleChangeAvailabilityDto>> GetAvailabilityAsync(ScheduleChangeAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        return await GetAvailabilityForBookingAsync(booking, request.Date, request.Shift, cancellationToken);
    }

    public async Task<HallManagerScheduleChangeOptionsDto> GetHallManagerCreateOptionsAsync(int userId, CancellationToken cancellationToken)
    {
        var today = RiversideBusinessClock.Today;
        var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, today);
        var bookings = await _context.WeddingBookings.AsNoTracking()
            .Where(b => hallIds.Contains(b.HallSchedule.HallId) && b.HallSchedule.Date > today &&
                (b.Status.StatusCode == BookingStatusCodes.Confirmed || b.Status.StatusCode == BookingStatusCodes.Preparing) &&
                !_context.WeddingScheduleChanges.Any(c => c.BookingId == b.BookingId && ScheduleChangeStatusCodes.Open.Contains(c.Status.StatusCode)))
            .OrderBy(b => b.HallSchedule.Date).ThenBy(b => b.BookingCode)
            .Select(b => new HallManagerScheduleChangeBookingDto(b.BookingId, b.BookingCode, b.Customer.FullName,
                b.HallSchedule.Date, b.HallSchedule.Shift, b.HallSchedule.Hall.HallName, b.GuestCount)).ToListAsync(cancellationToken);
        var policy = await _policies.GetActiveAsync(RiversideBusinessClock.Now, cancellationToken);
        return new(today.AddDays(policy.NewDateMinimumDays), policy.OnlineMinimumDays, today, bookings);
    }

    public async Task<List<ScheduleChangeAvailabilityDto>> GetHallManagerAvailabilityAsync(int userId, int bookingId,
        DateTime date, string shift, CancellationToken cancellationToken)
    {
        if (!await HallManagerOwnsBookingAsync(userId, bookingId, cancellationToken))
            throw new ScheduleChangeException("Không tìm thấy tiệc trong phạm vi sảnh phụ trách.", StatusCodes.Status404NotFound);
        var booking = await BookingQuery(false).SingleAsync(b => b.BookingId == bookingId, cancellationToken);
        if (!ScheduleChangeBusinessRules.IsBookingEligible(booking.Status.StatusCode) || booking.HallSchedule.Date.Date <= RiversideBusinessClock.Today ||
            await HasOpenRequestAsync(bookingId, null, cancellationToken))
            throw new ScheduleChangeException("Tiệc không còn đủ điều kiện tạo yêu cầu đổi lịch. Vui lòng tải lại danh sách.", StatusCodes.Status409Conflict);
        return await GetAvailabilityForBookingAsync(booking, date, shift, cancellationToken);
    }

    private async Task<List<ScheduleChangeAvailabilityDto>> GetAvailabilityForBookingAsync(WeddingBooking booking,
        DateTime requestedDate, string requestedShift, CancellationToken cancellationToken)
    {
        var policy = await _policies.GetActiveAsync(RiversideBusinessClock.Now, cancellationToken);
        var date = requestedDate.Date;
        var shift = NormalizeShift(requestedShift);
        if (date < RiversideBusinessClock.Today.AddDays(policy.NewDateMinimumDays))
            throw new ScheduleChangeException($"Ngày mới phải cách hôm nay ít nhất {policy.NewDateMinimumDays} ngày.");

        var existingId = await ExistingDataStatusIdAsync(cancellationToken);
        var halls = await _context.Halls.AsNoTracking().Where(x => x.DataStatusId == existingId &&
                x.Status.StatusCode == BusinessStatusCodes.Active)
            .OrderBy(x => x.HallName).ToListAsync(cancellationToken);
        var schedules = await _context.HallSchedules.AsNoTracking().Include(x => x.Status)
            .Where(x => x.Date == date && x.Shift == shift && x.DataStatusId == existingId)
            .ToListAsync(cancellationToken);
        var scheduleIds = schedules.Select(x => x.HallScheduleId).ToList();
        var occupied = await _context.WeddingBookings.AsNoTracking().Where(x => scheduleIds.Contains(x.HallScheduleId) &&
                x.BookingId != booking.BookingId && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode))
            .Select(x => x.HallScheduleId).ToListAsync(cancellationToken);
        var held = await _context.ScheduleChangeHolds.AsNoTracking().Where(x => scheduleIds.Contains(x.HallScheduleId) &&
                x.Status == ScheduleChangeHoldStatusCodes.Active && x.ExpiresAt > RiversideBusinessClock.Now)
            .Select(x => x.HallScheduleId).ToListAsync(cancellationToken);

        return halls.Select(hall =>
        {
            var schedule = schedules.SingleOrDefault(x => x.HallId == hall.HallId);
            string? unavailable = CapacityUnavailableReason(booking, hall);
            if (unavailable is null)
            {
                if (schedule?.Status.StatusCode == HallScheduleStatusCodes.Locked) unavailable = "Lịch sảnh đang bị khóa.";
                else if (schedule is not null && occupied.Contains(schedule.HallScheduleId)) unavailable = "Lịch đã có booking khác.";
                else if (schedule is not null && held.Contains(schedule.HallScheduleId)) unavailable = "Lịch đang được một yêu cầu khác giữ tạm.";
                else if (schedule?.HallScheduleId == booking.HallScheduleId) unavailable = "Lịch mới trùng hoàn toàn lịch hiện tại.";
            }
            return new ScheduleChangeAvailabilityDto
            {
                HallId = hall.HallId, HallName = hall.HallName, Date = date, Shift = shift,
                HallPrice = hall.RentalPrice, MinimumCapacity = hall.MinimumCapacity, MaximumCapacity = hall.MaximumCapacity,
                Available = unavailable is null, UnavailableReason = unavailable
            };
        }).ToList();
    }

    public async Task SendOtpAsync(SendScheduleChangeOtpRequest request, string clientIp, CancellationToken cancellationToken)
    {
        ValidateOtpPurpose(request.Purpose, request.ScheduleChangeId);
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        if (request.Purpose == ScheduleChangeOtpPurposes.CreateRequest)
        {
            var eligibility = await GetEligibilityAsync(request, cancellationToken);
            if (!eligibility.CanRequestOnline) throw new ScheduleChangeException(eligibility.Reason ?? "Không đủ điều kiện đổi lịch.", StatusCodes.Status409Conflict);
        }
        else
        {
            await RequireOwnedChangeAsync(request.ScheduleChangeId!.Value, booking.BookingId, cancellationToken);
            var change = await _context.WeddingScheduleChanges.AsNoTracking().Include(x => x.Status)
                .SingleAsync(x => x.ScheduleChangeId == request.ScheduleChangeId.Value, cancellationToken);
            if (change.Status.StatusCode != ScheduleChangeStatusCodes.AwaitingQuoteAcceptance)
                throw new ScheduleChangeException("Yêu cầu chưa ở bước xác nhận báo giá.", StatusCodes.Status409Conflict);
        }

        var now = RiversideBusinessClock.Now;
        if (string.IsNullOrWhiteSpace(booking.Customer.Email))
            throw new ScheduleChangeException("Booking chưa có email nhận OTP. Vui lòng liên hệ Riverside Palace để cập nhật email.", StatusCodes.Status409Conflict);
        var contactHash = Hash(booking.Customer.Email.Trim().ToLowerInvariant());
        var ipHash = Hash(clientIp);
        var last = await _context.ScheduleChangeOtps.AsNoTracking()
            .Where(x => x.BookingId == booking.BookingId && x.Purpose == request.Purpose)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (!ScheduleChangeBusinessRules.CanResendOtp(last?.CreatedAt, now))
            throw new ScheduleChangeException("Vui lòng chờ đủ 60 giây trước khi gửi lại OTP.", StatusCodes.Status429TooManyRequests);
        if (await _context.ScheduleChangeOtps.CountAsync(x =>
                (x.BookingId == booking.BookingId || x.PhoneHash == contactHash || x.IpHash == ipHash) &&
                x.CreatedAt > now.AddHours(-1), cancellationToken) >= 5)
            throw new ScheduleChangeException("Đã vượt giới hạn gửi OTP. Vui lòng thử lại sau.", StatusCodes.Status429TooManyRequests);

        var configuredDevelopmentCode = _smsOptions.Value.DevelopmentCode?.Trim();
        var useDevelopmentCode = _environment.IsDevelopment() &&
                                 string.Equals(_smsOptions.Value.Provider, "Development", StringComparison.OrdinalIgnoreCase) &&
                                 configuredDevelopmentCode is { Length: 6 } && configuredDevelopmentCode.All(char.IsDigit);
        var code = useDevelopmentCode
            ? configuredDevelopmentCode!
            : RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        try
        {
            await _email.SendScheduleChangeOtpEmailAsync(booking.Customer.Email, booking.Customer.FullName,
                booking.BookingCode, code, 5, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi OTP đổi lịch qua email thất bại cho BookingId={BookingId}", booking.BookingId);
            throw new ScheduleChangeException("Dịch vụ email chưa được cấu hình hoặc gửi OTP thất bại.", StatusCodes.Status503ServiceUnavailable);
        }

        var otp = new ScheduleChangeOtp
        {
            BookingId = booking.BookingId, ScheduleChangeId = request.ScheduleChangeId,
            Purpose = request.Purpose, PhoneHash = contactHash, IpHash = ipHash,
            CreatedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        otp.CodeHash = _otpHasher.HashPassword(otp, code);
        _context.ScheduleChangeOtps.Add(otp);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OtpVerificationDto> VerifyOtpAsync(VerifyScheduleChangeOtpRequest request, string clientIp,
        CancellationToken cancellationToken)
    {
        ValidateOtpPurpose(request.Purpose, request.ScheduleChangeId);
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        var now = RiversideBusinessClock.Now;
        var otp = await _context.ScheduleChangeOtps.Where(x => x.BookingId == booking.BookingId &&
                x.ScheduleChangeId == request.ScheduleChangeId && x.Purpose == request.Purpose &&
                x.VerifiedAt == null && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ScheduleChangeException("Không tìm thấy OTP hợp lệ.", StatusCodes.Status404NotFound);
        if (otp.ExpiresAt <= now) throw new ScheduleChangeException("OTP đã hết hạn.", StatusCodes.Status410Gone);
        if (!ScheduleChangeBusinessRules.CanAttemptOtp(otp, now))
            throw new ScheduleChangeException("OTP đã bị khóa sau 5 lần nhập sai hoặc đã được sử dụng.", StatusCodes.Status429TooManyRequests);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(otp.IpHash), Convert.FromHexString(Hash(clientIp))))
            throw new ScheduleChangeException("Phiên xác thực OTP không hợp lệ.", StatusCodes.Status403Forbidden);
        var verification = _otpHasher.VerifyHashedPassword(otp, otp.CodeHash, request.Code);
        if (verification == PasswordVerificationResult.Failed)
        {
            otp.FailedAttempts++;
            await _context.SaveChangesAsync(cancellationToken);
            throw new ScheduleChangeException("OTP không chính xác.", StatusCodes.Status400BadRequest);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        otp.VerificationTokenHash = Hash(token);
        otp.VerifiedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return new OtpVerificationDto { VerificationToken = token, ExpiresAt = otp.ExpiresAt };
    }

    public async Task<ScheduleChangeDto> CreateCustomerRequestAsync(CreateScheduleChangeRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        await ConsumeOtpTokenAsync(booking.BookingId, null, ScheduleChangeOtpPurposes.CreateRequest,
            request.VerificationToken, cancellationToken);
        var result = await CreateRequestCoreAsync(booking.BookingId, request.NewHallId, request.NewDate,
            request.NewShift, request.Reason, ScheduleChangeSources.Customer, null, null, null, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return ToDto(result);
    }

    public async Task<ScheduleChangeDto> CreateHallManagerRequestAsync(int userId,
        HallManagerCreateScheduleChangeRequest request, CancellationToken cancellationToken)
    {
        if (!await HallManagerOwnsBookingAsync(userId, request.BookingId, cancellationToken))
            throw new ScheduleChangeException("Bạn không phụ trách sảnh hiện tại của booking này.", StatusCodes.Status403Forbidden);
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var result = await CreateRequestCoreAsync(request.BookingId, request.NewHallId, request.NewDate,
            request.NewShift, request.Reason, ScheduleChangeSources.HallManager, userId,
            request.ExceptionReason, request.CustomerConsentEvidence, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return ToDto(result);
    }

    public async Task<List<ScheduleChangeDto>> GetPublicRequestsAsync(ScheduleChangeLookupRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        return (await FullQuery().AsNoTracking().Where(x => x.BookingId == booking.BookingId)
            .OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<ScheduleChangeDto> GetPublicRequestAsync(int id, ScheduleChangeLookupRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        return ToDto(await FullQuery().AsNoTracking().SingleOrDefaultAsync(x => x.ScheduleChangeId == id && x.BookingId == booking.BookingId, cancellationToken)
            ?? throw new ScheduleChangeException("Không tìm thấy yêu cầu đổi lịch.", StatusCodes.Status404NotFound));
    }

    public async Task<List<ScheduleChangeDto>> GetManagerRequestsAsync(string? status, CancellationToken cancellationToken)
    {
        var query = FullQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.StatusCode == status.Trim().ToUpperInvariant());
        return (await query.OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<List<ScheduleChangeDto>> GetHallManagerRequestsAsync(int userId, string? status,
        CancellationToken cancellationToken)
    {
        var hallIds = HallManagerQueryScope.CurrentHallIds(_context, userId, RiversideBusinessClock.Today);
        var query = FullQuery().AsNoTracking().Where(x => hallIds.Contains(x.OldHallSchedule.HallId));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.StatusCode == status.Trim().ToUpperInvariant());
        return (await query.OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<ScheduleChangeDto> GetInternalRequestAsync(int id, CancellationToken cancellationToken) =>
        ToDto(await LoadChangeAsync(id, false, cancellationToken));

    public async Task<ScheduleChangeDto> ReviewAsync(int userId, int id, ReviewScheduleChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!await HallManagerOwnsChangeAsync(userId, id, cancellationToken))
            throw new ScheduleChangeException("Bạn không phụ trách sảnh hiện tại của yêu cầu.", StatusCodes.Status403Forbidden);
        var change = await LoadChangeAsync(id, true, cancellationToken);
        RequireState(change, ScheduleChangeStatusCodes.Pending);
        await EnsureTargetAvailableAsync(change, false, cancellationToken);
        change.ReviewedByUserId = userId; change.ReviewedAt = RiversideBusinessClock.Now;
        change.ReviewResult = request.ReviewResult.Trim();
        await TransitionAsync(change, ScheduleChangeStatusCodes.UnderReview, userId, "HALL_MANAGER", change.ReviewResult, cancellationToken);
        AddAudit(userId, change, "SCHEDULE_CHANGE_REVIEW", change.ReviewResult);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(change);
    }

    public async Task<ScheduleChangeDto> QuoteAndConditionallyApproveAsync(int userId, int id,
        QuoteScheduleChangeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var change = await LoadChangeAsync(id, true, cancellationToken);
        RequireState(change, ScheduleChangeStatusCodes.UnderReview);
        await EnsureTargetAvailableAsync(change, false, cancellationToken);
        if (change.Booking.Contract?.Status.StatusCode is ContractStatusCodes.Completed or ContractStatusCodes.Cancelled)
            throw new ScheduleChangeException("Hợp đồng đã hoàn tất hoặc bị hủy nên không thể đổi lịch.", StatusCodes.Status409Conflict);

        foreach (var oldQuote in change.Quotes.Where(x => x.Status == ScheduleChangeQuoteStatusCodes.PendingCustomer))
            oldQuote.Status = ScheduleChangeQuoteStatusCodes.Superseded;
        var oldPrice = change.Booking.FinalHallPrice ?? change.OldHallSchedule.Hall.RentalPrice;
        var newPrice = change.NewHallSchedule.Hall.RentalPrice;
        var adjustment = ScheduleChangeBusinessRules.CalculateAdjustment(oldPrice, newPrice, change.AppliedFee);
        var contractBefore = change.Booking.Contract?.TotalValue ?? change.Booking.EstimatedTotal ?? 0m;
        var contractAfter = Math.Max(0m, contractBefore + adjustment);
        var remainingDebt = Math.Max(0m, contractBefore - change.Booking.PaidAmount);
        var credit = adjustment < 0 ? Math.Min(-adjustment, remainingDebt) : 0m;
        var refund = adjustment < 0 ? Math.Max(0m, -adjustment - credit) : 0m;
        var quote = new ScheduleChangeQuote
        {
            ScheduleChangeId = change.ScheduleChangeId,
            Version = (change.Quotes.Count == 0 ? 0 : change.Quotes.Max(x => x.Version)) + 1,
            OldHallPrice = oldPrice, NewHallPrice = newPrice, HallPriceDifference = newPrice - oldPrice,
            ScheduleChangeFee = change.AppliedFee, AdjustmentTotal = adjustment,
            ContractValueBefore = contractBefore, ContractValueAfter = contractAfter,
            PaidAmountSnapshot = change.Booking.PaidAmount, AmountDue = Math.Max(0m, adjustment),
            CreditAmount = credit, RefundAmount = refund, Status = ScheduleChangeQuoteStatusCodes.PendingCustomer,
            CreatedByUserId = userId, CreatedAt = RiversideBusinessClock.Now
        };
        change.Quotes.Add(quote); change.ApprovedByUserId = userId;
        await TransitionAsync(change, ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, userId, "MANAGER",
            request.Note?.Trim() ?? "Manager đã duyệt có điều kiện và lập báo giá.", cancellationToken);
        AddAudit(userId, change, "SCHEDULE_CHANGE_QUOTED", JsonSerializer.Serialize(new
        { quote.Version, quote.AdjustmentTotal, quote.AmountDue, quote.RefundAmount }));
        await _context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return ToDto(change);
    }

    public async Task<ScheduleChangeDto> RejectAsync(int userId, int id, RejectScheduleChangeRequest request,
        CancellationToken cancellationToken)
    {
        var change = await LoadChangeAsync(id, true, cancellationToken);
        if (change.Status.StatusCode is not (ScheduleChangeStatusCodes.Pending or ScheduleChangeStatusCodes.UnderReview or ScheduleChangeStatusCodes.AwaitingQuoteAcceptance))
            throw new ScheduleChangeException("Trạng thái hiện tại không cho phép từ chối.", StatusCodes.Status409Conflict);
        change.ResolutionReason = request.Reason.Trim(); change.ProcessedAt = RiversideBusinessClock.Now;
        await TransitionAsync(change, ScheduleChangeStatusCodes.Rejected, userId, "MANAGER", change.ResolutionReason, cancellationToken);
        ReleaseActiveHolds(change, ScheduleChangeHoldStatusCodes.Released);
        AddAudit(userId, change, "SCHEDULE_CHANGE_REJECTED", change.ResolutionReason);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(change);
    }

    public async Task<QuoteAcceptanceResultDto> AcceptQuoteAsync(int id, AcceptScheduleChangeQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var change = await LoadChangeAsync(id, true, cancellationToken);
        if (change.BookingId != booking.BookingId) throw new ScheduleChangeException("Yêu cầu không thuộc booking này.", StatusCodes.Status403Forbidden);
        RequireState(change, ScheduleChangeStatusCodes.AwaitingQuoteAcceptance);
        var quote = change.Quotes.SingleOrDefault(x => x.QuoteId == request.QuoteId && x.Status == ScheduleChangeQuoteStatusCodes.PendingCustomer)
            ?? throw new ScheduleChangeException("Báo giá không còn hiệu lực.", StatusCodes.Status409Conflict);
        await ConsumeOtpTokenAsync(booking.BookingId, change.ScheduleChangeId, ScheduleChangeOtpPurposes.AcceptQuote,
            request.VerificationToken, cancellationToken);
        await EnsureTargetAvailableAsync(change, false, cancellationToken);
        var policy = await _context.ScheduleChangePolicies.Include(x => x.Tiers).SingleAsync(x => x.PolicyId == change.PolicyId, cancellationToken);
        if (change.NewHallSchedule.Date < RiversideBusinessClock.Today.AddDays(policy.NewDateMinimumDays))
            throw new ScheduleChangeException("Ngày mới không còn đủ thời hạn tối thiểu theo chính sách.", StatusCodes.Status409Conflict);

        var now = RiversideBusinessClock.Now;
        var hold = new ScheduleChangeHold
        {
            ScheduleChangeId = change.ScheduleChangeId, HallScheduleId = change.NewHallScheduleId,
            Status = ScheduleChangeHoldStatusCodes.Active, StartedAt = now, ExpiresAt = now.AddMinutes(policy.HoldWindowMinutes)
        };
        change.Holds.Add(hold); quote.Status = ScheduleChangeQuoteStatusCodes.Accepted; quote.AcceptedAt = now;
        change.CustomerConfirmedAt = now;

        ScheduleChangePaymentDto? payment = null;
        if (quote.AmountDue <= 0)
        {
            await FinalizeScheduleChangeAsync(change, quote, false, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
        }
        else
        {
            await TransitionAsync(change, ScheduleChangeStatusCodes.PaymentPending, null, "CUSTOMER", "Khách đã xác nhận báo giá; chờ thanh toán.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            payment = await CreatePaymentLinkAsync(change.ScheduleChangeId, quote.AmountDue,
                now.AddMinutes(policy.PaymentWindowMinutes), cancellationToken);
            change = await LoadChangeAsync(id, false, cancellationToken);
        }
        return new QuoteAcceptanceResultDto { ScheduleChange = ToDto(change), Payment = payment };
    }

    public async Task<ScheduleChangeDto> WithdrawAsync(int id, WithdrawScheduleChangeRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        var change = await LoadChangeAsync(id, true, cancellationToken);
        if (change.BookingId != booking.BookingId) throw new ScheduleChangeException("Yêu cầu không thuộc booking này.", StatusCodes.Status403Forbidden);
        if (change.Status.StatusCode is not (ScheduleChangeStatusCodes.Pending or ScheduleChangeStatusCodes.UnderReview or ScheduleChangeStatusCodes.AwaitingQuoteAcceptance))
            throw new ScheduleChangeException("Yêu cầu không còn được phép rút.", StatusCodes.Status409Conflict);
        change.ResolutionReason = request.Reason.Trim(); change.ProcessedAt = RiversideBusinessClock.Now;
        await TransitionAsync(change, ScheduleChangeStatusCodes.Cancelled, null, "CUSTOMER", change.ResolutionReason, cancellationToken);
        ReleaseActiveHolds(change, ScheduleChangeHoldStatusCodes.Released);
        AddAudit(null, change, "SCHEDULE_CHANGE_WITHDRAWN", change.ResolutionReason);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(change);
    }

    public async Task<ScheduleChangePaymentDto> GetPaymentStatusAsync(int id, ScheduleChangeLookupRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(request.BookingCode, request.PhoneNumber, cancellationToken);
        var change = await LoadChangeAsync(id, false, cancellationToken);
        if (change.BookingId != booking.BookingId) throw new ScheduleChangeException("Yêu cầu không thuộc booking này.", StatusCodes.Status403Forbidden);
        var tx = change.PaymentTransactions.OrderByDescending(x => x.NgayTao).FirstOrDefault();
        if (tx is null) return new ScheduleChangePaymentDto { ScheduleChangeId = id, Status = "NOT_REQUIRED", PaymentStatus = "NOT_REQUIRED", ScheduleChangeStatus = change.Status.StatusCode, Message = change.Status.StatusCode == ScheduleChangeStatusCodes.Approved ? "Yêu cầu đã hoàn tất mà không cần thanh toán thêm." : "Yêu cầu không có khoản phải thanh toán." };
        if (tx.TrangThai == "PENDING")
        {
            try
            {
                var info = await _payOs.PaymentRequests.GetAsync(tx.OrderCode);
                if (string.Equals(info.Status.ToString(), "PAID", StringComparison.OrdinalIgnoreCase))
                    await ProcessVerifiedPaymentAsync(tx.OrderCode, decimal.ToInt64(tx.SoTienGiaoDich), "00", "Polled from PayOS", cancellationToken);
                else if (string.Equals(info.Status.ToString(), "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    tx.TrangThai = "CANCELLED";
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Không thể polling PayOS cho OrderCode={OrderCode}", tx.OrderCode); }
            change = await LoadChangeAsync(id, false, cancellationToken);
            tx = change.PaymentTransactions.OrderByDescending(x => x.NgayTao).First();
        }
        return PaymentDto(change, tx, null, null);
    }

    public async Task<bool> ProcessVerifiedPaymentAsync(long orderCode, long paidAmount, string responseCode,
        string payload, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var tx = await _context.PaymentTransactions.SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken);
        if (tx is null || tx.TransactionType != PaymentTransactionTypes.ScheduleChange) return false;
        if (tx.TrangThai == "PAID") { await CommitAsync(transaction, cancellationToken); return true; }
        var change = await LoadChangeAsync(tx.ScheduleChangeId!.Value, true, cancellationToken);
        var quote = change.Quotes.Single(x => x.Status == ScheduleChangeQuoteStatusCodes.Accepted);
        tx.PayloadWebhook = payload;
        if (responseCode != "00")
        {
            tx.TrangThai = "FAILED";
            await _context.SaveChangesAsync(cancellationToken); await CommitAsync(transaction, cancellationToken); return true;
        }
        if (paidAmount != decimal.ToInt64(tx.SoTienGiaoDich))
        {
            tx.TrangThai = "AMOUNT_MISMATCH";
            tx.ReconciliationData = $"ExpectedActual={tx.SoTienGiaoDich};Received={paidAmount}";
            await MoveToReconciliationAsync(change, "Số tiền PayOS không khớp giao dịch.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken); await CommitAsync(transaction, cancellationToken); return true;
        }

        tx.TrangThai = "PAID"; tx.NgayThanhToan = RiversideBusinessClock.Now;
        change.PaymentCompletedAt = tx.NgayThanhToan;
        change.Booking.PaidAmount += tx.SoTienGhiNhan;
        if (change.Booking.Contract is not null)
        {
            _context.Payments.Add(new Payment
            {
                ContractId = change.Booking.Contract.ContractId,
                PaymentType = PaymentTypes.ScheduleChange,
                Amount = tx.SoTienGhiNhan,
                PaymentDate = tx.NgayThanhToan.Value,
                PaymentMethod = tx.PhuongThuc,
                TransactionCode = tx.OrderCode.ToString(),
                StatusId = await _statuses.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success, cancellationToken)
            });
        }
        try
        {
            await FinalizeScheduleChangeAsync(change, quote, true, cancellationToken);
        }
        catch (ScheduleChangeException ex)
        {
            tx.ReconciliationData = ex.Message;
            await MoveToReconciliationAsync(change, ex.Message, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return true;
    }

    public async Task<ScheduleChangeDto> ReconcileAsync(int userId, int id, ReconciliationScheduleChangeRequest request,
        CancellationToken cancellationToken)
    {
        var change = await LoadChangeAsync(id, true, cancellationToken);
        RequireState(change, ScheduleChangeStatusCodes.ReconciliationRequired);
        var action = request.Action.Trim().ToUpperInvariant();
        if (action == "RETRY_APPLY")
        {
            var quote = change.Quotes.Single(x => x.Status == ScheduleChangeQuoteStatusCodes.Accepted);
            await FinalizeScheduleChangeAsync(change, quote, true, cancellationToken);
        }
        else if (action == "MARK_REFUND_REQUIRED")
        {
            var paid = change.PaymentTransactions.Where(x => x.TrangThai == "PAID").Sum(x => x.SoTienGhiNhan);
            change.Refunds.Add(new ScheduleChangeRefund
            {
                Amount = paid, Status = "PENDING_MANUAL", Reason = "Hoàn tiền sau lỗi áp dụng đổi lịch",
                CreatedAt = RiversideBusinessClock.Now, Evidence = request.Evidence.Trim()
            });
            change.ResolutionReason = request.Evidence.Trim(); change.ProcessedAt = RiversideBusinessClock.Now;
            await TransitionAsync(change, ScheduleChangeStatusCodes.Cancelled, userId, "MANAGER", "Đã ghi nhận yêu cầu hoàn tiền thủ công.", cancellationToken);
            ReleaseActiveHolds(change, ScheduleChangeHoldStatusCodes.Released);
        }
        else throw new ScheduleChangeException("Action đối soát không hợp lệ.");
        AddAudit(userId, change, "SCHEDULE_CHANGE_RECONCILED", request.Evidence.Trim());
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(change);
    }

    public async Task<ScheduleChangeFinancialDetailDto> GetFinancialDetailAsync(int id, CancellationToken cancellationToken)
    {
        var change = await LoadChangeAsync(id, false, cancellationToken);
        var amendment = await _context.ContractAmendments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ScheduleChangeId == id, cancellationToken);
        return new ScheduleChangeFinancialDetailDto
        {
            Transactions = change.PaymentTransactions.OrderByDescending(x => x.NgayTao).Select(x => new ScheduleChangeTransactionDto
            {
                TransactionId = x.GiaoDichId, OrderCode = x.OrderCode, ActualAmount = x.SoTienGiaoDich,
                BusinessAmount = x.SoTienGhiNhan, Status = x.TrangThai, CreatedAt = x.NgayTao,
                PaidAt = x.NgayThanhToan, ExpiresAt = x.ExpiresAt, ReconciliationData = x.ReconciliationData
            }).ToList(),
            Refunds = change.Refunds.OrderByDescending(x => x.CreatedAt).Select(x => new ScheduleChangeRefundDto
            {
                RefundId = x.RefundId, Amount = x.Amount, Status = x.Status, Reason = x.Reason,
                CreatedAt = x.CreatedAt, ProcessedAt = x.ProcessedAt
            }).ToList(),
            Amendment = amendment is null ? null : new ScheduleChangeAmendmentDto
            {
                AmendmentId = amendment.AmendmentId, ContractId = amendment.ContractId, Version = amendment.Version,
                Content = amendment.Content, ContractValueBefore = amendment.ContractValueBefore,
                ContractValueAfter = amendment.ContractValueAfter, CustomerConfirmedAt = amendment.CustomerConfirmedAt,
                EffectiveAt = amendment.EffectiveAt, Status = amendment.Status
            }
        };
    }

    public async Task<ScheduleChangeRefundDto> ConfirmManualRefundAsync(int userId, int id, int refundId,
        ManualRefundConfirmationRequest request, CancellationToken cancellationToken)
    {
        var refund = await _context.ScheduleChangeRefunds.Include(x => x.ScheduleChange)
            .SingleOrDefaultAsync(x => x.RefundId == refundId && x.ScheduleChangeId == id, cancellationToken)
            ?? throw new ScheduleChangeException("Không tìm thấy yêu cầu hoàn tiền.", StatusCodes.Status404NotFound);
        if (refund.Status != "PENDING_MANUAL")
            throw new ScheduleChangeException("Khoản hoàn tiền không ở trạng thái chờ xác nhận thủ công.", StatusCodes.Status409Conflict);
        refund.Status = "COMPLETED_MANUAL"; refund.ProcessedByUserId = userId;
        refund.ProcessedAt = RiversideBusinessClock.Now; refund.Evidence = request.Evidence.Trim();
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = AuditActions.ScheduleChangeRefundConfirmed, EntityName = AuditEntityNames.ScheduleChangeRefund,
            EntityId = refund.RefundId, NewData = JsonSerializer.Serialize(new { refund.Amount, refund.Status }),
            Timestamp = RiversideBusinessClock.Now, Outcome = AuditOutcomes.Success, Notes = refund.Evidence
        });
        await _context.SaveChangesAsync(cancellationToken);
        return new ScheduleChangeRefundDto
        { RefundId = refund.RefundId, Amount = refund.Amount, Status = refund.Status, Reason = refund.Reason,
            CreatedAt = refund.CreatedAt, ProcessedAt = refund.ProcessedAt };
    }

    public async Task ExpireDueHoldsAsync(CancellationToken cancellationToken)
    {
        var now = RiversideBusinessClock.Now;
        var ids = await _context.ScheduleChangeHolds.AsNoTracking()
            .Where(x => x.Status == ScheduleChangeHoldStatusCodes.Active && x.ExpiresAt <= now)
            .Select(x => x.ScheduleChangeId).Distinct().ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            try
            {
                var change = await LoadChangeAsync(id, true, cancellationToken);
                var tx = change.PaymentTransactions.OrderByDescending(x => x.NgayTao).FirstOrDefault();
                if (tx is not null && tx.TrangThai == "PENDING")
                {
                    try
                    {
                        var info = await _payOs.PaymentRequests.GetAsync(tx.OrderCode);
                        if (string.Equals(info.Status.ToString(), "PAID", StringComparison.OrdinalIgnoreCase))
                        {
                            await ProcessVerifiedPaymentAsync(tx.OrderCode, decimal.ToInt64(tx.SoTienGiaoDich), "00", "Expiry reconciliation poll", cancellationToken);
                            continue;
                        }
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "Không thể đối soát PayOS khi hold hết hạn."); }
                    tx.TrangThai = "EXPIRED";
                }
                ReleaseActiveHolds(change, ScheduleChangeHoldStatusCodes.Expired);
                if (change.Status.StatusCode == ScheduleChangeStatusCodes.PaymentPending)
                {
                    change.ProcessedAt = now;
                    await TransitionAsync(change, ScheduleChangeStatusCodes.Expired, null, "SYSTEM", "Hết thời gian thanh toán/giữ lịch.", cancellationToken);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) { _logger.LogError(ex, "Lỗi xử lý hết hạn đổi lịch {ScheduleChangeId}", id); }
        }
    }

    private async Task<WeddingScheduleChange> CreateRequestCoreAsync(int bookingId, int newHallId, DateTime newDate,
        string newShift, string reason, string source, int? actorUserId, string? exceptionReason,
        string? consentEvidence, CancellationToken cancellationToken)
    {
        var booking = await BookingQuery(true).SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken)
            ?? throw new ScheduleChangeException("Không tìm thấy booking.", StatusCodes.Status404NotFound);
        if (!ScheduleChangeBusinessRules.IsBookingEligible(booking.Status.StatusCode))
            throw new ScheduleChangeException("Chỉ booking CONFIRMED hoặc PREPARING mới được đổi lịch.", StatusCodes.Status409Conflict);
        if (booking.HallSchedule.Date.Date <= RiversideBusinessClock.Today)
            throw new ScheduleChangeException("Tiệc cũ đã hoặc đang diễn ra.", StatusCodes.Status409Conflict);
        if (await HasOpenRequestAsync(bookingId, null, cancellationToken))
            throw new ScheduleChangeException("Booking đang có một yêu cầu đổi lịch chưa kết thúc.", StatusCodes.Status409Conflict);

        var policy = await _policies.GetActiveAsync(RiversideBusinessClock.Now, cancellationToken);
        var days = ScheduleChangeBusinessRules.DaysBefore(booking.HallSchedule.Date, RiversideBusinessClock.Today);
        var tier = ScheduleChangeBusinessRules.ResolveTier(policy, Math.Max(0, days));
        if (source == ScheduleChangeSources.Customer && (days < policy.OnlineMinimumDays || !tier.CustomerOnlineAllowed))
            throw new ScheduleChangeException("Khách không được gửi yêu cầu trực tuyến dưới thời hạn tối thiểu.", StatusCodes.Status409Conflict);
        if (source == ScheduleChangeSources.HallManager && days < policy.OnlineMinimumDays &&
            (string.IsNullOrWhiteSpace(exceptionReason) || string.IsNullOrWhiteSpace(consentEvidence)))
            throw new ScheduleChangeException("Ngoại lệ dưới 7 ngày bắt buộc có lý do và bằng chứng khách đồng ý.");
        if (newDate.Date < RiversideBusinessClock.Today.AddDays(policy.NewDateMinimumDays))
            throw new ScheduleChangeException($"Ngày mới phải cách hôm nay ít nhất {policy.NewDateMinimumDays} ngày.");

        var schedule = await ResolveTargetScheduleAsync(newHallId, newDate.Date, NormalizeShift(newShift), booking, cancellationToken);
        if (schedule.HallScheduleId == booking.HallScheduleId)
            throw new ScheduleChangeException("Lịch mới không được trùng hoàn toàn lịch cũ.");
        var pendingId = await _statuses.GetStatusIdAsync(StatusGroups.ScheduleChange, ScheduleChangeStatusCodes.Pending, cancellationToken);
        var change = new WeddingScheduleChange
        {
            BookingId = booking.BookingId, Booking = booking, OldHallScheduleId = booking.HallScheduleId,
            OldHallSchedule = booking.HallSchedule, NewHallScheduleId = schedule.HallScheduleId, NewHallSchedule = schedule,
            Reason = reason.Trim(), RequestSource = source, CreatedByUserId = actorUserId,
            ExceptionReason = exceptionReason?.Trim(), CustomerConsentEvidence = consentEvidence?.Trim(),
            StatusId = pendingId, Status = await _context.Statuses.SingleAsync(x => x.StatusId == pendingId, cancellationToken),
            RequestedAt = RiversideBusinessClock.Now, PolicyId = policy.PolicyId, Policy = policy,
            PolicyVersion = policy.Version, SubmittedDaysBeforeEvent = days, AppliedFee = tier.FeeAmount
        };
        change.StatusHistory.Add(new ScheduleChangeStatusHistory
        { ToStatus = ScheduleChangeStatusCodes.Pending, ActorUserId = actorUserId, ActorType = source,
            Note = "Tạo yêu cầu đổi lịch.", ChangedAt = change.RequestedAt });
        _context.WeddingScheduleChanges.Add(change);
        await _context.SaveChangesAsync(cancellationToken);
        AddAudit(actorUserId, change, "SCHEDULE_CHANGE_CREATED", JsonSerializer.Serialize(new
        { booking.BookingCode, schedule.HallId, schedule.Date, schedule.Shift, policy.Version, Fee = tier.FeeAmount }));
        await _context.SaveChangesAsync(cancellationToken);
        return await LoadChangeAsync(change.ScheduleChangeId, false, cancellationToken);
    }

    private async Task<ScheduleChangePaymentDto> CreatePaymentLinkAsync(int scheduleChangeId, decimal businessAmount,
        DateTime paymentExpiresAt, CancellationToken cancellationToken)
    {
        var change = await LoadChangeAsync(scheduleChangeId, true, cancellationToken);
        var hold = change.Holds.Single(x => x.Status == ScheduleChangeHoldStatusCodes.Active);
        if (businessAmount <= 0) throw new ScheduleChangeException("Không tạo thanh toán cho số tiền không dương.");
        if (!_payOsOptions.Value.IsDemoMode && businessAmount > int.MaxValue)
            throw new ScheduleChangeException("Số tiền vượt giới hạn giao dịch PayOS.", StatusCodes.Status422UnprocessableEntity);
        var actualAmount = _payOsOptions.Value.IsDemoMode
            ? Math.Max(2000, decimal.ToInt32(decimal.Round(businessAmount / Math.Max(1, _payOsOptions.Value.DemoScaleRatio), 0, MidpointRounding.AwayFromZero)))
            : decimal.ToInt32(businessAmount);
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 10 + RandomNumberGenerator.GetInt32(0, 10);
        while (await _context.PaymentTransactions.AnyAsync(x => x.OrderCode == orderCode, cancellationToken)) orderCode++;
        var tx = new PaymentTransactionEntity
        {
            DatTiecId = change.BookingId, ScheduleChangeId = change.ScheduleChangeId,
            TransactionType = PaymentTransactionTypes.ScheduleChange, OrderCode = orderCode,
            SoTienGiaoDich = actualAmount, SoTienGhiNhan = businessAmount,
            PhuongThuc = "VIETQR_PAYOS", TrangThai = "PENDING", NgayTao = RiversideBusinessClock.Now,
            ExpiresAt = paymentExpiresAt
        };
        _context.PaymentTransactions.Add(tx);
        await _context.SaveChangesAsync(cancellationToken);
        hold.PaymentTransactionId = tx.GiaoDichId;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var response = await _payOs.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
            {
                OrderCode = orderCode, Amount = actualAmount, Description = $"DOI LICH {change.ScheduleChangeId}",
                CancelUrl = _payOsOptions.Value.ScheduleChangeCancelUrl,
                ReturnUrl = _payOsOptions.Value.ScheduleChangeReturnUrl,
                Items = [new PaymentLinkItem { Name = $"Doi lich {change.Booking.BookingCode}", Quantity = 1, Price = actualAmount }]
            });
            return PaymentDto(change, tx, response.CheckoutUrl, response.QrCode);
        }
        catch (Exception ex)
        {
            tx.TrangThai = "FAILED"; tx.ReconciliationData = "PAYMENT_LINK_CREATION_FAILED";
            hold.Status = ScheduleChangeHoldStatusCodes.Released; hold.ReleasedAt = RiversideBusinessClock.Now;
            change.ProcessedAt = RiversideBusinessClock.Now;
            await TransitionAsync(change, ScheduleChangeStatusCodes.Expired, null, "SYSTEM",
                "Không tạo được liên kết thanh toán; đã giải phóng giữ chỗ.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Không thể tạo link PayOS cho ScheduleChangeId={ScheduleChangeId}", scheduleChangeId);
            throw new ScheduleChangeException("Không thể tạo liên kết PayOS; lịch giữ tạm đã được giải phóng.", StatusCodes.Status502BadGateway);
        }
    }

    private async Task FinalizeScheduleChangeAsync(WeddingScheduleChange change, ScheduleChangeQuote quote,
        bool paymentReceived, CancellationToken cancellationToken)
    {
        if (paymentReceived && !change.PaymentTransactions.Any(x => x.TrangThai == "PAID"))
            throw new ScheduleChangeException("Chưa có bằng chứng thanh toán hợp lệ.", StatusCodes.Status409Conflict);
        if (quote.AmountDue > 0 && !paymentReceived)
            throw new ScheduleChangeException("Báo giá còn khoản phải thu.", StatusCodes.Status409Conflict);
        var hold = change.Holds.SingleOrDefault(x => x.Status == ScheduleChangeHoldStatusCodes.Active);
        if (hold is null || hold.ExpiresAt <= RiversideBusinessClock.Now)
            throw new ScheduleChangeException("Quyền giữ lịch đã hết hạn.", StatusCodes.Status409Conflict);
        var policy = await _context.ScheduleChangePolicies.SingleAsync(x => x.PolicyId == change.PolicyId, cancellationToken);
        if (change.NewHallSchedule.Date < RiversideBusinessClock.Today.AddDays(policy.NewDateMinimumDays))
            throw new ScheduleChangeException("Ngày mới không còn đủ 14 ngày tại thời điểm áp dụng.", StatusCodes.Status409Conflict);
        if (!ScheduleChangeBusinessRules.IsBookingEligible(change.Booking.Status.StatusCode))
            throw new ScheduleChangeException("Trạng thái booking đã thay đổi và không còn hợp lệ.", StatusCodes.Status409Conflict);
        await EnsureTargetAvailableAsync(change, true, cancellationToken);
        if (await HasCoordinatorConflictAsync(change, cancellationToken))
            throw new ScheduleChangeException("Điều phối viên hiện tại bị trùng lịch mới; cần phân công lại trước khi hoàn tất.", StatusCodes.Status409Conflict);

        var oldSchedule = change.OldHallSchedule;
        var newSchedule = change.NewHallSchedule;
        var booking = change.Booking;
        var oldSnapshot = new { booking.HallScheduleId, oldSchedule.Date, oldSchedule.Shift, HallId = oldSchedule.HallId,
            booking.FinalHallPrice, booking.EstimatedTotal };
        booking.HallScheduleId = newSchedule.HallScheduleId;
        booking.HallSchedule = newSchedule;
        var reviewQr = await _context.ReviewQrCodes.SingleOrDefaultAsync(
            qr => qr.BookingId == booking.BookingId, cancellationToken);
        if (reviewQr is not null && ChecklistTimingRules.Window(newSchedule) is { } reviewWindow)
            reviewQr.ExpiresAt = reviewWindow.End.AddHours(24);
        await ChecklistLifecycle.RecordAsync(_context, booking.BookingId, change.ApprovedByUserId,
            "Hệ thống đổi lịch", "RESCHEDULED",
            $"Đổi lịch {oldSchedule.Date:dd/MM/yyyy} {oldSchedule.Shift} → {newSchedule.Date:dd/MM/yyyy} {newSchedule.Shift}. Hạn công việc được dời tương ứng; giữ nguyên tiến độ.",
            null, cancellationToken);
        booking.FinalHallPrice = quote.NewHallPrice;
        booking.EstimatedTotal = quote.ContractValueAfter;
        newSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked, cancellationToken);
        var oldStillOccupied = await _context.WeddingBookings.AnyAsync(x => x.BookingId != booking.BookingId &&
            x.HallScheduleId == oldSchedule.HallScheduleId && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken);
        if (!oldStillOccupied) oldSchedule.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken);

        if (booking.Contract is not null)
        {
            if (booking.Contract.Status.StatusCode == ContractStatusCodes.Draft)
                booking.Contract.TotalValue = quote.ContractValueAfter;
            else if (booking.Contract.Status.StatusCode == ContractStatusCodes.Active)
                change.ContractAmendment = new ContractAmendment
                {
                    ContractId = booking.Contract.ContractId, ScheduleChangeId = change.ScheduleChangeId,
                    Version = booking.Contract.Amendments.Count + 1,
                    Content = $"Đổi lịch từ {oldSchedule.Date:dd/MM/yyyy} {oldSchedule.Shift} - {oldSchedule.Hall.HallName} sang {newSchedule.Date:dd/MM/yyyy} {newSchedule.Shift} - {newSchedule.Hall.HallName}.",
                    ContractValueBefore = quote.ContractValueBefore, ContractValueAfter = quote.ContractValueAfter,
                    CustomerConfirmedAt = change.CustomerConfirmedAt, ApprovedByUserId = change.ApprovedByUserId!.Value,
                    EffectiveAt = RiversideBusinessClock.Now, Status = "ACTIVE"
                };
        }
        if (quote.RefundAmount > 0)
            change.Refunds.Add(new ScheduleChangeRefund
            { Amount = quote.RefundAmount, Status = "PENDING_MANUAL", Reason = "Khoản hoàn phát sinh từ báo giá đổi lịch",
                CreatedAt = RiversideBusinessClock.Now });
        hold.Status = ScheduleChangeHoldStatusCodes.Consumed; hold.ReleasedAt = RiversideBusinessClock.Now;
        change.ProcessedAt = RiversideBusinessClock.Now;
        await TransitionAsync(change, ScheduleChangeStatusCodes.Approved, null, "SYSTEM", "Đã chuyển lịch thành công.", cancellationToken);
        AddAudit(null, change, "SCHEDULE_CHANGE_APPLIED", JsonSerializer.Serialize(new
        { Old = oldSnapshot, New = new { booking.HallScheduleId, newSchedule.Date, newSchedule.Shift, newSchedule.HallId,
            booking.FinalHallPrice, booking.EstimatedTotal }, quote.AdjustmentTotal }));
        await NotifyHallManagersAsync(change, cancellationToken);
    }

    private async Task MoveToReconciliationAsync(WeddingScheduleChange change, string reason, CancellationToken cancellationToken)
    {
        change.ResolutionReason = reason;
        if (change.Status.StatusCode != ScheduleChangeStatusCodes.ReconciliationRequired)
            await TransitionAsync(change, ScheduleChangeStatusCodes.ReconciliationRequired, null, "SYSTEM", reason, cancellationToken);
        var manager = await _context.Users.AsNoTracking().Where(x => x.Role.RoleName == RoleNames.Manager &&
            x.Status.StatusCode == AccountStatusCodes.Active).Select(x => (int?)x.UserId).FirstOrDefaultAsync(cancellationToken);
        if (manager.HasValue) await _notifications.AddAsync(manager.Value, null, "SCHEDULE_CHANGE_RECONCILIATION",
            "Đổi lịch cần đối soát", $"Yêu cầu #{change.ScheduleChangeId} đã nhận thanh toán nhưng chưa thể chuyển lịch: {reason}",
            "ScheduleChange", change.ScheduleChangeId, cancellationToken);
    }

    private async Task<HallSchedule> ResolveTargetScheduleAsync(int hallId, DateTime date, string shift,
        WeddingBooking booking, CancellationToken cancellationToken)
    {
        var existingId = await ExistingDataStatusIdAsync(cancellationToken);
        var hall = await _context.Halls.Include(x => x.Status).SingleOrDefaultAsync(x => x.HallId == hallId &&
            x.DataStatusId == existingId && x.Status.StatusCode == BusinessStatusCodes.Active, cancellationToken)
            ?? throw new ScheduleChangeException("Sảnh mới không tồn tại hoặc không hoạt động.");
        if (CapacityUnavailableReason(booking, hall) is { } capacityReason)
            throw new ScheduleChangeException(capacityReason);
        var schedule = await _context.HallSchedules.Include(x => x.Status).Include(x => x.Hall)
            .SingleOrDefaultAsync(x => x.HallId == hallId && x.Date == date && x.Shift == shift && x.DataStatusId == existingId, cancellationToken);
        if (schedule is null)
        {
            schedule = new HallSchedule
            {
                HallId = hallId, Hall = hall, Date = date, Shift = shift, DataStatusId = existingId,
                StatusId = await _statuses.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Available, cancellationToken)
            };
            _context.HallSchedules.Add(schedule);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(schedule).Reference(x => x.Status).LoadAsync(cancellationToken);
        }
        if (schedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
            throw new ScheduleChangeException("Lịch sảnh mới đang bị khóa.", StatusCodes.Status409Conflict);
        var occupied = await _context.WeddingBookings.AnyAsync(x => x.BookingId != booking.BookingId &&
            x.HallScheduleId == schedule.HallScheduleId && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken);
        if (occupied) throw new ScheduleChangeException("Lịch sảnh mới đã có booking khác.", StatusCodes.Status409Conflict);
        var held = await _context.ScheduleChangeHolds.AnyAsync(x => x.HallScheduleId == schedule.HallScheduleId &&
            x.Status == ScheduleChangeHoldStatusCodes.Active && x.ExpiresAt > RiversideBusinessClock.Now, cancellationToken);
        if (held) throw new ScheduleChangeException("Lịch sảnh mới đang được yêu cầu khác giữ tạm.", StatusCodes.Status409Conflict);
        return schedule;
    }

    private static string? CapacityUnavailableReason(WeddingBooking booking, Hall hall)
    {
        if (booking.GuestCount > hall.MaximumCapacity)
            return $"Tiệc có {booking.GuestCount} khách, vượt sức chứa tối đa {hall.MaximumCapacity} khách của sảnh.";
        if (hall.MinimumCapacity.HasValue && booking.GuestCount < hall.MinimumCapacity.Value)
            return $"Tiệc có {booking.GuestCount} khách, chưa đạt sức chứa tối thiểu {hall.MinimumCapacity.Value} khách của sảnh.";
        return null;
    }

    private async Task EnsureTargetAvailableAsync(WeddingScheduleChange change, bool allowOwnHold,
        CancellationToken cancellationToken)
    {
        if (change.NewHallSchedule.Hall.Status.StatusCode != BusinessStatusCodes.Active)
            throw new ScheduleChangeException("Sảnh mới không còn hoạt động.", StatusCodes.Status409Conflict);
        if (change.NewHallSchedule.Status.StatusCode == HallScheduleStatusCodes.Locked)
            throw new ScheduleChangeException("Lịch sảnh mới đang bị khóa.", StatusCodes.Status409Conflict);
        if (CapacityUnavailableReason(change.Booking, change.NewHallSchedule.Hall) is { } capacityReason)
            throw new ScheduleChangeException(capacityReason, StatusCodes.Status409Conflict);
        if (await _context.WeddingBookings.AnyAsync(x => x.BookingId != change.BookingId &&
            x.HallScheduleId == change.NewHallScheduleId && BookingStatusCodes.OccupyingSchedule.Contains(x.Status.StatusCode), cancellationToken))
            throw new ScheduleChangeException("Lịch sảnh mới đã có booking khác.", StatusCodes.Status409Conflict);
        if (await _context.ScheduleChangeHolds.AnyAsync(x => x.HallScheduleId == change.NewHallScheduleId &&
            (!allowOwnHold || x.ScheduleChangeId != change.ScheduleChangeId) &&
            x.Status == ScheduleChangeHoldStatusCodes.Active && x.ExpiresAt > RiversideBusinessClock.Now, cancellationToken))
            throw new ScheduleChangeException("Lịch sảnh mới đang được yêu cầu khác giữ tạm.", StatusCodes.Status409Conflict);
    }

    private async Task<bool> HasCoordinatorConflictAsync(WeddingScheduleChange change, CancellationToken cancellationToken)
    {
        var coordinatorIds = change.Booking.CoordinationAssignments
            .Where(x => x.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned)
            .Select(x => x.CoordinatorEmployeeId).ToList();
        if (coordinatorIds.Count == 0) return false;
        return await _context.CoordinationAssignments.AsNoTracking().AnyAsync(x =>
            coordinatorIds.Contains(x.CoordinatorEmployeeId) && x.BookingId != change.BookingId &&
            x.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned &&
            x.Booking.Status.StatusCode != BookingStatusCodes.Cancelled && x.Booking.Status.StatusCode != BookingStatusCodes.Completed &&
            x.Booking.HallSchedule.Date == change.NewHallSchedule.Date && x.Booking.HallSchedule.Shift == change.NewHallSchedule.Shift,
            cancellationToken);
    }

    private async Task ConsumeOtpTokenAsync(int bookingId, int? scheduleChangeId, string purpose, string token,
        CancellationToken cancellationToken)
    {
        var tokenHash = Hash(token);
        var now = RiversideBusinessClock.Now;
        var otp = await _context.ScheduleChangeOtps.SingleOrDefaultAsync(x => x.BookingId == bookingId &&
            x.ScheduleChangeId == scheduleChangeId && x.Purpose == purpose && x.VerificationTokenHash == tokenHash &&
            x.VerifiedAt != null && x.UsedAt == null && x.ExpiresAt > now, cancellationToken)
            ?? throw new ScheduleChangeException("Bằng chứng OTP không hợp lệ hoặc đã được sử dụng.", StatusCodes.Status403Forbidden);
        otp.UsedAt = now;
    }

    private async Task TransitionAsync(WeddingScheduleChange change, string to, int? actorUserId, string actorType,
        string? note, CancellationToken cancellationToken)
    {
        var from = change.Status.StatusCode;
        if (!ScheduleChangeBusinessRules.CanTransition(from, to))
            throw new ScheduleChangeException($"Không thể chuyển trạng thái từ {from} sang {to}.", StatusCodes.Status409Conflict);
        change.StatusId = await _statuses.GetStatusIdAsync(StatusGroups.ScheduleChange, to, cancellationToken);
        change.Status = await _context.Statuses.SingleAsync(x => x.StatusId == change.StatusId, cancellationToken);
        change.StatusHistory.Add(new ScheduleChangeStatusHistory
        { FromStatus = from, ToStatus = to, ActorUserId = actorUserId, ActorType = actorType,
            Note = note, ChangedAt = RiversideBusinessClock.Now });
    }

    private static void RequireState(WeddingScheduleChange change, string expected)
    {
        if (change.Status.StatusCode != expected)
            throw new ScheduleChangeException($"Yêu cầu phải ở trạng thái {expected}.", StatusCodes.Status409Conflict);
    }

    private IQueryable<WeddingBooking> BookingQuery(bool tracking)
    {
        var query = tracking ? _context.WeddingBookings.AsQueryable() : _context.WeddingBookings.AsNoTracking();
        return query.AsSplitQuery().Include(x => x.Customer).Include(x => x.Status)
            .Include(x => x.HallSchedule).ThenInclude(x => x.Hall).ThenInclude(x => x.Status)
            .Include(x => x.HallSchedule).ThenInclude(x => x.Status)
            .Include(x => x.Contract).ThenInclude(x => x!.Status)
            .Include(x => x.Contract).ThenInclude(x => x!.Amendments)
            .Include(x => x.CoordinationAssignments).ThenInclude(x => x.Status);
    }

    private IQueryable<WeddingScheduleChange> FullQuery() => _context.WeddingScheduleChanges.AsSplitQuery()
        .Include(x => x.Booking).ThenInclude(x => x.Customer)
        .Include(x => x.Booking).ThenInclude(x => x.Status)
        .Include(x => x.Booking).ThenInclude(x => x.Contract).ThenInclude(x => x!.Status)
        .Include(x => x.Booking).ThenInclude(x => x.Contract).ThenInclude(x => x!.Amendments)
        .Include(x => x.Booking).ThenInclude(x => x.CoordinationAssignments).ThenInclude(x => x.Status)
        .Include(x => x.OldHallSchedule).ThenInclude(x => x.Hall).ThenInclude(x => x.Status)
        .Include(x => x.OldHallSchedule).ThenInclude(x => x.Status)
        .Include(x => x.NewHallSchedule).ThenInclude(x => x.Hall).ThenInclude(x => x.Status)
        .Include(x => x.NewHallSchedule).ThenInclude(x => x.Status)
        .Include(x => x.Status).Include(x => x.Policy).ThenInclude(x => x.Tiers)
        .Include(x => x.Quotes).Include(x => x.Holds).Include(x => x.StatusHistory)
        .Include(x => x.PaymentTransactions).Include(x => x.Refunds);

    private async Task<WeddingBooking> FindBookingAsync(string code, string contact, CancellationToken cancellationToken)
    {
        var normalizedContact = contact.Trim();
        var normalizedEmail = normalizedContact.ToLowerInvariant();
        var lookupByEmail = normalizedContact.Contains('@');
        return await BookingQuery(false).SingleOrDefaultAsync(x =>
                x.BookingCode == code.Trim().ToUpperInvariant() &&
                (lookupByEmail ? x.Customer.Email != null && x.Customer.Email.ToLower() == normalizedEmail
                    : x.Customer.PhoneNumber == normalizedContact) &&
                x.Customer.DataStatus.DataStatusCode == DataStatusCodes.Existing, cancellationToken)
            ?? throw new ScheduleChangeException("Mã đặt tiệc hoặc email/số điện thoại không chính xác.", StatusCodes.Status404NotFound);
    }

    private async Task<WeddingScheduleChange> LoadChangeAsync(int id, bool tracking, CancellationToken cancellationToken)
    {
        var query = FullQuery();
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.ScheduleChangeId == id, cancellationToken)
            ?? throw new ScheduleChangeException("Không tìm thấy yêu cầu đổi lịch.", StatusCodes.Status404NotFound);
    }

    private async Task RequireOwnedChangeAsync(int changeId, int bookingId, CancellationToken cancellationToken)
    {
        if (!await _context.WeddingScheduleChanges.AnyAsync(x => x.ScheduleChangeId == changeId && x.BookingId == bookingId,
                cancellationToken))
            throw new ScheduleChangeException("Không tìm thấy yêu cầu đổi lịch thuộc booking.", StatusCodes.Status404NotFound);
    }

    private Task<bool> HasOpenRequestAsync(int bookingId, int? excludedId, CancellationToken cancellationToken) =>
        _context.WeddingScheduleChanges.AnyAsync(x => x.BookingId == bookingId &&
            (!excludedId.HasValue || x.ScheduleChangeId != excludedId.Value) &&
            ScheduleChangeStatusCodes.Open.Contains(x.Status.StatusCode), cancellationToken);

    private Task<bool> HallManagerOwnsBookingAsync(int userId, int bookingId, CancellationToken cancellationToken)
    {
        var halls = HallManagerQueryScope.CurrentHallIds(_context, userId, RiversideBusinessClock.Today);
        return _context.WeddingBookings.AnyAsync(x => x.BookingId == bookingId && halls.Contains(x.HallSchedule.HallId), cancellationToken);
    }

    private Task<bool> HallManagerOwnsChangeAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var halls = HallManagerQueryScope.CurrentHallIds(_context, userId, RiversideBusinessClock.Today);
        return _context.WeddingScheduleChanges.AnyAsync(x => x.ScheduleChangeId == id && halls.Contains(x.OldHallSchedule.HallId), cancellationToken);
    }

    private async Task NotifyHallManagersAsync(WeddingScheduleChange change, CancellationToken cancellationToken)
    {
        var hallIds = new[] { change.OldHallSchedule.HallId, change.NewHallSchedule.HallId }.Distinct().ToArray();
        var recipients = await _context.HallManagerAssignments.AsNoTracking().Where(x => hallIds.Contains(x.HallId) &&
                x.FromDate <= RiversideBusinessClock.Today && (!x.ToDate.HasValue || x.ToDate.Value >= RiversideBusinessClock.Today))
            .Select(x => x.HallManager.UserId).Distinct().ToListAsync(cancellationToken);
        foreach (var userId in recipients)
            await _notifications.AddAsync(userId, null, "SCHEDULE_CHANGE_APPROVED", "Lịch tiệc đã thay đổi",
                $"Booking {change.Booking.BookingCode} đã chuyển sang {change.NewHallSchedule.Date:dd/MM/yyyy} {change.NewHallSchedule.Shift} tại {change.NewHallSchedule.Hall.HallName}.",
                "ScheduleChange", change.ScheduleChangeId, cancellationToken);
    }

    private void AddAudit(int? userId, WeddingScheduleChange change, string action, string? notes) =>
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, EntityName = AuditEntityNames.ScheduleChange, EntityId = change.ScheduleChangeId,
            NewData = notes, Timestamp = RiversideBusinessClock.Now, Notes = notes
        });

    private static void ReleaseActiveHolds(WeddingScheduleChange change, string status)
    {
        foreach (var hold in change.Holds.Where(x => x.Status == ScheduleChangeHoldStatusCodes.Active))
        { hold.Status = status; hold.ReleasedAt = RiversideBusinessClock.Now; }
    }

    private static ScheduleChangeDto ToDto(WeddingScheduleChange x)
    {
        var quote = x.Quotes.OrderByDescending(q => q.Version).FirstOrDefault();
        return new ScheduleChangeDto
        {
            ScheduleChangeId = x.ScheduleChangeId, BookingId = x.BookingId, BookingCode = x.Booking.BookingCode,
            CustomerName = x.Booking.Customer.FullName, Source = x.RequestSource,
            Status = x.Status.StatusCode, StatusName = x.Status.StatusName, Reason = x.Reason ?? string.Empty,
            ExceptionReason = x.ExceptionReason, ReviewResult = x.ReviewResult, ResolutionReason = x.ResolutionReason,
            OldHallId = x.OldHallSchedule.HallId, OldHallName = x.OldHallSchedule.Hall.HallName,
            OldDate = x.OldHallSchedule.Date, OldShift = x.OldHallSchedule.Shift,
            NewHallId = x.NewHallSchedule.HallId, NewHallName = x.NewHallSchedule.Hall.HallName,
            NewDate = x.NewHallSchedule.Date, NewShift = x.NewHallSchedule.Shift,
            PolicyVersion = x.PolicyVersion, AppliedFee = x.AppliedFee,
            RequestedAt = x.RequestedAt, ProcessedAt = x.ProcessedAt,
            Quote = quote is null ? null : new ScheduleChangeQuoteDto
            {
                QuoteId = quote.QuoteId, Version = quote.Version, OldHallPrice = quote.OldHallPrice,
                NewHallPrice = quote.NewHallPrice, HallPriceDifference = quote.HallPriceDifference,
                ScheduleChangeFee = quote.ScheduleChangeFee, AdjustmentTotal = quote.AdjustmentTotal,
                ContractValueBefore = quote.ContractValueBefore, ContractValueAfter = quote.ContractValueAfter,
                AmountDue = quote.AmountDue, CreditAmount = quote.CreditAmount, RefundAmount = quote.RefundAmount,
                Status = quote.Status, CreatedAt = quote.CreatedAt, AcceptedAt = quote.AcceptedAt
            },
            History = x.StatusHistory.OrderBy(h => h.ChangedAt).Select(h => new ScheduleChangeHistoryDto
            { FromStatus = h.FromStatus, ToStatus = h.ToStatus, ActorType = h.ActorType, Note = h.Note, ChangedAt = h.ChangedAt }).ToList()
        };
    }

    private ScheduleChangePaymentDto PaymentDto(WeddingScheduleChange change, PaymentTransactionEntity tx,
        string? checkoutUrl, string? qrCode) => new()
    {
        ScheduleChangeId = change.ScheduleChangeId, OrderCode = tx.OrderCode, Status = tx.TrangThai,
        PaymentStatus = tx.TrangThai, ScheduleChangeStatus = change.Status.StatusCode,
        BusinessAmount = tx.SoTienGhiNhan, ActualAmount = tx.SoTienGiaoDich,
        IsDemoMode = _payOsOptions.Value.IsDemoMode, CheckoutUrl = checkoutUrl, QrCode = qrCode,
        ExpiresAt = tx.ExpiresAt, Message = change.Status.StatusCode == ScheduleChangeStatusCodes.Approved
            ? "Backend đã xác nhận thanh toán và chuyển lịch thành công."
            : change.Status.StatusCode == ScheduleChangeStatusCodes.ReconciliationRequired
                ? "Thanh toán đã được ghi nhận nhưng việc chuyển lịch chưa hoàn tất; yêu cầu đang được đối soát."
                : tx.TrangThai == "PAID" ? "Đã ghi nhận thanh toán; đang chờ Backend xác nhận chuyển lịch."
                : tx.TrangThai == "PENDING" ? "Đang chờ thanh toán." : "Giao dịch không còn hiệu lực."
    };

    private static string NormalizeShift(string value) => value.Trim().ToLowerInvariant() switch
    { "trưa" or "ca trưa" => "Ca trưa", "tối" or "ca tối" => "Ca tối", _ => throw new ScheduleChangeException("Ca tổ chức không hợp lệ.") };

    private static void ValidateOtpPurpose(string purpose, int? changeId)
    {
        if (purpose is not (ScheduleChangeOtpPurposes.CreateRequest or ScheduleChangeOtpPurposes.AcceptQuote))
            throw new ScheduleChangeException("Mục đích OTP không hợp lệ.");
        if (purpose == ScheduleChangeOtpPurposes.CreateRequest && changeId.HasValue ||
            purpose == ScheduleChangeOtpPurposes.AcceptQuote && !changeId.HasValue)
            throw new ScheduleChangeException("Liên kết OTP với yêu cầu đổi lịch không hợp lệ.");
    }

    private Task<byte> ExistingDataStatusIdAsync(CancellationToken cancellationToken) =>
        _context.DataStatuses.Where(x => x.DataStatusCode == DataStatusCodes.Existing)
            .Select(x => x.DataStatusId).SingleAsync(cancellationToken);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<IDbContextTransaction?> BeginSerializableAsync(CancellationToken cancellationToken) =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);
}

public sealed class ScheduleChangeExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleChangeExpirationWorker> _logger;
    public ScheduleChangeExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduleChangeExpirationWorker> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IScheduleChangeService>().ExpireDueHoldsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Schedule-change expiration worker failed."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}

using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public static class ScheduleChangeBusinessRules
{
    public static bool IsBookingEligible(string bookingStatus) =>
        bookingStatus is BookingStatusCodes.Confirmed or BookingStatusCodes.Preparing;

    public static int DaysBefore(DateTime eventDate, DateTime localToday) =>
        (eventDate.Date - localToday.Date).Days;

    public static ScheduleChangePolicyTier ResolveTier(ScheduleChangePolicy policy, int daysBefore)
    {
        var tier = policy.Tiers.SingleOrDefault(x =>
            daysBefore >= x.MinimumDays && (!x.MaximumDays.HasValue || daysBefore <= x.MaximumDays.Value));
        return tier ?? throw new InvalidOperationException("Chính sách đổi lịch không bao phủ mốc ngày này.");
    }

    public static decimal CalculateAdjustment(decimal oldHallPrice, decimal newHallPrice, decimal fee) =>
        newHallPrice - oldHallPrice + fee;

    public static bool IsNewDateAllowed(DateTime newDate, DateTime localToday, int minimumDays) =>
        newDate.Date >= localToday.Date.AddDays(minimumDays);

    public static bool IsCapacitySupported(int guestCount, int? minimumCapacity, int maximumCapacity) =>
        guestCount <= maximumCapacity && (!minimumCapacity.HasValue || guestCount >= minimumCapacity.Value);

    public static bool CanCustomerSubmit(ScheduleChangePolicy policy, int daysBefore) =>
        daysBefore >= policy.OnlineMinimumDays && ResolveTier(policy, Math.Max(0, daysBefore)).CustomerOnlineAllowed;

    public static bool CanResendOtp(DateTime? lastSentAt, DateTime now) =>
        !lastSentAt.HasValue || lastSentAt.Value <= now.AddSeconds(-60);

    public static bool CanAttemptOtp(ScheduleChangeOtp otp, DateTime now) =>
        otp.UsedAt is null && !otp.VerifiedAt.HasValue && otp.ExpiresAt > now && otp.FailedAttempts < 5;

    public static bool CanConsumeVerificationToken(ScheduleChangeOtp otp, DateTime now) =>
        otp.VerifiedAt.HasValue && otp.UsedAt is null && otp.ExpiresAt > now &&
        !string.IsNullOrWhiteSpace(otp.VerificationTokenHash);

    public static bool IsHoldActive(ScheduleChangeHold hold, DateTime now) =>
        hold.Status == ScheduleChangeHoldStatusCodes.Active && hold.ExpiresAt > now;

    public static void ValidatePolicy(ScheduleChangePolicy policy)
    {
        if (policy.OnlineMinimumDays <= 0 || policy.NewDateMinimumDays <= 0 ||
            policy.PaymentWindowMinutes <= 0 || policy.HoldWindowMinutes < policy.PaymentWindowMinutes)
            throw new ArgumentException("Mốc ngày/thời gian của chính sách không hợp lệ.");
        if (policy.Tiers.Count == 0 || policy.Tiers.Any(x => x.MinimumDays < 0 || x.FeeAmount < 0 ||
            (x.MaximumDays.HasValue && x.MaximumDays.Value < x.MinimumDays)))
            throw new ArgumentException("Mức phí của chính sách không hợp lệ.");

        var ordered = policy.Tiers.OrderBy(x => x.MinimumDays).ToList();
        if (ordered[0].MinimumDays != 0)
            throw new ArgumentException("Các mức phí phải bắt đầu từ 0 ngày.");
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (!ordered[i].MaximumDays.HasValue || ordered[i].MaximumDays!.Value + 1 != ordered[i + 1].MinimumDays)
                throw new ArgumentException("Các mức phí phải liên tục và không chồng lấn.");
        }
        if (ordered[^1].MaximumDays.HasValue)
            throw new ArgumentException("Mức phí cuối cùng phải không giới hạn ngày tối đa.");
    }

    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        (ScheduleChangeStatusCodes.Pending, ScheduleChangeStatusCodes.UnderReview) => true,
        (ScheduleChangeStatusCodes.Pending, ScheduleChangeStatusCodes.Cancelled) => true,
        (ScheduleChangeStatusCodes.Pending, ScheduleChangeStatusCodes.Rejected) => true,
        (ScheduleChangeStatusCodes.UnderReview, ScheduleChangeStatusCodes.AwaitingQuoteAcceptance) => true,
        (ScheduleChangeStatusCodes.UnderReview, ScheduleChangeStatusCodes.Rejected) => true,
        (ScheduleChangeStatusCodes.UnderReview, ScheduleChangeStatusCodes.Cancelled) => true,
        (ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, ScheduleChangeStatusCodes.PaymentPending) => true,
        (ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, ScheduleChangeStatusCodes.Approved) => true,
        (ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, ScheduleChangeStatusCodes.Cancelled) => true,
        (ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, ScheduleChangeStatusCodes.Expired) => true,
        (ScheduleChangeStatusCodes.PaymentPending, ScheduleChangeStatusCodes.Approved) => true,
        (ScheduleChangeStatusCodes.PaymentPending, ScheduleChangeStatusCodes.Expired) => true,
        (ScheduleChangeStatusCodes.PaymentPending, ScheduleChangeStatusCodes.ReconciliationRequired) => true,
        (ScheduleChangeStatusCodes.Expired, ScheduleChangeStatusCodes.ReconciliationRequired) => true,
        (ScheduleChangeStatusCodes.ReconciliationRequired, ScheduleChangeStatusCodes.Approved) => true,
        (ScheduleChangeStatusCodes.ReconciliationRequired, ScheduleChangeStatusCodes.Cancelled) => true,
        _ => false
    };
}

public static class RiversideBusinessClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime;
    public static DateTime Today => Now.Date;

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        throw new InvalidOperationException("Không tìm thấy timezone Asia/Ho_Chi_Minh trên máy chủ.");
    }
}

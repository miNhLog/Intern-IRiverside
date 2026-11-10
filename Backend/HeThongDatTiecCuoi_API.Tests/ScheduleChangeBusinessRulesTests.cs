using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ScheduleChangeBusinessRulesTests
{
    [Theory]
    [InlineData(BookingStatusCodes.Confirmed, true)]
    [InlineData(BookingStatusCodes.Preparing, true)]
    [InlineData(BookingStatusCodes.Pending, false)]
    [InlineData(BookingStatusCodes.Completed, false)]
    [InlineData(BookingStatusCodes.Cancelled, false)]
    public void Booking_status_is_enforced(string status, bool expected) =>
        Assert.Equal(expected, ScheduleChangeBusinessRules.IsBookingEligible(status));

    [Theory]
    [InlineData(0, 5_000_000, false)]
    [InlineData(6, 5_000_000, false)]
    [InlineData(7, 5_000_000, true)]
    [InlineData(14, 5_000_000, true)]
    [InlineData(29, 5_000_000, true)]
    [InlineData(30, 2_000_000, true)]
    [InlineData(44, 2_000_000, true)]
    [InlineData(45, 0, true)]
    [InlineData(365, 0, true)]
    public void Policy_boundaries_resolve_exactly(int days, decimal fee, bool online)
    {
        var tier = ScheduleChangeBusinessRules.ResolveTier(Policy(), days);
        Assert.Equal(fee, tier.FeeAmount);
        Assert.Equal(online, tier.CustomerOnlineAllowed);
    }

    [Fact]
    public void Every_change_at_45_days_or_more_is_free() =>
        Assert.All(Enumerable.Range(45, 200), days => Assert.Equal(0, ScheduleChangeBusinessRules.ResolveTier(Policy(), days).FeeAmount));

    [Fact]
    public void Hall_manager_exception_under_7_days_has_base_fee()
    {
        var tier = ScheduleChangeBusinessRules.ResolveTier(Policy(), 3);
        Assert.False(tier.CustomerOnlineAllowed);
        Assert.Equal(5_000_000m, tier.FeeAmount);
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    public void New_date_must_be_at_least_14_local_calendar_days(int offset, bool expected)
    {
        var today = new DateTime(2026, 10, 22);
        Assert.Equal(expected, ScheduleChangeBusinessRules.IsNewDateAllowed(today.AddDays(offset), today, 14));
    }

    [Theory]
    [InlineData(100, 150, 500, false)]
    [InlineData(150, 150, 500, true)]
    [InlineData(500, 150, 500, true)]
    [InlineData(501, 150, 500, false)]
    public void Hall_capacity_is_enforced(int guests, int min, int max, bool expected) =>
        Assert.Equal(expected, ScheduleChangeBusinessRules.IsCapacitySupported(guests, min, max));

    [Theory]
    [InlineData(20_000_000, 25_000_000, 2_000_000, 7_000_000)]
    [InlineData(20_000_000, 15_000_000, 5_000_000, 0)]
    [InlineData(30_000_000, 20_000_000, 0, -10_000_000)]
    public void Adjustment_formula_is_snapshot_safe(decimal oldPrice, decimal newPrice, decimal fee, decimal expected) =>
        Assert.Equal(expected, ScheduleChangeBusinessRules.CalculateAdjustment(oldPrice, newPrice, fee));

    [Theory]
    [InlineData(ScheduleChangeStatusCodes.Pending, ScheduleChangeStatusCodes.UnderReview, true)]
    [InlineData(ScheduleChangeStatusCodes.Pending, ScheduleChangeStatusCodes.Approved, false)]
    [InlineData(ScheduleChangeStatusCodes.UnderReview, ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, true)]
    [InlineData(ScheduleChangeStatusCodes.AwaitingQuoteAcceptance, ScheduleChangeStatusCodes.PaymentPending, true)]
    [InlineData(ScheduleChangeStatusCodes.PaymentPending, ScheduleChangeStatusCodes.Approved, true)]
    [InlineData(ScheduleChangeStatusCodes.Expired, ScheduleChangeStatusCodes.ReconciliationRequired, true)]
    [InlineData(ScheduleChangeStatusCodes.Approved, ScheduleChangeStatusCodes.Pending, false)]
    public void Workflow_prevents_skipping_steps(string from, string to, bool expected) =>
        Assert.Equal(expected, ScheduleChangeBusinessRules.CanTransition(from, to));

    [Fact]
    public void Policy_requires_contiguous_non_overlapping_tiers()
    {
        var policy = Policy();
        policy.Tiers.Single(x => x.MinimumDays == 30).MinimumDays = 29;
        Assert.Throws<ArgumentException>(() => ScheduleChangeBusinessRules.ValidatePolicy(policy));
    }

    [Fact]
    public void Payment_window_must_not_exceed_hold_window()
    {
        var policy = Policy();
        policy.PaymentWindowMinutes = 21;
        Assert.Throws<ArgumentException>(() => ScheduleChangeBusinessRules.ValidatePolicy(policy));
    }

    [Fact]
    public void Otp_is_rejected_when_expired_locked_or_reused()
    {
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        Assert.False(ScheduleChangeBusinessRules.CanAttemptOtp(new ScheduleChangeOtp { ExpiresAt = now }, now));
        Assert.False(ScheduleChangeBusinessRules.CanAttemptOtp(new ScheduleChangeOtp { ExpiresAt = now.AddMinutes(1), FailedAttempts = 5 }, now));
        Assert.False(ScheduleChangeBusinessRules.CanAttemptOtp(new ScheduleChangeOtp { ExpiresAt = now.AddMinutes(1), UsedAt = now }, now));
        Assert.False(ScheduleChangeBusinessRules.CanAttemptOtp(new ScheduleChangeOtp { ExpiresAt = now.AddMinutes(1), VerifiedAt = now }, now));
        Assert.True(ScheduleChangeBusinessRules.CanAttemptOtp(new ScheduleChangeOtp { ExpiresAt = now.AddMinutes(1), FailedAttempts = 4 }, now));
    }

    [Fact]
    public void Verified_otp_token_is_single_use()
    {
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        var otp = new ScheduleChangeOtp { ExpiresAt = now.AddMinutes(1), VerifiedAt = now, VerificationTokenHash = "hash" };
        Assert.True(ScheduleChangeBusinessRules.CanConsumeVerificationToken(otp, now));
        otp.UsedAt = now;
        Assert.False(ScheduleChangeBusinessRules.CanConsumeVerificationToken(otp, now));
    }

    [Fact]
    public void Otp_resend_is_blocked_for_60_seconds()
    {
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        Assert.False(ScheduleChangeBusinessRules.CanResendOtp(now.AddSeconds(-59), now));
        Assert.True(ScheduleChangeBusinessRules.CanResendOtp(now.AddSeconds(-60), now));
    }

    [Fact]
    public void Expired_hold_is_never_active()
    {
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        Assert.False(ScheduleChangeBusinessRules.IsHoldActive(new ScheduleChangeHold { Status = ScheduleChangeHoldStatusCodes.Active, ExpiresAt = now }, now));
        Assert.True(ScheduleChangeBusinessRules.IsHoldActive(new ScheduleChangeHold { Status = ScheduleChangeHoldStatusCodes.Active, ExpiresAt = now.AddSeconds(1) }, now));
    }

    private static ScheduleChangePolicy Policy() => new()
    {
        OnlineMinimumDays = 7, NewDateMinimumDays = 14,
        PaymentWindowMinutes = 15, HoldWindowMinutes = 20,
        Tiers =
        [
            new() { MinimumDays = 0, MaximumDays = 6, FeeAmount = 5_000_000, CustomerOnlineAllowed = false },
            new() { MinimumDays = 7, MaximumDays = 29, FeeAmount = 5_000_000, CustomerOnlineAllowed = true },
            new() { MinimumDays = 30, MaximumDays = 44, FeeAmount = 2_000_000, CustomerOnlineAllowed = true },
            new() { MinimumDays = 45, MaximumDays = null, FeeAmount = 0, CustomerOnlineAllowed = true }
        ]
    };
}

using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Options;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ReviewQrFeatureTests
{
    [Fact]
    public void Eligibility_DoesNotRequirePayment_AndAcceptsPendingBooking()
    {
        var policy = new ReviewQrEligibilityPolicy(Microsoft.Extensions.Options.Options.Create(new ReviewQrOptions { PaymentGateEnabled = false }));
        var schedule = new HallSchedule { Date = DateTime.Today.AddDays(2), Shift = "Ca trưa" };

        Assert.True(policy.IsEligible(BookingStatusCodes.Pending, schedule));
    }

    [Fact]
    public void Eligibility_RejectsCancelledBooking()
    {
        var policy = new ReviewQrEligibilityPolicy(Microsoft.Extensions.Options.Options.Create(new ReviewQrOptions { PaymentGateEnabled = false }));
        var schedule = new HallSchedule { Date = DateTime.Today.AddDays(2), Shift = "Ca tối" };

        Assert.False(policy.IsEligible(BookingStatusCodes.Cancelled, schedule));
    }

    [Fact]
    public void Window_FutureEvent_IsPending_AndExpires24HoursAfterShiftEnd()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today.AddDays(3), Shift = "Ca trưa" };

        var window = service.GetWindow(schedule, false);

        Assert.Equal(ReviewQrState.Pending, window.State);
        Assert.Equal(schedule.Date.Date.AddHours(10.5), window.OpensAt);
        Assert.Equal(schedule.Date.Date.AddHours(38), window.ExpiresAt);
    }

    [Fact]
    public void Window_CancelledOrLockedBooking_IsLockedRegardlessOfTime()
    {
        var service = ServiceWithoutDatabase();
        var schedule = new HallSchedule { Date = RiversideBusinessClock.Today, Shift = "Ca tối" };

        Assert.Equal(ReviewQrState.Locked, service.GetWindow(schedule, true).State);
    }

    private static ReviewQrService ServiceWithoutDatabase() =>
        new(null!, null!, NullLogger<ReviewQrService>.Instance);

    [Theory]
    [InlineData(5, "Positive")]
    [InlineData(4, "Positive")]
    [InlineData(3, "Neutral")]
    [InlineData(2, "Negative")]
    [InlineData(1, "Negative")]
    public void Statistics_ClassifiesOverallScoreCorrectly(int score, string expected) =>
        Assert.Equal(expected, ReviewStatisticsService.ClassifyScore(score));

    [Fact]
    public void Statistics_EmptySet_HasNoMisleadingPercentage() =>
        Assert.Null(ReviewStatisticsService.CalculateRate(0, 0));

    [Fact]
    public void Statistics_CalculatesPercentageFromActualReviewCount() =>
        Assert.Equal(66.7, ReviewStatisticsService.CalculateRate(2, 3));
}

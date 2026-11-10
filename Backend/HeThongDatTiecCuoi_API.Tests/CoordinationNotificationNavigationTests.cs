using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class CoordinationNotificationNavigationTests
{
    [Theory]
    [InlineData(NotificationTypeCodes.CoordinationAssigned, 42, RoleNames.Coordinator, true, "/dieu-phoi/checklist?bookingId=42")]
    [InlineData(NotificationTypeCodes.CoordinationChanged, 42, RoleNames.Coordinator, true, "/dieu-phoi/checklist?bookingId=42")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, 42, RoleNames.Coordinator, true, "/dieu-phoi/checklist?bookingId=42&tab=history")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, 42, RoleNames.HallManager, true, "/dieu-phoi/checklist?bookingId=42&tab=history")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, null, RoleNames.Coordinator, true, "/thong-bao")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, 0, RoleNames.Coordinator, true, "/thong-bao")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, 42, RoleNames.Coordinator, false, "/thong-bao")]
    [InlineData(NotificationTypeCodes.CoordinationCancelled, 42, RoleNames.Admin, true, "/thong-bao")]
    public async Task ExistingNotifications_ResolveNavigationAtReadTime_WithoutRewritingMessage(string type, int? bookingId,
        string role, bool matchingEntity, string expected)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Role { RoleId = 1, RoleName = role });
        db.Users.Add(new User { UserId = 10, RoleId = 1, Email = "reader@example.test", PasswordHash = "test" });
        const string oldMessage = "Nhiệm vụ điều phối đã bị hủy theo trạng thái tiệc.";
        db.Notifications.Add(new Notification { RecipientUserId = 10, Type = type, Title = "Tiệc", Message = oldMessage,
            RelatedEntityId = bookingId, RelatedEntityType = matchingEntity ? AuditEntityNames.CoordinationAssignment : "OtherEntity" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new NotificationService(db);
        Assert.Equal(expected, Assert.Single(await service.GetRecentAsync(10, 5, default)).NavigationUrl);
        Assert.Equal(expected, Assert.Single((await service.GetPageAsync(10, 1, 10, null, default)).Items).NavigationUrl);
        Assert.Equal(oldMessage, (await db.Notifications.SingleAsync()).Message);
        Assert.False(db.ChangeTracker.HasChanges());
    }
}

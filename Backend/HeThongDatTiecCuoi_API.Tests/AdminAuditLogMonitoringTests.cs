using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.AuditLog;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class AdminAuditLogMonitoringTests
{
    [Fact]
    public void Important_registry_matches_monitoring_rules()
    {
        Assert.True(AuditImportantGroups.IsImportant(AuditActions.LoginBlocked));
        Assert.False(AuditImportantGroups.IsImportant(AuditActions.LoginFailed));
        Assert.False(AuditImportantGroups.IsImportant(AuditActions.LoginSuccess));
        Assert.False(AuditImportantGroups.IsImportant(AuditActions.Logout));
        Assert.Contains(AuditActions.ScheduleChangeRefundConfirmed, AuditImportantGroups.FinanceActions);
        Assert.Contains(AuditActions.IncidentStatusChanged, AuditImportantGroups.OperationsActions);
    }

    [Fact]
    public void Role_change_events_are_grouped_with_accounts_and_authorization()
    {
        var accountActions = AuditActions.ForGroup("account");

        Assert.NotNull(accountActions);
        Assert.Contains(AuditActions.RoleChangeRequested, accountActions!);
        Assert.Contains(AuditActions.RoleChangeApproved, accountActions!);
        Assert.Contains(AuditActions.RoleChangeRejected, accountActions!);
        Assert.Equal("account", AuditActions.GroupOf(AuditActions.RoleChangeRequested));
    }

    [Fact]
    public void Review_rule_does_not_flag_successful_important_actions()
    {
        Assert.True(AuditImportantGroups.RequiresReview(AuditActions.LoginBlocked, AuditOutcomes.Success));
        Assert.True(AuditImportantGroups.RequiresReview(AuditActions.LockAccount, AuditOutcomes.Failed));
        Assert.False(AuditImportantGroups.RequiresReview(AuditActions.LockAccount, AuditOutcomes.Success));
        Assert.False(AuditImportantGroups.RequiresReview(AuditActions.Payment, AuditOutcomes.Failed));
    }

    [Fact]
    public void Controller_remains_restricted_to_administrators()
    {
        var attribute = Assert.Single(typeof(AdminAuditLogController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Admin, attribute.Roles);
    }

    [Fact]
    public async Task Important_mode_defaults_to_seven_calendar_days_filters_before_paging_and_counts_full_scope()
    {
        await using var db = CreateContext();
        var today = RiversideBusinessClock.Today;
        db.AuditLogs.AddRange(
            Log(AuditActions.LoginBlocked, AuditOutcomes.Blocked, today.AddHours(8)),
            Log(AuditActions.LoginFailed, AuditOutcomes.Failed, today.AddHours(9)),
            Log(AuditActions.LockAccount, AuditOutcomes.Failed, today.AddHours(10)),
            Log(AuditActions.RoleChangeApproved, AuditOutcomes.Success, today.AddHours(11)),
            Log(AuditActions.PaymentRecorded, AuditOutcomes.Success, today.AddHours(12)),
            Log(AuditActions.BookingCancelled, AuditOutcomes.Success, today.AddDays(-7).AddHours(12)));
        await db.SaveChangesAsync();

        var page = await GetPageAsync(new AdminAuditLogController(db), mode: AuditLogModes.Important, pageSize: 2);

        Assert.True(page.AppliedDefaultPeriod);
        Assert.Equal(today.AddDays(-6), page.EffectiveFromDate);
        Assert.Equal(today, page.EffectiveToDate);
        Assert.Equal(4, page.TotalItems);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.Statistics.NeedsReview);
        Assert.Equal(1, page.Statistics.RoleChanges);
        Assert.Equal(1, page.Statistics.FinancialEvents);
        Assert.DoesNotContain(page.Items, item => item.Action == AuditActions.LoginFailed);
    }

    [Fact]
    public async Task Security_priority_excludes_login_failed_but_full_authentication_keeps_it()
    {
        await using var db = CreateContext();
        var today = RiversideBusinessClock.Today;
        db.AuditLogs.AddRange(
            Log(AuditActions.LoginBlocked, AuditOutcomes.Blocked, today.AddHours(8)),
            Log(AuditActions.LoginFailed, AuditOutcomes.Failed, today.AddHours(9)),
            Log(AuditActions.LoginSuccess, AuditOutcomes.Success, today.AddHours(10)),
            Log(AuditActions.LoginFailed, AuditOutcomes.Failed, today.AddDays(-30).AddHours(9)));
        await db.SaveChangesAsync();
        var controller = new AdminAuditLogController(db);

        var important = await GetPageAsync(controller, mode: AuditLogModes.Important, importantGroup: AuditImportantGroups.Security);
        var allAuthentication = await GetPageAsync(controller, mode: AuditLogModes.All, group: "authentication");

        Assert.Single(important.Items);
        Assert.Equal(AuditActions.LoginBlocked, important.Items[0].Action);
        Assert.Equal(2, allAuthentication.Items.Count(item => item.Action == AuditActions.LoginFailed));
        Assert.Contains(allAuthentication.Items, item => item.Action == AuditActions.LoginSuccess);
        Assert.Contains(allAuthentication.Items, item => item.Timestamp.Date == today.AddDays(-30));
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"admin-audit-monitoring-{Guid.NewGuid()}").Options);

    private static AuditLog Log(string action, string outcome, DateTime timestamp) => new()
    {
        Action = action,
        Outcome = outcome,
        Timestamp = timestamp,
        EntityName = AuditEntityNames.User,
        EntityId = 999,
        Notes = action
    };

    private static async Task<AuditLogPageDto> GetPageAsync(
        AdminAuditLogController controller,
        string mode,
        int pageSize = 20,
        string? importantGroup = null,
        string? group = null)
    {
        var result = await controller.GetAuditLogs(pageSize: pageSize, mode: mode, importantGroup: importantGroup, group: group);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AuditLogPageDto>(ok.Value);
    }
}

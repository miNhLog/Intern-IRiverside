using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Helpers;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class AuthenticationAuditSecurityTests
{
    [Fact]
    public void Sanitizer_Redacts_Sensitive_Values_At_Every_Depth()
    {
        const string source = """{"email":"staff@example.test","credentials":{"password":"secret","items":[{"otp":"123456"}]},"accessToken":"jwt"}""";

        var sanitized = AuditDataSanitizer.Sanitize(source)!;

        Assert.DoesNotContain("secret", sanitized);
        Assert.DoesNotContain("123456", sanitized);
        Assert.DoesNotContain("jwt", sanitized);
        Assert.Contains("staff@example.test", sanitized);
        Assert.Equal("[Đã ẩn]", JsonDocument.Parse(sanitized).RootElement
            .GetProperty("credentials").GetProperty("password").GetString());
    }

    [Fact]
    public void Authentication_Actions_Are_Admin_Visible()
    {
        var expected = new[]
        {
            AuditActions.LoginSuccess, AuditActions.LoginFailed, AuditActions.LoginBlocked,
            AuditActions.Logout, AuditActions.PasswordChanged, AuditActions.PasswordResetCompleted
        };

        Assert.All(expected, action => Assert.Contains(action, AuditActions.AdminVisibleActions));
    }

    [Fact]
    public void Authentication_Action_Names_Fit_Existing_Database_Column()
    {
        Assert.All(AuditActions.AuthenticationActions, action => Assert.True(action.Length <= 30));
    }

    [Fact]
    public void Business_Audit_Groups_Are_Allowlisted_And_Fit_Migrated_Column()
    {
        var groups = new[] { "booking", "contract", "payment", "schedule", "catalog", "workforce", "incident" };
        foreach (var group in groups)
        {
            var actions = AuditActions.ForGroup(group);
            Assert.NotNull(actions);
            Assert.NotEmpty(actions!);
            Assert.All(actions!, action =>
            {
                Assert.Contains(action, AuditActions.AdminVisibleActions);
                Assert.True(action.Length <= 50);
            });
        }
    }

    [Fact]
    public void Unknown_Audit_Group_Is_Rejected_By_Registry()
    {
        Assert.Null(AuditActions.ForGroup("not-a-real-group"));
    }
}

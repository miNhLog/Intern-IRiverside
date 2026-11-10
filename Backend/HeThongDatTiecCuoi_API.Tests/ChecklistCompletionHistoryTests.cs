using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ChecklistCompletionHistoryTests
{
    [Fact]
    public void ExistingSchema_DoesNotRequireCompletionColumn()
    {
        // Generate SQL only; never opens a connection or changes a database.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        Assert.Null(db.Model.FindEntityType(typeof(PartyChecklistTask))!.FindProperty(nameof(PartyChecklistTask.CompletedAt)));
        Assert.DoesNotContain("NgayHoanThanh", db.PartyChecklistTasks.ToQueryString());
    }

    [Fact]
    public void LegacyHistory_UsesActualCompletion_NotLaterNotesOrRepeatedDone()
    {
        var (c, task) = Example();
        var original = new DateTime(2026, 11, 26, 18, 0, 0);
        Add(c, 1, "TASK", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", original);
        Add(c, 2, "NOTE", "Ghi chú bổ sung", original.AddDays(1)); task.UpdatedAt = original.AddDays(1);
        Add(c, 3, "TASK:1", $"Công việc “{task.Name}”: Hoàn thành → Hoàn thành.", original.AddDays(2));
        Assert.Equal(original, task.CompletedAt);
    }

    [Fact]
    public void DuplicateNames_RequireTaskIdEvidence_AndSupportRenamedTasks()
    {
        var (c, task) = Example();
        c.Tasks.Add(new PartyChecklistTask { Id = 2, Name = task.Name, Checklist = c, Status = "done" });
        var completed = new DateTime(2026, 11, 26, 18, 0, 0);
        Add(c, 1, "TASK", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", completed);
        Assert.Null(task.CompletedAt);
        Add(c, 2, "TASK:1", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", completed);
        Assert.Equal(completed, task.CompletedAt);
        Assert.Null(c.Tasks.Single(t => t.Id == 2).CompletedAt);
        task.Name = "Tên mới";
        Assert.Equal(completed, task.CompletedAt);
    }

    [Fact]
    public void ReopenedWork_UsesNewCompletion_AndMissingEvidenceRemainsUnverified()
    {
        var (c, task) = Example();
        Assert.Null(task.CompletedAt);
        var original = new DateTime(2026, 11, 26, 18, 0, 0);
        Add(c, 1, "TASK:1", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", original);
        Add(c, 2, "TASK:1", $"Công việc “{task.Name}”: Hoàn thành → Chưa làm.", original.AddMinutes(1));
        task.Status = "todo"; Assert.Null(task.CompletedAt);
        task.Status = "done"; Assert.Null(task.CompletedAt);
        Add(c, 3, "TASK:1", $"Công việc “{task.Name}”: Chưa làm → Hoàn thành.", original.AddMinutes(2));
        Assert.Equal(original.AddMinutes(2), task.CompletedAt);
    }

    private static (PartyChecklist, PartyChecklistTask) Example()
    {
        var c = new PartyChecklist { BookingId = 1 };
        var t = new PartyChecklistTask { Id = 1, BookingId = 1, Name = "Phục vụ món", Status = "done", Checklist = c };
        c.Tasks.Add(t); return (c, t);
    }
    private static void Add(PartyChecklist c, long id, string action, string text, DateTime timestamp) =>
        c.History.Add(new PartyChecklistHistory { Id = id, Action = action, Text = text, Timestamp = timestamp });
}

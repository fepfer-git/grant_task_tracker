using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Tests.Infrastructure;

/// <summary>
/// How dates and instants are stored in SQLite (DESIGN.md §3.6). Calendar dates are <c>'YYYY-MM-DD'</c> text and are
/// never converted. Instants are UTC: marked UTC on read, refused on write if they aren't UTC.
/// </summary>
public class DateStorageTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);

    [Fact]
    public void Given_UtcInstant_When_SavedAndReadBack_Then_ValueIsUnchangedAndKindIsUtc()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var taskId = Guid.NewGuid();
        using (var db = database.CreateContext())
        {
            var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", new DateOnly(2026, 3, 1));
            grant.AddTask(taskId, "Task", Today, TaskPriority.Low, Today, new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc));
            db.Grants.Add(grant);
            db.SaveChanges();
        }

        using var query = database.CreateContext();

        // Act
        var createdAtUtc = query.GrantTasks.Single(t => t.Id == taskId).CreatedAtUtc;

        // Assert: the same instant, not shifted by the machine's time zone, and marked UTC.
        Assert.Equal(new DateTime(2026, 3, 10, 22, 0, 0), createdAtUtc);
        Assert.Equal(DateTimeKind.Utc, createdAtUtc.Kind);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Given_LocalOrUnspecifiedDateTime_When_Saving_Then_SaveFails(DateTimeKind kind)
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();
        var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", new DateOnly(2026, 3, 1));
        grant.AddTask(Guid.NewGuid(), "Task", Today, TaskPriority.Low, Today, new DateTime(2026, 3, 11, 9, 0, 0, kind));
        db.Grants.Add(grant);

        // Act
        var exception = Record.Exception(() => db.SaveChanges());

        // Assert: EF wraps the converter's refusal, and nothing is stored.
        var updateException = Assert.IsType<DbUpdateException>(exception);
        var refusal = Assert.IsType<InvalidOperationException>(updateException.InnerException);
        Assert.Contains($"Kind = {kind}", refusal.Message);
        Assert.Empty(database.ReadText("SELECT Id FROM GrantTasks"));
    }

    [Fact]
    public void Given_DueDate_When_Saved_Then_StoredAsZeroPaddedIsoText()
    {
        // Arrange: a single-digit month and day.
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();
        var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", new DateOnly(2026, 3, 1));
        grant.AddTask(Guid.NewGuid(), "Task", new DateOnly(2026, 3, 5), TaskPriority.Low, new DateOnly(2026, 3, 1),
            new DateTime(2026, 2, 28, 22, 0, 0, DateTimeKind.Utc));
        db.Grants.Add(grant);

        // Act
        db.SaveChanges();

        // Assert
        Assert.Equal(["2026-03-05"], database.ReadText("SELECT DueDate FROM GrantTasks"));
    }

    [Fact]
    public void Given_DueDatesAcrossMonthAndYearEnds_When_FilteringDueDateBeforeTodayInSql_Then_ResultIsCorrect()
    {
        // Arrange: today is 2026-10-01. Unpadded text would put "2026-2-1" after "2026-10-01".
        var today = new DateOnly(2026, 10, 1);
        using var database = new SqliteTestDatabase();
        using (var db = database.CreateContext())
        {
            var applicationDate = new DateOnly(2025, 1, 1);
            var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", applicationDate);
            foreach (var dueDate in new[]
                     {
                         new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1),
                         new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2),
                         new DateOnly(2027, 1, 1),
                     })
            {
                grant.AddTask(Guid.NewGuid(), $"Due {dueDate:yyyy-MM-dd}", dueDate, TaskPriority.Low, applicationDate,
                    new DateTime(2024, 12, 31, 22, 0, 0, DateTimeKind.Utc));
            }

            db.Grants.Add(grant);
            db.SaveChanges();
        }

        using var query = database.CreateContext();

        // Act
        var before = query.GrantTasks.Where(t => t.DueDate < today).Select(t => t.DueDate).ToList();

        // Assert
        Assert.Equal(
            [new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 9, 30)],
            before.Order());
    }
}

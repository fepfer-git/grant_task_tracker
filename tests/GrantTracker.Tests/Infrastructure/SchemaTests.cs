using GrantTracker.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Tests.Infrastructure;

/// <summary>Constraints and column types that SQLite enforces (DESIGN.md §3.1, §3.2, §3.4).</summary>
public class SchemaTests
{
    private const int SqliteConstraintCheck = 275;
    private const int SqliteConstraintUnique = 2067;

    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_TaskRow_When_SettingBothCompletedAndCancelledInSql_Then_CheckConstraintRejectsIt()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using (var db = database.CreateContext())
        {
            var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", new DateOnly(2026, 3, 1));
            grant.AddTask(Guid.NewGuid(), "Task", Today, TaskPriority.Low, Today, NowUtc);
            db.Grants.Add(grant);
            db.SaveChanges();
        }

        // Act: bypass the domain and set both timestamps directly.
        var exception = Assert.Throws<SqliteException>(() => database.Execute(
            "UPDATE GrantTasks SET CompletedAtUtc = '2026-03-10 22:00:00', CancelledAtUtc = '2026-03-10 22:00:00'"));

        // Assert
        Assert.Equal(SqliteConstraintCheck, exception.SqliteExtendedErrorCode);
        Assert.Contains("CK_GrantTask_CompletedXorCancelled", exception.Message);
    }

    [Fact]
    public void Given_ExistingReference_When_SavingAnotherGrantWithSameReference_Then_SaveFails()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using (var db = database.CreateContext())
        {
            db.Grants.Add(new Grant(Guid.NewGuid(), "GR-100001", "First organisation", new DateOnly(2026, 3, 1)));
            db.SaveChanges();
        }

        using var second = database.CreateContext();
        second.Grants.Add(new Grant(Guid.NewGuid(), "GR-100001", "Second organisation", new DateOnly(2026, 3, 1)));

        // Act
        var exception = Assert.Throws<DbUpdateException>(() => second.SaveChanges());

        // Assert
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(SqliteConstraintUnique, sqliteException.SqliteExtendedErrorCode);
        Assert.Contains("Grants.Reference", sqliteException.Message);
    }

    [Fact]
    public void Given_TasksOfEachPriority_When_OrderingByPriorityDescendingInSql_Then_OrderIsHighMediumLow()
    {
        // Arrange: added in an order that is neither sorted nor alphabetical.
        using var database = new SqliteTestDatabase();
        using (var db = database.CreateContext())
        {
            var grant = new Grant(Guid.NewGuid(), "GR-1", "Organisation", new DateOnly(2026, 3, 1));
            grant.AddTask(Guid.NewGuid(), "Medium task", Today, TaskPriority.Medium, Today, NowUtc);
            grant.AddTask(Guid.NewGuid(), "Low task", Today, TaskPriority.Low, Today, NowUtc);
            grant.AddTask(Guid.NewGuid(), "High task", Today, TaskPriority.High, Today, NowUtc);
            db.Grants.Add(grant);
            db.SaveChanges();
        }

        using var query = database.CreateContext();

        // Act
        var priorities = query.GrantTasks.OrderByDescending(t => t.Priority).Select(t => t.Priority).ToList();

        // Assert
        Assert.Equal([TaskPriority.High, TaskPriority.Medium, TaskPriority.Low], priorities);
    }
}

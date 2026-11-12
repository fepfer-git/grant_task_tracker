using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Tests.Infrastructure;

/// <summary>
/// Version renewal (DESIGN.md §3.5) and the races it detects (OP-2). Two contexts on one database play two
/// requests that loaded the same data before either saved.
/// </summary>
public class VersionTests
{
    private static readonly Guid GrantId = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TaskId = new("00000000-0000-0000-0000-0000000000b1");
    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterUtc = new(2026, 3, 10, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_SavedTask_When_ModifiedAndSaved_Then_VersionIsRenewed()
    {
        // Arrange
        using var database = SeedGrantWithPendingTask(TaskPriority.Low);
        using var db = database.CreateContext();
        var task = db.GrantTasks.Include(t => t.Grant).Single(t => t.Id == TaskId);
        var originalVersion = task.Version;
        task.Complete(LaterUtc);

        // Act
        db.SaveChanges();

        // Assert
        using var query = database.CreateContext();
        Assert.NotEqual(originalVersion, query.GrantTasks.Single(t => t.Id == TaskId).Version);
    }

    [Fact]
    public void Given_SavedGrant_When_TaskIsAddedAndSaved_Then_GrantVersionIsRenewed()
    {
        // Arrange
        using var database = SeedGrantWithPendingTask(TaskPriority.Low);
        using var db = database.CreateContext();
        var grant = db.Grants.Include(g => g.Tasks).Single(g => g.Id == GrantId);
        var originalVersion = grant.Version;
        grant.AddTask(Guid.NewGuid(), "Second task", Today, TaskPriority.Low, Today, LaterUtc);

        // Act
        db.SaveChanges();

        // Assert
        using var query = database.CreateContext();
        Assert.NotEqual(originalVersion, query.Grants.Single(g => g.Id == GrantId).Version);
    }

    [Fact]
    public void Given_GrantLoadedInTwoContexts_When_TaskAddedInOneAndGrantClosedInOther_Then_SecondSaveThrowsConcurrencyException()
    {
        // Arrange: both requests see an Open grant with no pending High task.
        using var database = SeedGrantWithPendingTask(TaskPriority.Low);
        using var addRequest = database.CreateContext();
        using var closeRequest = database.CreateContext();
        var grantForAdd = addRequest.Grants.Include(g => g.Tasks).Single(g => g.Id == GrantId);
        var grantForClose = closeRequest.Grants.Include(g => g.Tasks).Single(g => g.Id == GrantId);

        grantForAdd.AddTask(Guid.NewGuid(), "New High task", Today, TaskPriority.High, Today, NowUtc);
        addRequest.SaveChanges();
        grantForClose.Close(note: null, LaterUtc);

        // Act
        var exception = Record.Exception(() => closeRequest.SaveChanges());

        // Assert: the close is refused, so BR-5 can be checked again on retry.
        Assert.IsType<DbUpdateConcurrencyException>(exception);
    }

    [Fact]
    public void Given_TaskLoadedInTwoContexts_When_CompletedInOneAndGrantClosedInOther_Then_SecondSaveThrowsAndTaskIsNeverBoth()
    {
        // Arrange: both requests see the task as pending.
        using var database = SeedGrantWithPendingTask(TaskPriority.Low);
        using var completeRequest = database.CreateContext();
        using var closeRequest = database.CreateContext();
        var taskForComplete = completeRequest.GrantTasks.Include(t => t.Grant).Single(t => t.Id == TaskId);
        var grantForClose = closeRequest.Grants.Include(g => g.Tasks).Single(g => g.Id == GrantId);

        taskForComplete.Complete(NowUtc);
        completeRequest.SaveChanges();
        grantForClose.Close(note: null, LaterUtc);

        // Act
        var exception = Record.Exception(() => closeRequest.SaveChanges());

        // Assert: the close is refused as a whole, and the task stays completed only.
        Assert.IsType<DbUpdateConcurrencyException>(exception);
        using var query = database.CreateContext();
        var task = query.GrantTasks.Single(t => t.Id == TaskId);
        Assert.Equal(NowUtc, task.CompletedAtUtc);
        Assert.Null(task.CancelledAtUtc);
        Assert.Equal(GrantStatus.Open, query.Grants.Single(g => g.Id == GrantId).Status);
    }

    private static SqliteTestDatabase SeedGrantWithPendingTask(TaskPriority priority)
    {
        var database = new SqliteTestDatabase();
        using var db = database.CreateContext();
        var grant = new Grant(GrantId, "GR-1", "Organisation", new DateOnly(2026, 3, 1));
        grant.AddTask(TaskId, "First task", Today, priority, Today, NowUtc);
        db.Grants.Add(grant);
        db.SaveChanges();
        return database;
    }
}

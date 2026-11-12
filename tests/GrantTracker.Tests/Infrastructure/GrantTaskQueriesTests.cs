using GrantTracker.Domain;

namespace GrantTracker.Tests.Infrastructure;

/// <summary>
/// <see cref="GrantTaskQueries"/> translated to SQL gives the same answer as the domain properties
/// <see cref="GrantTask.Status"/> and <see cref="GrantTask.IsOverdue"/> (DESIGN.md §3.3, BR-6).
/// </summary>
public class GrantTaskQueriesTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);

    private static readonly Guid PendingDueYesterday = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PendingDueToday = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid PendingDueTomorrow = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid CompletedDueYesterday = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid CancelledDueYesterday = new("00000000-0000-0000-0000-000000000005");

    [Fact]
    public void Given_TasksInEveryState_When_QueryingWithIsPendingAndIsOverdue_Then_ResultsMatchDomainProperties()
    {
        // Arrange: tasks are created on 2026-03-01, so due dates before today are allowed (BR-2).
        using var database = new SqliteTestDatabase();
        using (var db = database.CreateContext())
        {
            var created = new DateOnly(2026, 3, 1);
            var createdUtc = new DateTime(2026, 2, 28, 22, 0, 0, DateTimeKind.Utc);
            var yesterday = new DateOnly(2026, 3, 10);

            var openGrant = new Grant(Guid.NewGuid(), "GR-1", "Open organisation", created);
            openGrant.AddTask(PendingDueYesterday, "Pending, due yesterday", yesterday, TaskPriority.Low, created, createdUtc);
            openGrant.AddTask(PendingDueToday, "Pending, due today", Today, TaskPriority.Low, created, createdUtc);
            openGrant.AddTask(PendingDueTomorrow, "Pending, due tomorrow", new DateOnly(2026, 3, 12), TaskPriority.Low, created, createdUtc);
            openGrant.AddTask(CompletedDueYesterday, "Completed, due yesterday", yesterday, TaskPriority.Low, created, createdUtc)
                .Complete(new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc));

            var closedGrant = new Grant(Guid.NewGuid(), "GR-2", "Closed organisation", created);
            closedGrant.AddTask(CancelledDueYesterday, "Cancelled, due yesterday", yesterday, TaskPriority.Low, created, createdUtc);
            closedGrant.Close(note: null, new DateTime(2026, 3, 6, 22, 0, 0, DateTimeKind.Utc));

            db.Grants.AddRange(openGrant, closedGrant);
            db.SaveChanges();
        }

        using var query = database.CreateContext();

        // Act
        var pendingInSql = query.GrantTasks.Where(GrantTaskQueries.IsPending).Select(t => t.Id).ToList();
        var overdueInSql = query.GrantTasks.Where(GrantTaskQueries.IsOverdue(Today)).Select(t => t.Id).ToList();
        var allTasks = query.GrantTasks.ToList();
        var pendingInDomain = allTasks.Where(t => t.Status == GrantTaskStatus.Pending).Select(t => t.Id);
        var overdueInDomain = allTasks.Where(t => t.IsOverdue(Today)).Select(t => t.Id);

        // Assert: the expected ids are literals, and SQL and the domain agree with them.
        Assert.Equal([PendingDueYesterday, PendingDueToday, PendingDueTomorrow], pendingInSql.Order());
        Assert.Equal([PendingDueYesterday], overdueInSql);
        Assert.Equal(pendingInSql.Order(), pendingInDomain.Order());
        Assert.Equal(overdueInSql, overdueInDomain);
    }
}

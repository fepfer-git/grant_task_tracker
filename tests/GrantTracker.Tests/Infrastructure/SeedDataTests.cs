using GrantTracker.Domain;
using GrantTracker.Infrastructure;

namespace GrantTracker.Tests.Infrastructure;

/// <summary>
/// The seed (CONTEXT.md §8, DESIGN.md §8) with <c>today</c> = 2026-03-11. Every expected value is a literal.
/// Timestamps are 09:00 Sydney: 22:00 UTC the day before in daylight saving time (AEDT), 23:00 UTC in standard time (AEST).
/// </summary>
public class SeedDataTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    [Fact]
    public async Task Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_GrantsMatchSpec()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();

        // Act
        await SeedData.SeedAsync(db, Today, Sydney, TestContext.Current.CancellationToken);

        // Assert
        using var query = database.CreateContext();
        var grants = query.Grants.OrderBy(g => g.Reference)
            .Select(g => new { g.Id, g.Reference, g.OrganisationName, g.ApplicationDate, g.Status, g.ClosedNote })
            .ToList();
        Assert.Equal(
            [
                new { Id = new Guid("00000000-0000-0000-0000-000000100001"), Reference = "GR-100001", OrganisationName = "Riverside Community Garden Inc.", ApplicationDate = new DateOnly(2026, 1, 10), Status = GrantStatus.Open, ClosedNote = (string?)null },
                new { Id = new Guid("00000000-0000-0000-0000-000000100002"), Reference = "GR-100002", OrganisationName = "Northside Youth Sports Club", ApplicationDate = new DateOnly(2026, 2, 19), Status = GrantStatus.Open, ClosedNote = (string?)null },
                new { Id = new Guid("00000000-0000-0000-0000-000000100003"), Reference = "GR-100003", OrganisationName = "Harbour Arts Collective", ApplicationDate = new DateOnly(2025, 8, 23), Status = GrantStatus.Closed, ClosedNote = (string?)null },
                new { Id = new Guid("00000000-0000-0000-0000-000000100004"), Reference = "GR-100004", OrganisationName = "Hillview Library Friends Group", ApplicationDate = new DateOnly(2026, 3, 6), Status = GrantStatus.Open, ClosedNote = (string?)null },
            ],
            grants);
    }

    [Fact]
    public async Task Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TasksMatchSpec()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();

        // Act
        await SeedData.SeedAsync(db, Today, Sydney, TestContext.Current.CancellationToken);

        // Assert
        using var query = database.CreateContext();
        var tasks = query.GrantTasks.OrderBy(t => t.Id).ToList()
            .Select(t => (t.Id, t.GrantId, t.Title, t.Priority, t.DueDate, t.Status))
            .ToList();
        Assert.Equal(
            [
                (new Guid("00000000-0000-0000-0001-000000000001"), new Guid("00000000-0000-0000-0000-000000100001"), "Verify bank details", TaskPriority.High, new DateOnly(2026, 3, 8), GrantTaskStatus.Pending),
                (new Guid("00000000-0000-0000-0001-000000000002"), new Guid("00000000-0000-0000-0000-000000100001"), "Request quote for tools", TaskPriority.Medium, new DateOnly(2026, 3, 16), GrantTaskStatus.Pending),
                (new Guid("00000000-0000-0000-0001-000000000003"), new Guid("00000000-0000-0000-0000-000000100001"), "Check eligibility documents", TaskPriority.High, new DateOnly(2026, 1, 20), GrantTaskStatus.Completed),
                (new Guid("00000000-0000-0000-0001-000000000004"), new Guid("00000000-0000-0000-0000-000000100002"), "Review progress report", TaskPriority.Low, new DateOnly(2026, 3, 10), GrantTaskStatus.Pending),
                (new Guid("00000000-0000-0000-0001-000000000005"), new Guid("00000000-0000-0000-0000-000000100002"), "Schedule site visit", TaskPriority.Medium, new DateOnly(2026, 3, 21), GrantTaskStatus.Pending),
                (new Guid("00000000-0000-0000-0001-000000000006"), new Guid("00000000-0000-0000-0000-000000100003"), "Final acquittal review", TaskPriority.High, new DateOnly(2025, 10, 12), GrantTaskStatus.Completed),
            ],
            tasks);
    }

    [Fact]
    public async Task Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TimestampsAre0900SydneyOnPreviousUtcDay()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();

        // Act
        await SeedData.SeedAsync(db, Today, Sydney, TestContext.Current.CancellationToken);

        // Assert: stored text, as the database holds it.
        Assert.Equal(
            [
                "GR-100001|",
                "GR-100002|",
                "GR-100003|2025-10-16 22:00:00", // 2025-10-17 09:00 AEDT (today - 145)
                "GR-100004|",
            ],
            database.ReadText("SELECT Reference || '|' || IFNULL(ClosedAtUtc, '') FROM Grants ORDER BY Reference"));
        Assert.Equal(
            [
                "Verify bank details|2026-01-09 22:00:00||",               // created 2026-01-10 09:00 AEDT
                "Request quote for tools|2026-01-09 22:00:00||",
                "Check eligibility documents|2026-01-09 22:00:00|2026-01-17 22:00:00|", // completed 2026-01-18 09:00 AEDT
                "Review progress report|2026-02-18 22:00:00||",            // created 2026-02-19 09:00 AEDT
                "Schedule site visit|2026-02-18 22:00:00||",
                "Final acquittal review|2025-08-22 23:00:00|2025-10-10 22:00:00|", // created 2025-08-23 09:00 AEST, completed 2025-10-11 09:00 AEDT
            ],
            database.ReadText(
                "SELECT Title || '|' || CreatedAtUtc || '|' || IFNULL(CompletedAtUtc, '') || '|' || IFNULL(CancelledAtUtc, '') " +
                "FROM GrantTasks ORDER BY Id"));
    }

    [Fact]
    public async Task Given_SeededDatabase_When_SeedingAgain_Then_NothingIsAdded()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using (var first = database.CreateContext())
        {
            await SeedData.SeedAsync(first, Today, Sydney, TestContext.Current.CancellationToken);
        }

        using var second = database.CreateContext();

        // Act: a later start-up, on a later day.
        await SeedData.SeedAsync(second, new DateOnly(2026, 3, 12), Sydney, TestContext.Current.CancellationToken);

        // Assert
        using var query = database.CreateContext();
        Assert.Equal(4, query.Grants.Count());
        Assert.Equal(6, query.GrantTasks.Count());
        Assert.Equal(new DateOnly(2026, 1, 10), query.Grants.Single(g => g.Reference == "GR-100001").ApplicationDate);
    }

    [Fact]
    public async Task Given_SameToday_When_SeedingTwoDatabases_Then_BothSeedsAreIdentical()
    {
        // Arrange
        using var firstDatabase = new SqliteTestDatabase();
        using var secondDatabase = new SqliteTestDatabase();

        // Act
        using (var db = firstDatabase.CreateContext())
        {
            await SeedData.SeedAsync(db, Today, Sydney, TestContext.Current.CancellationToken);
        }

        using (var db = secondDatabase.CreateContext())
        {
            await SeedData.SeedAsync(db, Today, Sydney, TestContext.Current.CancellationToken);
        }

        // Assert: every stored column, including ids and versions.
        Assert.Equal(DumpAllRows(firstDatabase), DumpAllRows(secondDatabase));
    }

    private static List<string> DumpAllRows(SqliteTestDatabase database) =>
    [
        .. database.ReadText(
            "SELECT Id || '|' || Reference || '|' || OrganisationName || '|' || ApplicationDate || '|' || Status || '|' || " +
            "IFNULL(ClosedNote, '') || '|' || IFNULL(ClosedAtUtc, '') || '|' || Version FROM Grants ORDER BY Id"),
        .. database.ReadText(
            "SELECT Id || '|' || GrantId || '|' || Title || '|' || DueDate || '|' || Priority || '|' || CreatedAtUtc || '|' || " +
            "IFNULL(CompletedAtUtc, '') || '|' || IFNULL(CancelledAtUtc, '') || '|' || Version FROM GrantTasks ORDER BY Id"),
    ];
}

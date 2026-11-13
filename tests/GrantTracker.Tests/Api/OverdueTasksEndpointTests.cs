using System.Net;
using GrantTracker.Domain;
using GrantTracker.Infrastructure;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// <c>GET /tasks/overdue</c> (BR-6): pending tasks due before today in Sydney, most overdue first, then High priority
/// first, then by id. The clock starts at <c>2026-03-10T22:00:00Z</c> = 2026-03-11 in Sydney.
/// </summary>
public class OverdueTasksEndpointTests
{
    // GR-100004 (application date 2026-03-06) has no seeded tasks, so tests add theirs there.
    private static readonly DateOnly Gr100004ApplicationDate = new(2026, 3, 6);
    private static readonly DateTime AddedAtUtc = new(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Given_SeedData_When_ListingOverdueTasks_Then_MostOverdueFirstAndCompletedExcluded()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/tasks/overdue", TestContext.Current.CancellationToken);

        // Assert: the completed tasks due 2026-01-20 and 2025-10-12 aren't listed.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await ReadArray(response);
        Assert.Equal(
            [
                "2026-03-08 Verify bank details (GR-100001, Riverside Community Garden Inc.)",
                "2026-03-10 Review progress report (GR-100002, Northside Youth Sports Club)",
            ],
            tasks.Select(t => $"{t!["dueDate"]} {t["title"]} ({t["grantReference"]}, {t["organisationName"]})"));
    }

    [Fact]
    public async Task Given_TasksWithSameDueDate_When_ListingOverdueTasks_Then_HigherPriorityFirstThenId()
    {
        // Arrange: four tasks due 2026-03-09, added in an order that is neither by priority nor by id.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        factory.UpdateDatabase(db =>
        {
            var grant = GrantTrackerApiFactory.LoadGrant(db, SeedData.Gr100004Id);
            AddTaskDue(grant, "00000000-0000-0000-0000-000000000002", "Low", TaskPriority.Low, new DateOnly(2026, 3, 9));
            AddTaskDue(grant, "00000000-0000-0000-0000-000000000003", "High B", TaskPriority.High, new DateOnly(2026, 3, 9));
            AddTaskDue(grant, "00000000-0000-0000-0000-000000000004", "Medium", TaskPriority.Medium, new DateOnly(2026, 3, 9));
            AddTaskDue(grant, "00000000-0000-0000-0000-000000000001", "High A", TaskPriority.High, new DateOnly(2026, 3, 9));
        });

        // Act
        var response = await client.GetAsync("/tasks/overdue", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await ReadArray(response);
        Assert.Equal(
            [
                "2026-03-08 High Verify bank details",
                "2026-03-09 High High A",
                "2026-03-09 High High B",
                "2026-03-09 Medium Medium",
                "2026-03-09 Low Low",
                "2026-03-10 Low Review progress report",
            ],
            tasks.Select(t => $"{t!["dueDate"]} {t["priority"]} {t["title"]}"));
    }

    [Fact]
    public async Task Given_TaskDueToday_When_ClockPassesSydneyMidnight_Then_TaskAppearsInOverdueList()
    {
        // Arrange: a task due 2026-03-11. 2026-03-11T13:00:00Z is midnight in Sydney (AEDT), when the 12th starts.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        factory.UpdateDatabase(db =>
            AddTaskDue(GrantTrackerApiFactory.LoadGrant(db, SeedData.Gr100004Id), "00000000-0000-0000-0000-000000000001",
                "Due 11 March", TaskPriority.Low, new DateOnly(2026, 3, 11)));

        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 59, 0, TimeSpan.Zero));
        var beforeMidnight = await ReadArray(await client.GetAsync("/tasks/overdue", TestContext.Current.CancellationToken));
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 11, 13, 0, 0, TimeSpan.Zero));

        // Act
        var afterMidnight = await ReadArray(await client.GetAsync("/tasks/overdue", TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain(beforeMidnight, t => (string)t!["title"]! == "Due 11 March");
        Assert.Contains(afterMidnight, t => (string)t!["title"]! == "Due 11 March");
    }

    private static void AddTaskDue(Grant grant, string id, string title, TaskPriority priority, DateOnly dueDate) =>
        grant.AddTask(new Guid(id), title, dueDate, priority, Gr100004ApplicationDate, AddedAtUtc);
}

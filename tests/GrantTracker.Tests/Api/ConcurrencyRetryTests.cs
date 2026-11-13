using System.Net;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// OP-2: the races in DESIGN.md §6.2. In each test, another officer's request saves just before the request under test
/// does (<see cref="CompetingSaveInterceptor"/>). The request's first save then conflicts, the service retries against
/// the new data, and the outcome is the one in the §6.2 race table: success, a no-op, or the rule's own error.
/// </summary>
public class ConcurrencyRetryTests
{
    private const string Gr100002 = "00000000-0000-0000-0000-000000100002";
    private const string Gr100004NoTasks = "00000000-0000-0000-0000-000000100004";
    private const string ReviewProgressReportPendingLow = "00000000-0000-0000-0001-000000000004";
    private const string ScheduleSiteVisitPendingMedium = "00000000-0000-0000-0001-000000000005";
    private const string TaskA = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string TaskB = "bbbbbbbb-0000-0000-0000-000000000001";

    [Fact]
    public async Task Given_TwoOfficersAddingTasksToSameGrant_When_Racing_Then_BothTasksAreSaved()
    {
        // Arrange: the other officer adds task B just before this request saves task A.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask(TaskB, "Task B", "Low"), TestContext.Current.CancellationToken));

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask(TaskA, "Task A", "Low"), TestContext.Current.CancellationToken);

        // Assert: no unnecessary 409. The first save conflicted and was retried.
        Assert.Equal(HttpStatusCode.Created, other?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, factory.Race.RequestSaves);
        Assert.Equal(["Task A", "Task B"], (await TaskTitles(client, Gr100004NoTasks)).Order());
    }

    [Fact]
    public async Task Given_HighTaskAddedFirst_When_CloseRaces_Then_CloseReturns409Br5()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask(TaskB, "Urgent check", "High"), TestContext.Current.CancellationToken));

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", null, TestContext.Current.CancellationToken);

        // Assert: on retry the close sees the pending High task, so BR-5 holds.
        Assert.Equal(HttpStatusCode.Created, other?.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BR-5", (string)(await ReadObject(response))["code"]!);
        Assert.Equal("Open", await GrantStatus(client, Gr100004NoTasks));
    }

    [Fact]
    public async Task Given_GrantClosedFirst_When_AddRaces_Then_AddReturns409Br1()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", null, TestContext.Current.CancellationToken));

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask(TaskA, "Too late", "Low"), TestContext.Current.CancellationToken);

        // Assert: on retry the add sees the grant is closed, so BR-1 holds.
        Assert.Equal(HttpStatusCode.OK, other?.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BR-1", (string)(await ReadObject(response))["code"]!);
        Assert.Empty(await TaskTitles(client, Gr100004NoTasks));
    }

    [Fact]
    public async Task Given_LowTaskAddedFirst_When_CloseRaces_Then_CloseSucceedsAndNewTaskIsCancelled()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask(TaskB, "Minor follow-up", "Low"), TestContext.Current.CancellationToken));

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, other?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("Closed", (string)grant["status"]!);
        var task = Assert.Single(grant["tasks"]!.AsArray())!;
        Assert.Equal("Minor follow-up", (string)task["title"]!);
        Assert.Equal("Cancelled", (string)task["status"]!);
    }

    [Fact]
    public async Task Given_TaskCompletedFirst_When_CloseRaces_Then_CloseSucceedsAndCompletedTaskIsUntouched()
    {
        // Arrange: the other officer completes "Review progress report" at 22:30 UTC, after this request has loaded
        // the grant at 22:00 UTC but before it saves.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () =>
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(30));
            other = await client.PutAsync($"/tasks/{ReviewProgressReportPendingLow}/complete", null, TestContext.Current.CancellationToken);
        });

        // Act
        var response = await client.PutAsync($"/grants/{Gr100002}/close", null, TestContext.Current.CancellationToken);

        // Assert: the completed task keeps its completion; only the other pending task is cancelled.
        Assert.Equal(HttpStatusCode.OK, other?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("Closed", (string)grant["status"]!);
        var tasks = grant["tasks"]!.AsArray();
        var completed = tasks.Single(t => (string)t!["id"]! == ReviewProgressReportPendingLow)!;
        Assert.Equal("Completed", (string)completed["status"]!);
        Assert.Equal("2026-03-10T22:30:00Z", (string)completed["completedAtUtc"]!);
        Assert.Null(completed["cancelledAtUtc"]);
        var cancelled = tasks.Single(t => (string)t!["id"]! == ScheduleSiteVisitPendingMedium)!;
        Assert.Equal("Cancelled", (string)cancelled["status"]!);
    }

    [Fact]
    public async Task Given_GrantClosedFirst_When_CompleteRaces_Then_CompleteReturns409GrantClosed()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PutAsync($"/grants/{Gr100002}/close", null, TestContext.Current.CancellationToken));

        // Act
        var response = await client.PutAsync($"/tasks/{ReviewProgressReportPendingLow}/complete", null, TestContext.Current.CancellationToken);

        // Assert: on retry the task is Cancelled, so it can't be completed; it's never both.
        Assert.Equal(HttpStatusCode.OK, other?.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("grant-closed", (string)(await ReadObject(response))["code"]!);
        var grant = await ReadObject(await client.GetAsync($"/grants/{Gr100002}", TestContext.Current.CancellationToken));
        var task = grant["tasks"]!.AsArray().Single(t => (string)t!["id"]! == ReviewProgressReportPendingLow)!;
        Assert.Equal("Cancelled", (string)task["status"]!);
        Assert.Null(task["completedAtUtc"]);
    }

    [Fact]
    public async Task Given_TaskCompletedFirst_When_SecondCompleteRaces_Then_Returns200WithFirstCompletedAtUtc()
    {
        // Arrange: the other officer completes the task at 22:30 UTC, after this request has loaded it at 22:00 UTC
        // but before it saves.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () =>
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(30));
            other = await client.PutAsync($"/tasks/{ReviewProgressReportPendingLow}/complete", null, TestContext.Current.CancellationToken);
        });

        // Act
        var response = await client.PutAsync($"/tasks/{ReviewProgressReportPendingLow}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, other?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-03-10T22:30:00Z", (string)(await ReadObject(response))["completedAtUtc"]!);
    }

    [Fact]
    public async Task Given_GrantClosedFirst_When_SecondCloseRaces_Then_Returns200AndFirstNoteIsKept()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? other = null;
        factory.Race.Arm(async () => other = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", Body("""{"note":"First officer's note"}"""), TestContext.Current.CancellationToken));

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", Body("""{"note":"Second officer's note"}"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, other?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("First officer's note", (string)(await ReadObject(response))["closedNote"]!);
    }

    [Fact]
    public async Task Given_ConflictOnEveryAttempt_When_ClosingGrant_Then_Returns409ConcurrencyConflictAfterThreeAttempts()
    {
        // Arrange: before every save, another officer adds a Low task, so the grant always changes underneath.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var added = 0;
        factory.Race.Arm(
            async () =>
            {
                added++;
                await client.PostAsync($"/grants/{Gr100004NoTasks}/tasks", NewTask($"cccccccc-0000-0000-0000-00000000000{added}", $"Task {added}", "Low"), TestContext.Current.CancellationToken);
            },
            onEverySave: true);

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/concurrency-conflict", (string)problem["type"]!);
        Assert.Equal("concurrency-conflict", (string)problem["code"]!);
        Assert.Equal("The record was changed by someone else. Reload and try again.", (string)problem["title"]!);
        Assert.Equal(3, factory.Race.RequestSaves);
        Assert.Equal("Open", await GrantStatus(client, Gr100004NoTasks));
    }

    private static StringContent NewTask(string id, string title, string priority) =>
        Body($$"""{"id":"{{id}}","title":"{{title}}","dueDate":"2026-03-20","priority":"{{priority}}"}""");

    private static async Task<string[]> TaskTitles(HttpClient client, string grantId)
    {
        var grant = await ReadObject(await client.GetAsync($"/grants/{grantId}", TestContext.Current.CancellationToken));
        return [.. grant["tasks"]!.AsArray().Select(t => (string)t!["title"]!)];
    }

    private static async Task<string> GrantStatus(HttpClient client, string grantId) =>
        (string)(await ReadObject(await client.GetAsync($"/grants/{grantId}", TestContext.Current.CancellationToken)))["status"]!;
}

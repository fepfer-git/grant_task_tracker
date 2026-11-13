using System.Net;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// OP-3: sending the same create again is safe (DESIGN.md §6.3, §6.5). The client chooses the task id; the server looks
/// the id up before checking any rule. The same task again → 200 with the task as it is now. A different task with
/// the same id → 409 <c>duplicate-task-id</c>. The clock is pinned at <c>2026-03-10T22:00:00Z</c> (Sydney 2026-03-11).
/// </summary>
public class SafeTaskCreationTests
{
    private const string Gr100001 = "00000000-0000-0000-0000-000000100001";
    private const string Gr100004 = "00000000-0000-0000-0000-000000100004";
    private const string TaskId = "dddddddd-0000-0000-0000-000000000001";
    private const string OriginalBody = $$"""{"id":"{{TaskId}}","title":"Send welcome pack","dueDate":"2026-03-20","priority":"Medium"}""";

    [Fact]
    public async Task Given_TaskCreated_When_SameRequestIsSentAgain_Then_Returns200WithSameBodyAndOneTaskExists()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var first = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Act
        var second = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([TaskId], await TaskIds(client, Gr100004));
    }

    [Fact]
    public async Task Given_TaskCreated_When_SameIdSentWithPaddedTaskTitle_Then_Returns200()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Act: the title is compared after trimming, as it was stored.
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks",
            Body($$"""{"id":"{{TaskId}}","title":"  Send welcome pack  ","dueDate":"2026-03-20","priority":"Medium"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Send welcome pack", (string)(await ReadObject(response))["title"]!);
    }

    [Theory]
    [InlineData("Send a different pack", "2026-03-20", "Medium")]
    [InlineData("Send welcome pack", "2026-03-21", "Medium")]
    [InlineData("Send welcome pack", "2026-03-20", "High")]
    public async Task Given_TaskCreated_When_SameIdSentWithDifferentDetails_Then_Returns409DuplicateTaskId(
        string title, string dueDate, string priority)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks",
            Body($$"""{"id":"{{TaskId}}","title":"{{title}}","dueDate":"{{dueDate}}","priority":"{{priority}}"}"""),
            TestContext.Current.CancellationToken);

        // Assert: refused, and the saved task is unchanged.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/duplicate-task-id", (string)problem["type"]!);
        Assert.Equal("duplicate-task-id", (string)problem["code"]!);
        Assert.Equal("This task id is already used by a task with different details", (string)problem["title"]!);
        var task = await SavedTask(client, Gr100004);
        Assert.Equal("Send welcome pack 2026-03-20 Medium", $"{task["title"]} {task["dueDate"]} {task["priority"]}");
    }

    [Fact]
    public async Task Given_TaskCreated_When_SameIdSentToAnotherGrant_Then_Returns409DuplicateTaskId()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("duplicate-task-id", (string)(await ReadObject(response))["code"]!);
        Assert.DoesNotContain(TaskId, await TaskIds(client, Gr100001));
    }

    [Fact]
    public async Task Given_TaskCreatedDueToday_When_RepeatedAfterSydneyMidnight_Then_Returns200NotBr2()
    {
        // Arrange: due 2026-03-11 (today). 2026-03-11T13:00:00Z is midnight in Sydney, so the due date is then in the past.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var body = $$"""{"id":"{{TaskId}}","title":"Phone the applicant","dueDate":"2026-03-11","priority":"Low"}""";
        var first = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(body), TestContext.Current.CancellationToken);
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 11, 13, 0, 0, TimeSpan.Zero));

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(body), TestContext.Current.CancellationToken);

        // Assert: the existing task, as it is now (overdue), not a BR-2 error.
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadObject(response);
        Assert.Equal(TaskId, (string)task["id"]!);
        Assert.True((bool)task["isOverdue"]!);
    }

    [Fact]
    public async Task Given_TaskCreatedThenGrantClosed_When_Repeated_Then_Returns200NotBr1()
    {
        // Arrange: closing the grant cancels the new Medium task.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);
        await client.PutAsync($"/grants/{Gr100004}/close", null, TestContext.Current.CancellationToken);

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", (string)(await ReadObject(response))["status"]!);
    }

    [Fact]
    public async Task Given_TaskCreatedThenCompleted_When_Repeated_Then_Returns200WithCompletedTask()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);
        await client.PutAsync($"/tasks/{TaskId}/complete", null, TestContext.Current.CancellationToken);

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadObject(response);
        Assert.Equal("Completed", (string)task["status"]!);
        Assert.Equal("2026-03-10T22:00:00Z", (string)task["completedAtUtc"]!);
    }

    [Fact]
    public async Task Given_TwoIdenticalCreates_When_Racing_Then_OneTaskExistsAndResponsesAre201And200()
    {
        // Arrange: a copy of the same request saves just before this one does.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        HttpResponseMessage? copy = null;
        factory.Race.Arm(async () => copy = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken));

        // Act
        var response = await client.PostAsync($"/grants/{Gr100004}/tasks", Body(OriginalBody), TestContext.Current.CancellationToken);

        // Assert: the copy created it. This request's one save conflicted; the retry found the task and returned it
        // without saving again.
        Assert.Equal(HttpStatusCode.Created, copy?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Race.RequestSaves);
        Assert.Equal([TaskId], await TaskIds(client, Gr100004));
    }

    private static async Task<string[]> TaskIds(HttpClient client, string grantId)
    {
        var grant = await ReadObject(await client.GetAsync($"/grants/{grantId}", TestContext.Current.CancellationToken));
        return [.. grant["tasks"]!.AsArray().Select(t => (string)t!["id"]!)];
    }

    private static async Task<System.Text.Json.Nodes.JsonObject> SavedTask(HttpClient client, string grantId)
    {
        var grant = await ReadObject(await client.GetAsync($"/grants/{grantId}", TestContext.Current.CancellationToken));
        return grant["tasks"]!.AsArray().Single(t => (string)t!["id"]! == TaskId)!.AsObject();
    }
}

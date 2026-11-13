using System.Net;
using System.Text.Json.Nodes;
using GrantTracker.Domain;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// <c>POST /grants/{id}/tasks</c>: BR-1, BR-2 and BR-3 reach the client with the right status, <c>code</c> and field key
/// (DESIGN.md §5, §5.1, §6.3 step 1). The boundaries are tested in the domain (Part 1); these tests check the API.
/// The clock is pinned at <c>2026-03-10T22:00:00Z</c>, so today in Sydney is 2026-03-11.
/// </summary>
public class CreateTaskEndpointTests
{
    private const string Gr100001 = "00000000-0000-0000-0000-000000100001";
    private const string Gr100003Closed = "00000000-0000-0000-0000-000000100003";
    private const string NewTaskId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Given_OpenGrant_When_PostingValidTask_Then_Returns201WithLocationAndTaskIsSaved()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"  Order seedlings  ","dueDate":"2026-03-20","priority":"Medium"}"""),
            TestContext.Current.CancellationToken);

        // Assert: the task as created, with the title trimmed and the clock's instant.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/grants/{Gr100001}", response.Headers.Location?.OriginalString);
        var task = await ReadObject(response);
        Assert.Equal(NewTaskId, (string)task["id"]!);
        Assert.Equal("Order seedlings", (string)task["title"]!);
        Assert.Equal("2026-03-20", (string)task["dueDate"]!);
        Assert.Equal("Medium", (string)task["priority"]!);
        Assert.Equal("Pending", (string)task["status"]!);
        Assert.Equal("2026-03-10T22:00:00Z", (string)task["createdAtUtc"]!);

        var grant = await ReadObject(await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken));
        Assert.Contains(grant["tasks"]!.AsArray(), t => (string)t!["id"]! == NewTaskId);
    }

    [Fact]
    public async Task Given_ClosedGrant_When_PostingTask_Then_Returns409Br1()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100003Closed}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Late task","dueDate":"2026-03-20","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/BR-1", (string)problem["type"]!);
        Assert.Equal("BR-1", (string)problem["code"]!);
        Assert.Equal("Grant is closed", (string)problem["title"]!);
    }

    [Fact]
    public async Task Given_DueDateIsUtcDateButNotSydneyDate_When_PostingTask_Then_Returns400KeyedOnDueDate()
    {
        // Arrange: 2026-03-10 is today in UTC, but yesterday in Sydney.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Call the treasurer","dueDate":"2026-03-10","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.Equal(["dueDate"], ErrorKeys(problem));
        Assert.Equal(["Due date cannot be in the past."], ErrorMessages(problem, "dueDate"));
    }

    [Fact]
    public async Task Given_DueDateIsSydneyToday_When_PostingTask_Then_Returns201()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Call the treasurer","dueDate":"2026-03-11","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("2026-03-11", (string)(await ReadObject(response))["dueDate"]!);
    }

    [Fact]
    public async Task Given_DueDateBeforeApplicationDate_When_PostingTask_Then_Returns400KeyedOnDueDate()
    {
        // Arrange: a grant whose application date (2026-03-20) is after today, so only that check fails.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var grantId = new Guid("00000000-0000-0000-0000-000000200001");
        factory.UpdateDatabase(db => db.Grants.Add(new Grant(grantId, "GR-200001", "Future organisation", new DateOnly(2026, 3, 20))));

        // Act
        var response = await client.PostAsync($"/grants/{grantId}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Early task","dueDate":"2026-03-19","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal(["dueDate"], ErrorKeys(problem));
        Assert.Equal(["Due date cannot be before the grant's application date."], ErrorMessages(problem, "dueDate"));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    public async Task Given_InvalidTaskTitle_When_PostingTask_Then_Returns400KeyedOnTitle(string title)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"{{title}}","dueDate":"2026-03-20","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.Equal(["title"], ErrorKeys(problem));
    }

    [Fact]
    public async Task Given_InvalidTaskTitleAndInvalidDueDate_When_PostingTask_Then_Returns400WithBothFieldErrors()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"","dueDate":"2026-03-10","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["dueDate", "title"], ErrorKeys(await ReadObject(response)).Order());
    }

    [Theory]
    [InlineData("""{"title":"No id","dueDate":"2026-03-20","priority":"Low"}""")]
    [InlineData("""{"id":"00000000-0000-0000-0000-000000000000","title":"Empty id","dueDate":"2026-03-20","priority":"Low"}""")]
    public async Task Given_MissingOrEmptyId_When_PostingTask_Then_Returns400KeyedOnId(string json)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks", Body(json), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["id"], ErrorKeys(await ReadObject(response)));
    }

    [Theory]
    [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","title":"No due date","priority":"Low"}""", "dueDate", "Due date is required.")]
    [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","title":"Null due date","dueDate":null,"priority":"Low"}""", "dueDate", "Due date is required.")]
    [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","title":"No priority","dueDate":"2026-03-20"}""", "priority", "Priority is required.")]
    [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","title":"Null priority","dueDate":"2026-03-20","priority":null}""", "priority", "Priority is required.")]
    [InlineData("""{"id":"11111111-1111-1111-1111-111111111111","dueDate":"2026-03-20","priority":"Low"}""", "title", "Title is required.")]
    public async Task Given_MissingTaskTitleDueDateOrPriority_When_PostingTask_Then_Returns400KeyedOnField(
        string json, string expectedKey, string expectedMessage)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks", Body(json), TestContext.Current.CancellationToken);

        // Assert: a field error, not a 500, and no task is created.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.Equal([expectedKey], ErrorKeys(problem));
        Assert.Equal([expectedMessage], ErrorMessages(problem, expectedKey));
        var grant = await ReadObject(await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken));
        Assert.DoesNotContain(grant["tasks"]!.AsArray(), t => (string)t!["id"]! == NewTaskId);
    }

    [Theory]
    [InlineData("\"priority\":\"Urgent\",\"dueDate\":\"2026-03-20\"", "priority")]
    [InlineData("\"priority\":3,\"dueDate\":\"2026-03-20\"", "priority")]
    [InlineData("\"priority\":\"Low\",\"dueDate\":\"2026-3-20\"", "dueDate")]
    [InlineData("\"priority\":\"Low\",\"dueDate\":\"20/03/2026\"", "dueDate")]
    [InlineData("\"priority\":\"Low\",\"dueDate\":\"2026-02-30\"", "dueDate")]
    public async Task Given_UnknownPriorityOrMalformedDate_When_PostingTask_Then_Returns400KeyedOnField(string fields, string expectedKey)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/grants/{Gr100001}/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Some task",{{fields}}}"""), TestContext.Current.CancellationToken);

        // Assert: keyed on the field, with a message that names no .NET type.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.Equal([expectedKey], ErrorKeys(problem));
        Assert.Equal(["The value isn't in the expected format."], ErrorMessages(problem, expectedKey));
    }

    [Fact]
    public async Task Given_UnknownGrant_When_PostingTask_Then_Returns404()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/grants/00000000-0000-0000-0000-000000999999/tasks",
            Body($$"""{"id":"{{NewTaskId}}","title":"Some task","dueDate":"2026-03-20","priority":"Low"}"""),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("not-found", (string)problem["code"]!);
        Assert.Equal("Grant not found", (string)problem["title"]!);
    }

    private static string[] ErrorKeys(JsonObject problem) => [.. problem["errors"]!.AsObject().Select(e => e.Key)];

    private static string[] ErrorMessages(JsonObject problem, string key) =>
        [.. problem["errors"]![key]!.AsArray().Select(m => (string)m!)];
}

using System.Net;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// <c>PUT /tasks/{id}/complete</c> and <c>PUT /grants/{id}/close</c> on the seed data: BR-4, BR-5 and the read-only
/// rule for closed grants (DESIGN.md BR-4, BR-5, §4.7, §5.1). The clock is pinned at <c>2026-03-10T22:00:00Z</c>.
/// </summary>
public class CompleteAndCloseEndpointTests
{
    private const string Gr100001 = "00000000-0000-0000-0000-000000100001";
    private const string Gr100002 = "00000000-0000-0000-0000-000000100002";
    private const string Gr100003Closed = "00000000-0000-0000-0000-000000100003";
    private const string Gr100004NoTasks = "00000000-0000-0000-0000-000000100004";
    private const string VerifyBankDetailsPendingHigh = "00000000-0000-0000-0001-000000000001";
    private const string ReviewProgressReportPendingLow = "00000000-0000-0000-0001-000000000004";
    private const string FinalAcquittalReviewCompleted = "00000000-0000-0000-0001-000000000006";

    [Fact]
    public async Task Given_PendingTask_When_Completing_Then_Returns200WithCompletedAtUtcFromClock()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/tasks/{VerifyBankDetailsPendingHigh}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadObject(response);
        Assert.Equal("Completed", (string)task["status"]!);
        Assert.Equal("2026-03-10T22:00:00Z", (string)task["completedAtUtc"]!);
        Assert.False((bool)task["isOverdue"]!);
    }

    [Fact]
    public async Task Given_CompletedTask_When_CompletingAgainAfterClockAdvances_Then_Returns200WithOriginalCompletedAtUtc()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PutAsync($"/tasks/{VerifyBankDetailsPendingHigh}/complete", null, TestContext.Current.CancellationToken);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        // Act
        var response = await client.PutAsync($"/tasks/{VerifyBankDetailsPendingHigh}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-03-10T22:00:00Z", (string)(await ReadObject(response))["completedAtUtc"]!);
    }

    [Fact]
    public async Task Given_CancelledTask_When_Completing_Then_Returns409GrantClosed()
    {
        // Arrange: closing GR-100002 cancels its pending tasks.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        await client.PutAsync($"/grants/{Gr100002}/close", Body("""{"note":"Organisation withdrew"}"""), TestContext.Current.CancellationToken);

        // Act
        var response = await client.PutAsync($"/tasks/{ReviewProgressReportPendingLow}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/grant-closed", (string)problem["type"]!);
        Assert.Equal("grant-closed", (string)problem["code"]!);
        Assert.Equal("Grant is closed; its tasks are read-only", (string)problem["title"]!);
    }

    [Fact]
    public async Task Given_CompletedTaskOnClosedGrant_When_Completing_Then_Returns200AndNothingChanges()
    {
        // Arrange: GR-100003 is closed; its task was completed at 2025-10-11 09:00 Sydney.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/tasks/{FinalAcquittalReviewCompleted}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadObject(response);
        Assert.Equal("Completed", (string)task["status"]!);
        Assert.Equal("2025-10-10T22:00:00Z", (string)task["completedAtUtc"]!);
    }

    [Fact]
    public async Task Given_PendingHighTask_When_ClosingGrant_Then_Returns409Br5WithBlockingTaskIds()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/grants/{Gr100001}/close", Body("""{"note":"Funding withdrawn"}"""), TestContext.Current.CancellationToken);

        // Assert: blocked by "Verify bank details" only; the completed High task doesn't block. Nothing changes.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/BR-5", (string)problem["type"]!);
        Assert.Equal("BR-5", (string)problem["code"]!);
        Assert.Equal("Grant has pending High-priority tasks", (string)problem["title"]!);
        Assert.Equal([VerifyBankDetailsPendingHigh], problem["blockingTaskIds"]!.AsArray().Select(id => (string)id!));
        var grant = await ReadObject(await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken));
        Assert.Equal("Open", (string)grant["status"]!);
    }

    [Fact]
    public async Task Given_PendingLowAndMediumTasks_When_ClosingGrantWithNote_Then_Returns200AndTasksAreCancelled()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/grants/{Gr100002}/close", Body("""{"note":"  Organisation withdrew  "}"""), TestContext.Current.CancellationToken);

        // Assert: both pending tasks are cancelled at the close instant, and none is overdue any more.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("Closed", (string)grant["status"]!);
        Assert.Equal("Organisation withdrew", (string)grant["closedNote"]!);
        Assert.Equal("2026-03-10T22:00:00Z", (string)grant["closedAtUtc"]!);
        Assert.Equal(0, (int)grant["pendingTaskCount"]!);
        Assert.Equal(
            ["Review progress report: Cancelled at 2026-03-10T22:00:00Z, overdue False", "Schedule site visit: Cancelled at 2026-03-10T22:00:00Z, overdue False"],
            grant["tasks"]!.AsArray().Select(t => $"{t!["title"]}: {t["status"]} at {t["cancelledAtUtc"]}, overdue {(bool)t["isOverdue"]!}"));
    }

    [Fact]
    public async Task Given_ClosedGrant_When_ClosingAgain_Then_Returns200AndNothingChanges()
    {
        // Arrange: GR-100003 was closed at 2025-10-17 09:00 Sydney, with no note.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/grants/{Gr100003Closed}/close", Body("""{"note":"Closed again"}"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("Closed", (string)grant["status"]!);
        Assert.Null(grant["closedNote"]);
        Assert.Equal("2025-10-16T22:00:00Z", (string)grant["closedAtUtc"]!);
    }

    [Fact]
    public async Task Given_NoBody_When_ClosingGrantWithoutTasks_Then_Returns200WithNoNote()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("Closed", (string)grant["status"]!);
        Assert.Null(grant["closedNote"]);
    }

    [Fact]
    public async Task Given_NoteOver1000Characters_When_ClosingGrant_Then_Returns400KeyedOnNote()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var note = new string('n', 1001);

        // Act
        var response = await client.PutAsync($"/grants/{Gr100004NoTasks}/close", Body($$"""{"note":"{{note}}"}"""), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.Equal(["Note must be 1000 characters or fewer."], problem["errors"]!["note"]!.AsArray().Select(m => (string)m!));
    }

    [Theory]
    [InlineData("/tasks/00000000-0000-0000-0001-000000999999/complete", "Task not found")]
    [InlineData("/grants/00000000-0000-0000-0000-000000999999/close", "Grant not found")]
    public async Task Given_UnknownTaskOrGrant_When_CompletingOrClosing_Then_Returns404(string url, string expectedTitle)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsync(url, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadObject(response);
        Assert.Equal("not-found", (string)problem["code"]!);
        Assert.Equal(expectedTitle, (string)problem["title"]!);
    }
}

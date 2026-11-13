using System.Net;
using System.Text.Json.Nodes;
using static GrantTracker.Tests.Api.ApiJson;

namespace GrantTracker.Tests.Api;

/// <summary>
/// <c>GET /grants</c> and <c>GET /grants/{id}</c> on the seed data (DESIGN.md §5, §8). The clock is pinned to
/// <c>2026-03-10T22:00:00Z</c> = 2026-03-11 in Sydney, so the seed's dates are literal values.
/// </summary>
public class GrantReadEndpointTests
{
    private const string Gr100001 = "00000000-0000-0000-0000-000000100001";
    private const string Gr100002 = "00000000-0000-0000-0000-000000100002";
    private const string Gr100003 = "00000000-0000-0000-0000-000000100003";

    [Fact]
    public async Task Given_SeedData_When_ListingGrants_Then_ReturnsAllGrantsWithPendingTaskCounts()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/grants", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grants = await ReadArray(response);
        Assert.Equal(
            ["GR-100001: 2", "GR-100002: 2", "GR-100003: 0", "GR-100004: 0"],
            grants.Select(g => $"{g!["reference"]}: {g["pendingTaskCount"]}"));
    }

    [Fact]
    public async Task Given_GrantClosedWithCancelledTasks_When_ListingGrants_Then_PendingTaskCountIsZero()
    {
        // Arrange: closing GR-100002 cancels its two pending tasks. The seed has no cancelled task otherwise.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();
        var close = await client.PutAsync($"/grants/{Gr100002}/close", Body("""{"note":"Organisation withdrew"}"""), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        // Act
        var response = await client.GetAsync("/grants", TestContext.Current.CancellationToken);

        // Assert: cancelled tasks aren't pending (§7).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grants = await ReadArray(response);
        var gr100002 = grants.Single(g => (string)g!["reference"]! == "GR-100002")!;
        Assert.Equal("Closed", (string)gr100002["status"]!);
        Assert.Equal(0, (int)gr100002["pendingTaskCount"]!);
    }

    [Theory]
    [InlineData("Open", new[] { "GR-100001", "GR-100002", "GR-100004" })]
    [InlineData("Closed", new[] { "GR-100003" })]
    public async Task Given_StatusFilter_When_ListingGrants_Then_ReturnsOnlyMatchingGrants(string status, string[] expectedReferences)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/grants?status={status}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grants = await ReadArray(response);
        Assert.Equal(expectedReferences, grants.Select(g => (string)g!["reference"]!));
        Assert.All(grants, g => Assert.Equal(status, (string)g!["status"]!));
    }

    [Fact]
    public async Task Given_InvalidStatus_When_ListingGrants_Then_Returns400ProblemDetails()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/grants?status=Archived", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/validation", (string)problem["type"]!);
        Assert.Equal("validation", (string)problem["code"]!);
        Assert.NotNull(problem["errors"]!["status"]);
    }

    [Fact]
    public async Task Given_PinnedClock_When_GettingGrant_Then_TodayIsSydneyDate()
    {
        // Arrange: 2026-03-10T22:00:00Z is 2026-03-11 09:00 in Sydney.
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal("2026-03-11", (string)grant["today"]!);
    }

    [Fact]
    public async Task Given_SeededGrant_When_GettingIt_Then_DatesAreLiteralSydneyDatesAndInstantsEndWithZ()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var open = await ReadObject(await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken));
        var closed = await ReadObject(await client.GetAsync($"/grants/{Gr100003}", TestContext.Current.CancellationToken));

        // Assert: calendar dates as plain "YYYY-MM-DD"; instants in UTC with "Z".
        Assert.Equal("2026-01-10", (string)open["applicationDate"]!);
        var verifyBankDetails = TaskTitled(open, "Verify bank details");
        Assert.Equal("2026-03-08", (string)verifyBankDetails["dueDate"]!);
        Assert.Equal("2026-01-09T22:00:00Z", (string)verifyBankDetails["createdAtUtc"]!);
        Assert.Equal("2025-10-16T22:00:00Z", (string)closed["closedAtUtc"]!);
    }

    [Fact]
    public async Task Given_ExistingGrant_When_GettingIt_Then_TasksHaveStatusAndIsOverdue()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/grants/{Gr100001}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = await ReadObject(response);
        Assert.Equal(
            [
                "Check eligibility documents: Completed, overdue False",  // due 2026-01-20, completed
                "Verify bank details: Pending, overdue True",              // due 2026-03-08
                "Request quote for tools: Pending, overdue False",         // due 2026-03-16
            ],
            grant["tasks"]!.AsArray().Select(t => $"{t!["title"]}: {t["status"]}, overdue {(bool)t["isOverdue"]!}"));
        Assert.Equal(2, (int)grant["pendingTaskCount"]!);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000999999")]
    [InlineData("not-a-guid")]
    public async Task Given_UnknownOrNonGuidId_When_GettingGrant_Then_Returns404ProblemDetails(string id)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/grants/{id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/not-found", (string)problem["type"]!);
        Assert.Equal("not-found", (string)problem["code"]!);
        Assert.Equal(404, (int)problem["status"]!);
    }

    private static JsonObject TaskTitled(JsonObject grant, string title) =>
        grant["tasks"]!.AsArray().Single(t => (string)t!["title"]! == title)!.AsObject();
}

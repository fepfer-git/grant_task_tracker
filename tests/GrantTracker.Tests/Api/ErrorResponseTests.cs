using System.Net;
using static GrantTracker.Tests.Api.ResponseJson;

namespace GrantTracker.Tests.Api;

/// <summary>Every error is a ProblemDetails body with the same fields, and a 500 reveals nothing internal (DESIGN.md §5.1).</summary>
public class ErrorResponseTests
{
    [Fact]
    public async Task Given_UnhandledException_When_Requesting_Then_Returns500WithoutExceptionDetails()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/test/throw", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Secret internal detail", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain(nameof(ThrowingTestController), body);
        var problem = await ReadObject(response);
        Assert.Equal("https://granttracker.example/problems/internal", (string)problem["type"]!);
        Assert.Equal("internal", (string)problem["code"]!);
        Assert.Equal("An unexpected error occurred.", (string)problem["title"]!);
    }

    [Theory]
    [InlineData("/grants?status=Archived", 400, "/grants", "https://granttracker.example/problems/validation", "validation")]
    [InlineData("/grants/00000000-0000-0000-0000-000000999999", 404, "/grants/00000000-0000-0000-0000-000000999999", "https://granttracker.example/problems/not-found", "not-found")]
    [InlineData("/grants/not-a-guid", 404, "/grants/not-a-guid", "https://granttracker.example/problems/not-found", "not-found")]
    [InlineData("/test/throw", 500, "/test/throw", "https://granttracker.example/problems/internal", "internal")]
    public async Task Given_AnyError_When_Returned_Then_BodyHasTypeCodeTitleStatusDetailInstanceAndTraceId(
        string url, int expectedStatus, string expectedInstance, string expectedType, string expectedCode)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadObject(response);
        Assert.Equal(expectedType, (string?)problem["type"]);
        Assert.Equal(expectedCode, (string?)problem["code"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["title"]), "title");
        Assert.Equal(expectedStatus, (int)problem["status"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["detail"]), "detail");
        Assert.Equal(expectedInstance, (string?)problem["instance"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["traceId"]), "traceId");
    }
}

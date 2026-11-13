using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;

namespace GrantTracker.Tests.Api;

/// <summary>
/// The OpenAPI document and Swagger UI (DESIGN.md §1, §5): served in Development only, listing every endpoint, and
/// describing the JSON the API really sends. The test app runs in Development unless a test says otherwise.
/// </summary>
public class OpenApiTests
{
    [Fact]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_ListsEveryEndpoint()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var document = await GetDocument(client);

        // Assert
        var endpoints = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(operation => $"{operation.Key.ToUpperInvariant()} {path.Key}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "GET /grants",
                "GET /grants/{id}",
                "GET /tasks/overdue",
                "POST /grants/{id}/tasks",
                "PUT /grants/{id}/close",
                "PUT /tasks/{id}/complete",
            ],
            endpoints);
    }

    /// <summary>
    /// Every environment other than Development, not only Production. A check such as <c>!IsProduction()</c> would
    /// otherwise pass while exposing the API's description on Staging or a custom environment.
    /// </summary>
    [Theory]
    [InlineData("Production", "/openapi/v1.json")]
    [InlineData("Production", "/swagger")]
    [InlineData("Production", "/swagger/index.html")]
    [InlineData("Staging", "/openapi/v1.json")]
    [InlineData("Staging", "/swagger")]
    [InlineData("Staging", "/swagger/index.html")]
    [InlineData("Uat", "/openapi/v1.json")]
    [InlineData("Uat", "/swagger")]
    [InlineData("Uat", "/swagger/index.html")]
    public async Task Given_NonDevelopmentEnvironment_When_GettingOpenApiDocumentOrSwagger_Then_Returns404(
        string environment, string path)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        using var nonDevelopment = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        var client = nonDevelopment.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Given_DevelopmentEnvironment_When_GettingSwaggerUi_Then_ReturnsThePage()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("GrantStatus", """["Open","Closed"]""")]
    [InlineData("GrantTaskStatus", """["Pending","Completed","Cancelled"]""")]
    [InlineData("TaskPriority", """["Low","Medium","High"]""")]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_EnumIsDescribedByName(
        string schema, string expectedValues)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var document = await GetDocument(client);

        // Assert
        var values = document["components"]!["schemas"]![schema]!["enum"];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedValues), values), $"{schema} enum was {values?.ToJsonString()}");
    }

    [Theory]
    [InlineData("TaskDto", "dueDate", """{"type":"string","format":"date"}""")]
    [InlineData("CreateTaskRequest", "dueDate", """{"type":["null","string"],"format":"date"}""")]
    [InlineData("GrantDto", "applicationDate", """{"type":"string","format":"date"}""")]
    [InlineData("GrantDto", "today", """{"type":"string","format":"date"}""")]
    [InlineData("GrantDto", "pendingTaskCount", """{"type":"integer","format":"int32"}""")]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_PropertyIsDescribedAsSent(
        string schema, string property, string expectedSchema)
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var document = await GetDocument(client);

        // Assert
        var actual = document["components"]!["schemas"]![schema]!["properties"]![property];
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expectedSchema), actual),
            $"{schema}.{property} was {actual?.ToJsonString()}");
    }

    [Fact]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_CloseNoteIsOptional()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var document = await GetDocument(client);

        // Assert
        var closeRequest = document["components"]!["schemas"]!["CloseGrantRequest"]!;
        Assert.NotNull(closeRequest["properties"]!["note"]);
        var required = closeRequest["required"]?.AsArray().Select(name => name!.GetValue<string>()) ?? [];
        Assert.DoesNotContain("note", required);
    }

    [Fact]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_AddTaskListsCreatedAndOk()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var document = await GetDocument(client);

        // Assert: 201 for a new task, 200 for the same create sent again (§6.3).
        var responses = document["paths"]!["/grants/{id}/tasks"]!["post"]!["responses"]!.AsObject().Select(r => r.Key).Order();
        Assert.Equal(["200", "201"], responses);
    }

    private static async Task<JsonObject> GetDocument(HttpClient client)
    {
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiJson.ReadObject(response);
    }
}

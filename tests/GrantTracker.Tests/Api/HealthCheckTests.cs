using System.Net;
using GrantTracker.Api.Startup;
using GrantTracker.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrantTracker.Tests.Api;

/// <summary>
/// OP-5 (DESIGN.md §6.4): liveness answers while the process runs; readiness also needs the database. The body is the
/// status word only.
/// </summary>
public class HealthCheckTests
{
    [Fact]
    public async Task Given_RunningApp_When_CallingLive_Then_Returns200Healthy()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ReachableDatabase_When_CallingReady_Then_Returns200Healthy()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_UnreachableDatabase_When_CallingReady_Then_Returns503Unhealthy()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        using var unreachable = WithUnreachableDatabase(factory, out _);
        var client = unreachable.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_UnreachableDatabase_When_CallingLive_Then_StillReturns200()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        using var unreachable = WithUnreachableDatabase(factory, out _);
        var client = unreachable.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_UnreachableDatabase_When_CallingReady_Then_BodyHasNoExceptionDetails()
    {
        // Arrange
        using var factory = new GrantTrackerApiFactory();
        using var unreachable = WithUnreachableDatabase(factory, out var missingFolder);
        var client = unreachable.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        // Assert: the status word and nothing else; in particular not the check's name or the database path.
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain(missingFolder, body);
        Assert.DoesNotContain(nameof(AppDbContext), body);
    }

    /// <summary>
    /// The same app with its database file in a folder that doesn't exist, so the database can't be reached. The
    /// start-up migration is removed, because it would stop the app before the health endpoints could answer.
    /// </summary>
    private static WebApplicationFactory<Program> WithUnreachableDatabase(GrantTrackerApiFactory factory, out string missingFolder)
    {
        var folder = $"granttracker-missing-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), folder, "granttracker.db")}";
        missingFolder = folder;

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(s => s.ImplementationType == typeof(DatabaseInitializer)));
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        }));
    }
}

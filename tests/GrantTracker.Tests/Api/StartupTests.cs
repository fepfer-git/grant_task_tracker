using GrantTracker.Api.Startup;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrantTracker.Tests.Api;

/// <summary>Start-up checks (DESIGN.md §4.0): a bad setting stops the app before it serves any request.</summary>
public class StartupTests
{
    [Fact]
    public void Given_InvalidTimeZoneId_When_AppStarts_Then_StartupFails()
    {
        // Arrange: the database start-up is removed, because it reads the same setting and would also fail.
        // This way only the start-up options check can stop the app.
        using var factory = new GrantTrackerApiFactory(new Dictionary<string, string?> { ["App:TimeZoneId"] = "Nowhere/Nope" });
        using var withoutDatabaseStartup = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Remove(services.Single(s => s.ImplementationType == typeof(DatabaseInitializer)))));

        // Act
        var exception = Record.Exception(() => withoutDatabaseStartup.CreateClient());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains("App:TimeZoneId is not a known time zone id.", validation.Message);
    }
}

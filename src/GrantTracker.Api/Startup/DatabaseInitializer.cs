using GrantTracker.Api.Time;
using GrantTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrantTracker.Api.Startup;

/// <summary>
/// Applies the migrations, then seeds an empty database with <c>today</c> in the program time zone (DESIGN.md §6.2, §8).
/// It runs as the app starts. If the database can't be reached, the exception stops the app from starting.
/// </summary>
public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<IAppClock>();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(services.GetRequiredService<IOptions<AppOptions>>().Value.TimeZoneId);

        await db.Database.MigrateAsync(cancellationToken);
        await SeedData.SeedAsync(db, clock.Today, timeZone, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

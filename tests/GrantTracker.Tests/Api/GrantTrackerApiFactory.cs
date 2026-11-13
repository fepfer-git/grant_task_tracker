using System.Text;
using System.Text.Json.Nodes;
using GrantTracker.Domain;
using GrantTracker.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace GrantTracker.Tests.Api;

/// <summary>
/// The real app for one test (DESIGN.md §9): its own in-memory SQLite database, migrated and seeded by the app's
/// own start-up, and a fake clock pinned to <c>2026-03-10T22:00:00Z</c>, which is 2026-03-11 09:00 in Sydney.
/// The UTC date and the Sydney date differ at that instant, so code that used the UTC date fails the tests.
/// </summary>
public sealed class GrantTrackerApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset PinnedInstant = new(2026, 3, 10, 22, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly IReadOnlyDictionary<string, string?> _settings;

    /// <param name="settings">Configuration values to override, for example <c>App:TimeZoneId</c>.</param>
    public GrantTrackerApiFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        _settings = settings ?? new Dictionary<string, string?>();
        _connection.Open();
    }

    /// <summary>The app's clock. Advance it to move time forward.</summary>
    public FakeTimeProvider Clock { get; } = new(PinnedInstant);

    /// <summary>Added to every DbContext the app creates. Arm it to make a race happen (OP-2 tests).</summary>
    public CompetingSaveInterceptor Race { get; } = new();

    /// <summary>
    /// Changes data directly through its own DbContext: for states no endpoint creates, and for the competing change
    /// in a race.
    /// </summary>
    public void UpdateDatabase(Action<AppDbContext> change)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        db.SaveChanges();
    }

    /// <summary>Loads a grant with its tasks, for use inside <see cref="UpdateDatabase"/>.</summary>
    public static Grant LoadGrant(AppDbContext db, Guid id) => db.Grants.Include(g => g.Tasks).Single(g => g.Id == id);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection).AddInterceptors(Race));

            // Adds the test-only controllers, such as the endpoint that throws.
            services.AddControllers().AddApplicationPart(typeof(GrantTrackerApiFactory).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

/// <summary>
/// JSON in and out of the API. Bodies are written and read as text, so tests control and assert the exact JSON
/// the client sends and receives.
/// </summary>
public static class ApiJson
{
    public static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    public static async Task<JsonObject> ReadObject(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();

    public static async Task<JsonArray> ReadArray(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsArray();
}

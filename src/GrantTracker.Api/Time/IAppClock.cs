namespace GrantTracker.Api.Time;

/// <summary>
/// The only source of "now" and "today" for the application services (DESIGN.md §2, §4.0).
/// Business logic receives these values as parameters and never reads a clock itself (BR-6).
/// </summary>
public interface IAppClock
{
    /// <summary>The current instant, with <see cref="DateTimeKind.Utc"/>.</summary>
    DateTime UtcNow { get; }

    /// <summary>The current date in the program time zone (<see cref="AppOptions.TimeZoneId"/>).</summary>
    DateOnly Today { get; }
}

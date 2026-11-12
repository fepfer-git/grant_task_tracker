using Microsoft.Extensions.Options;

namespace GrantTracker.Api.Time;

/// <summary>
/// Reads the instant from <see cref="TimeProvider"/> and converts it to the program time zone. This is the only
/// place that turns UTC into a local date (DESIGN.md §2). It never uses the machine's time zone.
/// </summary>
public sealed class AppClock : IAppClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public AppClock(TimeProvider timeProvider, IOptions<AppOptions> options)
    {
        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);
}

namespace GrantTracker.Api.Time;

/// <summary>The <c>App</c> configuration section (DESIGN.md §4.0).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// The program time zone, as an IANA id. "Today" for BR-2 and BR-6 is the current date in this zone.
    /// .NET accepts IANA ids on Windows too.
    /// </summary>
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    /// <summary>True when <see cref="TimeZoneId"/> names a time zone this machine knows.</summary>
    public bool HasKnownTimeZone() => TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _);
}

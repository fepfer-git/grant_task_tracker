using System.Globalization;
using GrantTracker.Api.Time;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrantTracker.Tests.Api;

/// <summary>
/// <see cref="AppClock"/>: "today" is the date in Australia/Sydney, whatever the UTC date or the machine's
/// time zone (DESIGN.md §4.0, §7). Sydney is UTC+11 (AEDT) until 5 April 2026 03:00 and from
/// 4 October 2026 02:00, and UTC+10 (AEST) in between. Every instant and date is a literal.
/// </summary>
public class AppClockTests
{
    [Fact]
    public void Given_UtcInstantOnPreviousUtcDate_When_ReadingToday_Then_ReturnsSydneyDate()
    {
        // Arrange: 2026-03-10 22:00 UTC is 2026-03-11 09:00 in Sydney (AEDT).
        var clock = SydneyClock(new DateTimeOffset(2026, 3, 10, 22, 0, 0, TimeSpan.Zero));

        // Act
        var today = clock.Today;

        // Assert
        Assert.Equal(new DateOnly(2026, 3, 11), today);
    }

    [Fact]
    public void Given_OneMinuteBeforeSydneyMidnight_When_ReadingToday_Then_ReturnsSameDay()
    {
        // Arrange: 2026-03-10 12:59 UTC is 2026-03-10 23:59 in Sydney (AEDT).
        var clock = SydneyClock(new DateTimeOffset(2026, 3, 10, 12, 59, 0, TimeSpan.Zero));

        // Act
        var today = clock.Today;

        // Assert
        Assert.Equal(new DateOnly(2026, 3, 10), today);
    }

    [Fact]
    public void Given_SydneyMidnight_When_ReadingToday_Then_ReturnsNextDay()
    {
        // Arrange: 2026-03-10 13:00 UTC is 2026-03-11 00:00 in Sydney (AEDT).
        var clock = SydneyClock(new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.Zero));

        // Act
        var today = clock.Today;

        // Assert
        Assert.Equal(new DateOnly(2026, 3, 11), today);
    }

    [Theory]
    // Daylight saving ended at 03:00 on 5 April 2026, so that night's midnight is at UTC+10 (AEST).
    [InlineData("2026-04-05T13:59:00Z", "2026-04-05")]
    [InlineData("2026-04-05T14:00:00Z", "2026-04-06")]
    public void Given_DaylightSavingEnds_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight(string utcInstant, string expectedToday)
    {
        // Arrange
        var clock = SydneyClock(DateTimeOffset.Parse(utcInstant, CultureInfo.InvariantCulture));

        // Act
        var today = clock.Today;

        // Assert
        Assert.Equal(DateOnly.ParseExact(expectedToday, "yyyy-MM-dd", CultureInfo.InvariantCulture), today);
    }

    [Theory]
    // Daylight saving started at 02:00 on 4 October 2026, so that night's midnight is at UTC+11 (AEDT).
    [InlineData("2026-10-04T12:59:00Z", "2026-10-04")]
    [InlineData("2026-10-04T13:00:00Z", "2026-10-05")]
    public void Given_DaylightSavingStarts_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight(string utcInstant, string expectedToday)
    {
        // Arrange
        var clock = SydneyClock(DateTimeOffset.Parse(utcInstant, CultureInfo.InvariantCulture));

        // Act
        var today = clock.Today;

        // Assert
        Assert.Equal(DateOnly.ParseExact(expectedToday, "yyyy-MM-dd", CultureInfo.InvariantCulture), today);
    }

    [Fact]
    public void Given_FakeClock_When_ReadingUtcNow_Then_ReturnsFakeInstantMarkedUtc()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 10, 22, 0, 0, TimeSpan.Zero));
        var clock = new AppClock(timeProvider, Options.Create(new AppOptions { TimeZoneId = "Australia/Sydney" }));
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        var utcNow = clock.UtcNow;

        // Assert: the advanced fake instant, read when asked for, and marked as UTC.
        Assert.Equal(new DateTime(2026, 3, 10, 22, 5, 0), utcNow);
        Assert.Equal(DateTimeKind.Utc, utcNow.Kind);
    }

    private static AppClock SydneyClock(DateTimeOffset utcInstant) =>
        new(new FakeTimeProvider(utcInstant), Options.Create(new AppOptions { TimeZoneId = "Australia/Sydney" }));
}

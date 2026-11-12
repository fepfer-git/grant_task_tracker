using GrantTracker.Domain;

namespace GrantTracker.Tests.Domain;

/// <summary><see cref="GrantTask.Status"/> is derived from the two timestamps (DESIGN.md §3.3).</summary>
public class GrantTaskStatusTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);

    public enum TimestampSet
    {
        Neither,
        CompletedAtUtc,
        CancelledAtUtc,
    }

    [Theory]
    [InlineData(TimestampSet.Neither, GrantTaskStatus.Pending)]
    [InlineData(TimestampSet.CompletedAtUtc, GrantTaskStatus.Completed)]
    [InlineData(TimestampSet.CancelledAtUtc, GrantTaskStatus.Cancelled)]
    public void Given_TaskTimestamps_When_ReadingStatus_Then_StatusMatchesTimestamps(TimestampSet timestampSet, GrantTaskStatus expected)
    {
        // Arrange
        var grant = new Grant(Guid.NewGuid(), "GR-100001", "Riverside Community Garden Inc.", new DateOnly(2026, 1, 10));
        var task = grant.AddTask(Guid.NewGuid(), "Request quote for tools", new DateOnly(2026, 3, 16), TaskPriority.Medium, Today, NowUtc);
        if (timestampSet == TimestampSet.CompletedAtUtc)
        {
            task.Complete(NowUtc);
        }
        else if (timestampSet == TimestampSet.CancelledAtUtc)
        {
            grant.Close(note: null, NowUtc);
        }

        // Act
        var status = task.Status;

        // Assert
        Assert.Equal(expected, status);
    }
}

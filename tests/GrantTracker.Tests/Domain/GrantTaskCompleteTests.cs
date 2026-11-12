using GrantTracker.Domain;

namespace GrantTracker.Tests.Domain;

/// <summary>
/// <see cref="GrantTask.Complete"/>: BR-4 and the read-only rule for closed grants (DESIGN.md BR-4 table, §4.7).
/// Every date and instant is a literal.
/// </summary>
public class GrantTaskCompleteTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);

    // 2026-03-11 09:00 in Sydney.
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);

    // 2026-03-06 09:00 in Sydney: an earlier completion.
    private static readonly DateTime EarlierUtc = new(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_PendingTaskOnOpenGrant_When_Completing_Then_CompletedAtUtcIsNow()
    {
        // Arrange
        var task = PendingTask(OpenGrant());

        // Act
        task.Complete(NowUtc);

        // Assert
        Assert.Equal(new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc), task.CompletedAtUtc);
        Assert.Equal(GrantTaskStatus.Completed, task.Status);
    }

    [Fact]
    public void Given_CompletedTask_When_CompletingAgainLater_Then_CompletedAtUtcIsUnchanged()
    {
        // Arrange
        var task = PendingTask(OpenGrant());
        task.Complete(EarlierUtc);

        // Act
        task.Complete(NowUtc);

        // Assert
        Assert.Equal(new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc), task.CompletedAtUtc);
    }

    [Fact]
    public void Given_CompletedTaskOnClosedGrant_When_Completing_Then_NothingChangesAndNoError()
    {
        // Arrange
        var grant = OpenGrant();
        var task = PendingTask(grant);
        task.Complete(EarlierUtc);
        grant.Close(note: null, EarlierUtc.AddHours(1));

        // Act
        var exception = Record.Exception(() => task.Complete(NowUtc));

        // Assert
        Assert.Null(exception);
        Assert.Equal(new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc), task.CompletedAtUtc);
        Assert.Null(task.CancelledAtUtc);
    }

    [Fact]
    public void Given_CancelledTask_When_Completing_Then_ThrowsGrantClosed()
    {
        // Arrange: closing the grant cancels its pending task.
        var grant = OpenGrant();
        var task = PendingTask(grant);
        grant.Close(note: null, EarlierUtc);

        // Act
        var exception = Assert.Throws<BusinessRuleException>(() => task.Complete(NowUtc));

        // Assert
        Assert.Equal("grant-closed", exception.Code);
        Assert.Null(task.CompletedAtUtc);
        Assert.Equal(GrantTaskStatus.Cancelled, task.Status);
    }

    private static Grant OpenGrant() =>
        new(new Guid("00000000-0000-0000-0000-000000100001"), "GR-100001", "Riverside Community Garden Inc.", new DateOnly(2026, 1, 10));

    private static GrantTask PendingTask(Grant grant) =>
        grant.AddTask(Guid.NewGuid(), "Request quote for tools", new DateOnly(2026, 3, 16), TaskPriority.Medium, new DateOnly(2026, 3, 1), new DateTime(2026, 2, 28, 22, 0, 0, DateTimeKind.Utc));
}

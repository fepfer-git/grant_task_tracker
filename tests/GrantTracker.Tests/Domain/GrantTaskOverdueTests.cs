using GrantTracker.Domain;

namespace GrantTracker.Tests.Domain;

/// <summary>
/// <see cref="GrantTask.IsOverdue"/>: BR-6. "Today" is passed in, never read from a clock.
/// Every date and instant is a literal.
/// </summary>
public class GrantTaskOverdueTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);

    // Tasks are added on 2026-03-01 (Sydney), so a due date of 2026-03-10 was valid when created.
    private static readonly DateOnly AddedOn = new(2026, 3, 1);
    private static readonly DateTime AddedAtUtc = new(2026, 2, 28, 22, 0, 0, DateTimeKind.Utc);

    // 2026-03-06 09:00 in Sydney.
    private static readonly DateTime EarlierUtc = new(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc);

    public enum FinalState
    {
        Completed,
        Cancelled,
    }

    [Fact]
    public void Given_PendingTaskDueYesterday_When_CheckingOverdue_Then_IsOverdue()
    {
        // Arrange
        var task = AddTask(OpenGrant(), dueDate: new DateOnly(2026, 3, 10));

        // Act
        var isOverdue = task.IsOverdue(Today);

        // Assert
        Assert.True(isOverdue);
    }

    [Fact]
    public void Given_PendingTaskDueToday_When_CheckingOverdue_Then_IsNotOverdue()
    {
        // Arrange
        var task = AddTask(OpenGrant(), dueDate: new DateOnly(2026, 3, 11));

        // Act
        var isOverdue = task.IsOverdue(Today);

        // Assert
        Assert.False(isOverdue);
    }

    [Theory]
    [InlineData(FinalState.Completed)]
    [InlineData(FinalState.Cancelled)]
    public void Given_CompletedOrCancelledTaskPastDue_When_CheckingOverdue_Then_IsNotOverdue(FinalState finalState)
    {
        // Arrange
        var grant = OpenGrant();
        var task = AddTask(grant, dueDate: new DateOnly(2026, 3, 10));
        if (finalState == FinalState.Completed)
        {
            task.Complete(EarlierUtc);
        }
        else
        {
            grant.Close(note: null, EarlierUtc);
        }

        // Act
        var isOverdue = task.IsOverdue(Today);

        // Assert
        Assert.False(isOverdue);
    }

    private static Grant OpenGrant() =>
        new(new Guid("00000000-0000-0000-0000-000000100002"), "GR-100002", "Northside Youth Sports Club", new DateOnly(2026, 2, 19));

    private static GrantTask AddTask(Grant grant, DateOnly dueDate) =>
        grant.AddTask(Guid.NewGuid(), "Review progress report", dueDate, TaskPriority.Low, AddedOn, AddedAtUtc);
}

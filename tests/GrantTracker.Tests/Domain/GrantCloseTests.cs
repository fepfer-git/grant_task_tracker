using GrantTracker.Domain;

namespace GrantTracker.Tests.Domain;

/// <summary>
/// <see cref="Grant.Close"/>: BR-5, cancelling pending tasks, the note, and repeating a close
/// (DESIGN.md BR-5, §4.7, §7). Every date and instant is a literal.
/// </summary>
public class GrantCloseTests
{
    // Tasks are added on 2026-03-01 (Sydney), so due dates up to then are valid when created.
    private static readonly DateOnly AddedOn = new(2026, 3, 1);
    private static readonly DateTime AddedAtUtc = new(2026, 2, 28, 22, 0, 0, DateTimeKind.Utc);

    // 2026-03-06 09:00 in Sydney.
    private static readonly DateTime CompletedAtUtc = new(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc);

    // 2026-03-11 09:00 in Sydney.
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_PendingHighTask_When_Closing_Then_ThrowsBr5WithBlockingTaskIds()
    {
        // Arrange
        var grant = OpenGrant();
        var highTask = AddTask(grant, TaskPriority.High);
        var lowTask = AddTask(grant, TaskPriority.Low);

        // Act
        var exception = Assert.Throws<BusinessRuleException>(() => grant.Close("Funding withdrawn", NowUtc));

        // Assert
        Assert.Equal("BR-5", exception.Code);
        Assert.Equal([highTask.Id], Assert.IsType<Guid[]>(exception.Extensions["blockingTaskIds"]));
        Assert.Equal(GrantStatus.Open, grant.Status);
        Assert.Null(grant.ClosedAtUtc);
        Assert.Null(grant.ClosedNote);
        Assert.Equal(GrantTaskStatus.Pending, lowTask.Status);
    }

    [Fact]
    public void Given_OnlyCompletedHighTask_When_Closing_Then_GrantIsClosed()
    {
        // Arrange
        var grant = OpenGrant();
        AddTask(grant, TaskPriority.High).Complete(CompletedAtUtc);

        // Act
        grant.Close(note: null, NowUtc);

        // Assert
        Assert.Equal(GrantStatus.Closed, grant.Status);
    }

    [Fact]
    public void Given_PendingLowAndMediumTasks_When_Closing_Then_TheyAreCancelledAtClosedAtUtc()
    {
        // Arrange
        var grant = OpenGrant();
        var lowTask = AddTask(grant, TaskPriority.Low);
        var mediumTask = AddTask(grant, TaskPriority.Medium);
        var completedTask = AddTask(grant, TaskPriority.High);
        completedTask.Complete(CompletedAtUtc);

        // Act
        grant.Close(note: null, NowUtc);

        // Assert
        var expected = new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, grant.ClosedAtUtc);
        Assert.Equal(expected, lowTask.CancelledAtUtc);
        Assert.Equal(expected, mediumTask.CancelledAtUtc);
        Assert.Equal(new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc), completedTask.CompletedAtUtc);
        Assert.Null(completedTask.CancelledAtUtc);
    }

    [Fact]
    public void Given_NoTasks_When_Closing_Then_GrantIsClosed()
    {
        // Arrange
        var grant = OpenGrant();

        // Act
        grant.Close(note: null, NowUtc);

        // Assert
        Assert.Equal(GrantStatus.Closed, grant.Status);
        Assert.Equal(new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc), grant.ClosedAtUtc);
    }

    [Theory]
    [InlineData("  Duplicate application  ", "Duplicate application")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Given_NoteWithSpacesOrBlank_When_Closing_Then_NoteIsTrimmedOrNull(string? note, string? expected)
    {
        // Arrange
        var grant = OpenGrant();

        // Act
        grant.Close(note, NowUtc);

        // Assert
        Assert.Equal(expected, grant.ClosedNote);
    }

    [Fact]
    public void Given_ClosedGrant_When_ClosingAgain_Then_NoteClosedAtAndTasksAreUnchanged()
    {
        // Arrange: the first close cancels the pending task.
        var grant = OpenGrant();
        var task = AddTask(grant, TaskPriority.Low);
        grant.Close("Funding withdrawn", CompletedAtUtc);

        // Act
        grant.Close("Closed again", NowUtc);

        // Assert
        var firstClose = new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Funding withdrawn", grant.ClosedNote);
        Assert.Equal(firstClose, grant.ClosedAtUtc);
        Assert.Equal(firstClose, task.CancelledAtUtc);
    }

    [Fact]
    public void Given_NoteOf1000CharactersAfterTrimming_When_Closing_Then_GrantIsClosedWithNote()
    {
        // Arrange
        var grant = OpenGrant();
        var note = "  " + new string('n', 1000) + "  ";

        // Act
        grant.Close(note, NowUtc);

        // Assert
        Assert.Equal(GrantStatus.Closed, grant.Status);
        Assert.Equal(1000, grant.ClosedNote!.Length);
    }

    [Fact]
    public void Given_NoteOf1001CharactersAfterTrimming_When_Closing_Then_ThrowsValidationOnNoteAndNothingChanges()
    {
        // Arrange
        var grant = OpenGrant();
        var task = AddTask(grant, TaskPriority.Low);
        var note = "  " + new string('n', 1001) + "  ";

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => grant.Close(note, NowUtc));

        // Assert
        Assert.Equal(["note"], exception.Errors.Keys);
        Assert.Equal(GrantStatus.Open, grant.Status);
        Assert.Null(grant.ClosedAtUtc);
        Assert.Null(grant.ClosedNote);
        Assert.Equal(GrantTaskStatus.Pending, task.Status);
    }

    [Fact]
    public void Given_PendingHighTaskAndTooLongNote_When_Closing_Then_ThrowsBr5()
    {
        // Arrange
        var grant = OpenGrant();
        AddTask(grant, TaskPriority.High);

        // Act
        var exception = Record.Exception(() => grant.Close(new string('n', 1001), NowUtc));

        // Assert: BR-5 is checked first, so the note doesn't matter.
        Assert.Equal("BR-5", Assert.IsType<BusinessRuleException>(exception).Code);
    }

    private static Grant OpenGrant() =>
        new(new Guid("00000000-0000-0000-0000-000000100001"), "GR-100001", "Riverside Community Garden Inc.", new DateOnly(2026, 1, 10));

    private static GrantTask AddTask(Grant grant, TaskPriority priority) =>
        grant.AddTask(Guid.NewGuid(), $"{priority} task", new DateOnly(2026, 3, 16), priority, AddedOn, AddedAtUtc);
}

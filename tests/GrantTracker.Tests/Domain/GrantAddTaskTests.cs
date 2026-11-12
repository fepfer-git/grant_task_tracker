using GrantTracker.Domain;

namespace GrantTracker.Tests.Domain;

/// <summary>
/// <see cref="Grant.AddTask"/>: BR-1, BR-2, BR-3 and the order of checks (DESIGN.md §4, §7).
/// Every date and instant is a literal, so a bug in the date logic can't shift the expected value too.
/// </summary>
public class GrantAddTaskTests
{
    // "Today" in Sydney, and the matching instant (2026-03-11 09:00 in Sydney is still 2026-03-10 in UTC).
    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly DateTime NowUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);

    private const string ValidTaskTitle = "Verify bank details";

    [Fact]
    public void Given_OpenGrantAndValidInput_When_AddingTask_Then_TaskIsPendingWithCreatedAtUtcSet()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));
        var taskId = new Guid("00000000-0000-0000-0001-000000000001");

        // Act
        var task = grant.AddTask(taskId, ValidTaskTitle, new DateOnly(2026, 3, 16), TaskPriority.High, Today, NowUtc);

        // Assert
        Assert.Same(task, Assert.Single(grant.Tasks));
        Assert.Equal(taskId, task.Id);
        Assert.Equal(grant.Id, task.GrantId);
        Assert.Equal(ValidTaskTitle, task.Title);
        Assert.Equal(new DateOnly(2026, 3, 16), task.DueDate);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(GrantTaskStatus.Pending, task.Status);
        Assert.Equal(new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc), task.CreatedAtUtc);
        Assert.Null(task.CompletedAtUtc);
        Assert.Null(task.CancelledAtUtc);
    }

    [Fact]
    public void Given_ClosedGrant_When_AddingTask_Then_ThrowsBr1()
    {
        // Arrange
        var grant = ClosedGrant();

        // Act
        var exception = Assert.Throws<BusinessRuleException>(
            () => grant.AddTask(Guid.NewGuid(), ValidTaskTitle, new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc));

        // Assert
        Assert.Equal("BR-1", exception.Code);
        Assert.Empty(grant.Tasks);
    }

    [Fact]
    public void Given_ClosedGrantAndInvalidTaskTitle_When_AddingTask_Then_ThrowsBr1()
    {
        // Arrange
        var grant = ClosedGrant();

        // Act
        var exception = Assert.Throws<BusinessRuleException>(
            () => grant.AddTask(Guid.NewGuid(), "", new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc));

        // Assert
        Assert.Equal("BR-1", exception.Code);
    }

    [Fact]
    public void Given_DueDateToday_When_AddingTask_Then_TaskIsAdded()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));

        // Act
        var task = grant.AddTask(Guid.NewGuid(), ValidTaskTitle, new DateOnly(2026, 3, 11), TaskPriority.Medium, Today, NowUtc);

        // Assert
        Assert.Equal(new DateOnly(2026, 3, 11), task.DueDate);
        Assert.Single(grant.Tasks);
    }

    [Fact]
    public void Given_DueDateYesterday_When_AddingTask_Then_ThrowsValidationOnDueDate()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));

        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => grant.AddTask(Guid.NewGuid(), ValidTaskTitle, new DateOnly(2026, 3, 10), TaskPriority.Medium, Today, NowUtc));

        // Assert
        Assert.Equal("dueDate", Assert.Single(exception.Errors).Key);
        Assert.Empty(grant.Tasks);
    }

    [Fact]
    public void Given_DueDateEqualToApplicationDate_When_AddingTask_Then_TaskIsAdded()
    {
        // Arrange: the application date is in the future, so only the application-date rule applies.
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 3, 16));

        // Act
        var task = grant.AddTask(Guid.NewGuid(), ValidTaskTitle, new DateOnly(2026, 3, 16), TaskPriority.Medium, Today, NowUtc);

        // Assert
        Assert.Equal(new DateOnly(2026, 3, 16), task.DueDate);
        Assert.Single(grant.Tasks);
    }

    [Fact]
    public void Given_DueDateOneDayBeforeApplicationDate_When_AddingTask_Then_ThrowsValidationOnDueDate()
    {
        // Arrange: the application date is in the future, and the due date is after today,
        // so only the application-date rule can reject it.
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 3, 16));

        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => grant.AddTask(Guid.NewGuid(), ValidTaskTitle, new DateOnly(2026, 3, 15), TaskPriority.Medium, Today, NowUtc));

        // Assert
        Assert.Equal("dueDate", Assert.Single(exception.Errors).Key);
        Assert.Empty(grant.Tasks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyOrWhitespaceTaskTitle_When_AddingTask_Then_ThrowsValidationOnTitle(string taskTitle)
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));

        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => grant.AddTask(Guid.NewGuid(), taskTitle, new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc));

        // Assert
        Assert.Equal("title", Assert.Single(exception.Errors).Key);
        Assert.Empty(grant.Tasks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_TaskTitleOf100CharactersAfterTrimming_When_AddingTask_Then_TaskIsAdded(int spacesOnEachSide)
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));
        var padding = new string(' ', spacesOnEachSide);
        var taskTitle = padding + new string('a', 100) + padding;

        // Act
        var task = grant.AddTask(Guid.NewGuid(), taskTitle, new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc);

        // Assert
        Assert.Equal(100, task.Title.Length);
    }

    [Fact]
    public void Given_TaskTitleOf101CharactersAfterTrimming_When_AddingTask_Then_ThrowsValidationOnTitle()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));
        var taskTitle = new string('a', 101);

        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => grant.AddTask(Guid.NewGuid(), taskTitle, new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc));

        // Assert
        Assert.Equal("title", Assert.Single(exception.Errors).Key);
        Assert.Empty(grant.Tasks);
    }

    [Fact]
    public void Given_TaskTitleWithSurroundingSpaces_When_AddingTask_Then_TitleIsStoredTrimmed()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));

        // Act
        var task = grant.AddTask(Guid.NewGuid(), "  Verify bank details  ", new DateOnly(2026, 3, 16), TaskPriority.Low, Today, NowUtc);

        // Assert
        Assert.Equal("Verify bank details", task.Title);
    }

    [Fact]
    public void Given_InvalidTaskTitleAndInvalidDueDate_When_AddingTask_Then_BothFieldErrorsAreReported()
    {
        // Arrange
        var grant = OpenGrant(applicationDate: new DateOnly(2026, 1, 10));

        // Act
        var exception = Assert.Throws<DomainValidationException>(
            () => grant.AddTask(Guid.NewGuid(), "", new DateOnly(2026, 3, 10), TaskPriority.Low, Today, NowUtc));

        // Assert
        Assert.Equal(["dueDate", "title"], exception.Errors.Keys.Order());
        Assert.Empty(grant.Tasks);
    }

    private static Grant OpenGrant(DateOnly applicationDate) =>
        new(new Guid("00000000-0000-0000-0000-000000100001"), "GR-100001", "Riverside Community Garden Inc.", applicationDate);

    private static Grant ClosedGrant()
    {
        var grant = new Grant(
            new Guid("00000000-0000-0000-0000-000000100003"), "GR-100003", "Harbour Arts Collective", new DateOnly(2025, 8, 23));
        grant.Close(note: null, nowUtc: new DateTime(2025, 10, 16, 22, 0, 0, DateTimeKind.Utc));
        return grant;
    }
}

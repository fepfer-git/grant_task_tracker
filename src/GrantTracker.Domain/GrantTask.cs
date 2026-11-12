namespace GrantTracker.Domain;

/// <summary>A task on a grant (DESIGN.md §3.2). Created only through <see cref="Grant.AddTask"/>.</summary>
public class GrantTask
{
    public const int TitleMaxLength = 100;

    internal GrantTask(Guid id, Guid grantId, string title, DateOnly dueDate, TaskPriority priority, DateTime createdAtUtc)
    {
        Id = id;
        GrantId = grantId;
        Title = title;
        DueDate = dueDate;
        Priority = priority;
        CreatedAtUtc = createdAtUtc;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }

    public Guid GrantId { get; private set; }

    public string Title { get; private set; }

    /// <summary>A calendar date in the program time zone. Never converted to or from UTC (DESIGN.md §3.6).</summary>
    public DateOnly DueDate { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public Guid Version { get; private set; }

    /// <summary>Derived from the two timestamps, which are the only source of truth (DESIGN.md §3.3).</summary>
    public GrantTaskStatus Status =>
        CompletedAtUtc is not null ? GrantTaskStatus.Completed
        : CancelledAtUtc is not null ? GrantTaskStatus.Cancelled
        : GrantTaskStatus.Pending;
}

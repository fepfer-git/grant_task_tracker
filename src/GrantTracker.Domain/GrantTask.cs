namespace GrantTracker.Domain;

/// <summary>A task on a grant (DESIGN.md §3.2). Created only through <see cref="Grant.AddTask"/>.</summary>
public class GrantTask
{
    public const int TitleMaxLength = 100;

    internal GrantTask(Grant grant, Guid id, string title, DateOnly dueDate, TaskPriority priority, DateTime createdAtUtc)
    {
        Id = id;
        GrantId = grant.Id;
        Grant = grant;
        Title = title;
        DueDate = dueDate;
        Priority = priority;
        CreatedAtUtc = createdAtUtc;
        Version = Guid.NewGuid();
    }

    /// <summary>
    /// Used by EF Core when it loads a task. EF can't pass a navigation to a constructor, so <see cref="Grant"/>
    /// is set by EF only when the grant is loaded with the task (for example with <c>Include</c>).
    /// </summary>
    private GrantTask(Guid id, Guid grantId, string title, DateOnly dueDate, TaskPriority priority, DateTime createdAtUtc)
    {
        Id = id;
        GrantId = grantId;
        Grant = null!;
        Title = title;
        DueDate = dueDate;
        Priority = priority;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid GrantId { get; private set; }

    /// <summary>The grant this task belongs to. <see cref="Complete"/> needs it to apply the read-only rule (§4.7).</summary>
    public Grant Grant { get; private set; }

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

    /// <summary>
    /// BR-6: pending and due before <paramref name="today"/>. Due today is not overdue, and completed or
    /// cancelled tasks are never overdue (DESIGN.md BR-6, §10.4). <see cref="GrantTaskQueries.IsOverdue"/>
    /// is the same rule for database queries.
    /// </summary>
    /// <param name="today">The current date in the program time zone. Passed in, never read from a clock here.</param>
    public bool IsOverdue(DateOnly today) => Status == GrantTaskStatus.Pending && DueDate < today;

    /// <summary>
    /// BR-4, checked in the order of the DESIGN.md BR-4 table:
    /// already completed → no change, even on a closed grant;
    /// on a closed grant (pending or cancelled) → <see cref="BusinessRuleException.GrantClosed"/>;
    /// otherwise <see cref="CompletedAtUtc"/> = <paramref name="nowUtc"/>.
    /// </summary>
    /// <exception cref="BusinessRuleException"><c>grant-closed</c>: the grant is closed, so its tasks are read-only.</exception>
    public void Complete(DateTime nowUtc)
    {
        if (Status == GrantTaskStatus.Completed)
        {
            return;
        }

        // A cancelled task only exists on a closed grant, so it's rejected here too.
        if (Grant.Status == GrantStatus.Closed || Status == GrantTaskStatus.Cancelled)
        {
            throw new BusinessRuleException(
                BusinessRuleException.GrantClosed, "The grant is closed, so its tasks can't be changed.");
        }

        CompletedAtUtc = nowUtc;
    }

    /// <summary>Called by <see cref="Grant.Close"/> for each pending task.</summary>
    internal void Cancel(DateTime nowUtc)
    {
        CancelledAtUtc = nowUtc;
    }
}

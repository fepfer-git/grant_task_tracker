namespace GrantTracker.Domain;

/// <summary>A grant and its tasks (DESIGN.md §3.1). Grants are never deleted.</summary>
public class Grant
{
    private const string TitleField = "title";
    private const string DueDateField = "dueDate";

    private readonly List<GrantTask> _tasks = [];

    public Grant(Guid id, string reference, string organisationName, DateOnly applicationDate)
    {
        Id = id;
        Reference = reference;
        OrganisationName = organisationName;
        ApplicationDate = applicationDate;
        Status = GrantStatus.Open;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }

    public string Reference { get; private set; }

    public string OrganisationName { get; private set; }

    /// <summary>A calendar date in the program time zone. Never converted to or from UTC (DESIGN.md §3.6).</summary>
    public DateOnly ApplicationDate { get; private set; }

    public GrantStatus Status { get; private set; }

    public string? ClosedNote { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public Guid Version { get; private set; }

    public IReadOnlyCollection<GrantTask> Tasks => _tasks.AsReadOnly();

    /// <summary>
    /// Adds a pending task. Checks BR-1 first, then BR-2 and BR-3 together, so one exception carries
    /// every field error (DESIGN.md §4, "Order of checks when adding a task").
    /// </summary>
    /// <param name="today">The current date in the program time zone. Passed in, never read from a clock here (BR-6).</param>
    /// <param name="nowUtc">The current instant, stored as <see cref="GrantTask.CreatedAtUtc"/>.</param>
    /// <exception cref="BusinessRuleException">BR-1: the grant is closed.</exception>
    /// <exception cref="DomainValidationException">BR-2 or BR-3: the due date or the title is invalid.</exception>
    public GrantTask AddTask(Guid id, string title, DateOnly dueDate, TaskPriority priority, DateOnly today, DateTime nowUtc)
    {
        if (Status == GrantStatus.Closed)
        {
            throw new BusinessRuleException(BusinessRuleException.Br1GrantClosed, "A task cannot be added to a closed grant.");
        }

        var trimmedTitle = (title ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>();

        var titleErrors = ValidateTitle(trimmedTitle);
        if (titleErrors.Length > 0)
        {
            errors[TitleField] = titleErrors;
        }

        var dueDateErrors = ValidateDueDate(dueDate, today);
        if (dueDateErrors.Length > 0)
        {
            errors[DueDateField] = dueDateErrors;
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var task = new GrantTask(this, id, trimmedTitle, dueDate, priority, nowUtc);
        _tasks.Add(task);
        return task;
    }

    /// <summary>
    /// Closes the grant (DESIGN.md BR-5):
    /// already closed → no change (the note, <see cref="ClosedAtUtc"/> and the tasks stay as they are);
    /// any pending High-priority task → BR-5, and nothing changes;
    /// otherwise the grant is closed and every pending task is cancelled at the same instant.
    /// </summary>
    /// <param name="note">Optional. Trimmed; empty or whitespace becomes <c>null</c>.</param>
    /// <exception cref="BusinessRuleException">
    /// BR-5, with the blocking task ids in <see cref="BusinessRuleException.BlockingTaskIdsKey"/>.
    /// </exception>
    public void Close(string? note, DateTime nowUtc)
    {
        if (Status == GrantStatus.Closed)
        {
            return;
        }

        var blockingTaskIds = _tasks
            .Where(t => t.Status == GrantTaskStatus.Pending && t.Priority == TaskPriority.High)
            .Select(t => t.Id)
            .ToArray();

        if (blockingTaskIds.Length > 0)
        {
            throw new BusinessRuleException(
                BusinessRuleException.Br5PendingHighTasks,
                "A grant can't be closed while it has pending High-priority tasks.",
                new Dictionary<string, object?> { [BusinessRuleException.BlockingTaskIdsKey] = blockingTaskIds });
        }

        Status = GrantStatus.Closed;
        ClosedAtUtc = nowUtc;
        ClosedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        foreach (var task in _tasks.Where(t => t.Status == GrantTaskStatus.Pending))
        {
            task.Cancel(nowUtc);
        }
    }

    /// <summary>BR-3: required, and at most 100 characters after trimming.</summary>
    private static string[] ValidateTitle(string trimmedTitle)
    {
        if (trimmedTitle.Length == 0)
        {
            return ["Title is required."];
        }

        if (trimmedTitle.Length > GrantTask.TitleMaxLength)
        {
            return [$"Title must be {GrantTask.TitleMaxLength} characters or fewer."];
        }

        return [];
    }

    /// <summary>BR-2: not in the past and not before the application date. Both boundaries are allowed.</summary>
    private string[] ValidateDueDate(DateOnly dueDate, DateOnly today)
    {
        var messages = new List<string>();

        if (dueDate < today)
        {
            messages.Add("Due date cannot be in the past.");
        }

        if (dueDate < ApplicationDate)
        {
            messages.Add("Due date cannot be before the grant's application date.");
        }

        return [.. messages];
    }
}

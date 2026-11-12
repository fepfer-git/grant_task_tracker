namespace GrantTracker.Domain;

/// <summary>
/// A business rule blocked the action. <see cref="Code"/> becomes the ProblemDetails <c>type</c>, and
/// <see cref="Extensions"/> become extension fields of the ProblemDetails body (DESIGN.md §5.1).
/// </summary>
public sealed class BusinessRuleException : Exception
{
    /// <summary>BR-1: a task cannot be added to a closed grant.</summary>
    public const string Br1GrantClosed = "BR-1";

    /// <summary>§4.7: a closed grant's tasks are read-only.</summary>
    public const string GrantClosed = "grant-closed";

    /// <summary>BR-5: a grant cannot be closed while it has a pending High-priority task.</summary>
    public const string Br5PendingHighTasks = "BR-5";

    /// <summary>The extension field that lists the pending High-priority tasks blocking a close (BR-5).</summary>
    public const string BlockingTaskIdsKey = "blockingTaskIds";

    public BusinessRuleException(string code, string message, IReadOnlyDictionary<string, object?>? extensions = null)
        : base(message)
    {
        Code = code;
        Extensions = extensions ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object?> Extensions { get; }
}

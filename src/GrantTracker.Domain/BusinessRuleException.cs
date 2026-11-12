namespace GrantTracker.Domain;

/// <summary>
/// A business rule blocked the action. <see cref="Code"/> becomes the ProblemDetails <c>type</c> (DESIGN.md §5.1).
/// </summary>
public sealed class BusinessRuleException : Exception
{
    /// <summary>BR-1: a task cannot be added to a closed grant.</summary>
    public const string Br1GrantClosed = "BR-1";

    public BusinessRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

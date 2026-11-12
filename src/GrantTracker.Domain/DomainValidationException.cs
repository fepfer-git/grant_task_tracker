namespace GrantTracker.Domain;

/// <summary>
/// One or more input fields are invalid (BR-2, BR-3). <see cref="Errors"/> is keyed on the API field name
/// (<c>title</c>, <c>dueDate</c>), so the UI can show each message on its field (DESIGN.md §4, §5.1).
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

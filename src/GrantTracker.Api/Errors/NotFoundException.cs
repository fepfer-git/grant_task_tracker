namespace GrantTracker.Api.Errors;

/// <summary>The grant or task in the request doesn't exist. Mapped to 404 <c>not-found</c> (DESIGN.md §5.1).</summary>
public sealed class NotFoundException(string title, string detail) : Exception(detail)
{
    /// <summary>For example "Grant not found".</summary>
    public string Title { get; } = title;
}

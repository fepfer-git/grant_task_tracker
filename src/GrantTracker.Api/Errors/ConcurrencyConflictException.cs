namespace GrantTracker.Api.Errors;

/// <summary>
/// Every attempt to save conflicted with another request (DESIGN.md §6.2). Mapped to 409 <c>concurrency-conflict</c>.
/// </summary>
public sealed class ConcurrencyConflictException(int attempts, Exception lastConflict)
    : Exception($"The change conflicted with other changes on all {attempts} attempts.", lastConflict);

using GrantTracker.Domain;

namespace GrantTracker.Api.Contracts;

/// <summary>
/// Body of <c>POST /grants/{id}/tasks</c> (DESIGN.md §5). The client chooses <c>Id</c>, so sending the same request
/// again is safe (§6.3). Every field is nullable so a missing one is reported as a field error, not a JSON error.
/// </summary>
public sealed record CreateTaskRequest(Guid? Id, string? Title, DateOnly? DueDate, TaskPriority? Priority);

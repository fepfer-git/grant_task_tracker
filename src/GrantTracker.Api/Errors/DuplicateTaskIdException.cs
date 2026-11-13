namespace GrantTracker.Api.Errors;

/// <summary>
/// A task with this id already exists, with different details or on another grant (DESIGN.md §6.3).
/// Mapped to 409 <c>duplicate-task-id</c>.
/// </summary>
public sealed class DuplicateTaskIdException(Guid taskId)
    : Exception($"The task id {taskId} is already used by a task with a different title, due date, priority or grant.");

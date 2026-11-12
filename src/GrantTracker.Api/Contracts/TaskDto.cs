using GrantTracker.Domain;

namespace GrantTracker.Api.Contracts;

/// <summary>A task as the API returns it (DESIGN.md §5). <c>Status</c> and <c>IsOverdue</c> come from the domain.</summary>
public sealed record TaskDto(
    Guid Id,
    Guid GrantId,
    string Title,
    DateOnly DueDate,
    TaskPriority Priority,
    GrantTaskStatus Status,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc)
{
    /// <param name="today">The current date in the program time zone, for BR-6.</param>
    public static TaskDto From(GrantTask task, DateOnly today) => new(
        task.Id,
        task.GrantId,
        task.Title,
        task.DueDate,
        task.Priority,
        task.Status,
        task.IsOverdue(today),
        task.CreatedAtUtc,
        task.CompletedAtUtc,
        task.CancelledAtUtc);
}

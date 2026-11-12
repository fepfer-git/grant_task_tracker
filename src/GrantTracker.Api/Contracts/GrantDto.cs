using GrantTracker.Domain;

namespace GrantTracker.Api.Contracts;

/// <summary>
/// <c>GET /grants/{id}</c> (DESIGN.md §5). <c>Today</c> is the date in the program time zone, so the UI never uses the
/// browser's date (§4.0). Tasks are ordered by due date, then priority (High first), then id.
/// </summary>
public sealed record GrantDto(
    Guid Id,
    string Reference,
    string OrganisationName,
    DateOnly ApplicationDate,
    GrantStatus Status,
    string? ClosedNote,
    DateTime? ClosedAtUtc,
    int PendingTaskCount,
    DateOnly Today,
    IReadOnlyList<TaskDto> Tasks)
{
    public static GrantDto From(Grant grant, DateOnly today) => new(
        grant.Id,
        grant.Reference,
        grant.OrganisationName,
        grant.ApplicationDate,
        grant.Status,
        grant.ClosedNote,
        grant.ClosedAtUtc,
        grant.Tasks.Count(t => t.Status == GrantTaskStatus.Pending),
        today,
        [.. grant.Tasks
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.Id)
            .Select(t => TaskDto.From(t, today))]);
}

using GrantTracker.Domain;

namespace GrantTracker.Api.Contracts;

/// <summary>
/// One row of <c>GET /tasks/overdue</c> (DESIGN.md BR-6). It carries the grant's reference and organisation, so the
/// list makes sense without opening the grant.
/// </summary>
public sealed record OverdueTaskDto(
    Guid Id,
    Guid GrantId,
    string GrantReference,
    string OrganisationName,
    string Title,
    DateOnly DueDate,
    TaskPriority Priority);

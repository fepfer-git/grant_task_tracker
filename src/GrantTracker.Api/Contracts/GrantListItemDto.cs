using GrantTracker.Domain;

namespace GrantTracker.Api.Contracts;

/// <summary>One row of <c>GET /grants</c> (DESIGN.md §5). <c>PendingTaskCount</c> counts Pending tasks only (§7).</summary>
public sealed record GrantListItemDto(
    Guid Id,
    string Reference,
    string OrganisationName,
    DateOnly ApplicationDate,
    GrantStatus Status,
    DateTime? ClosedAtUtc,
    int PendingTaskCount);

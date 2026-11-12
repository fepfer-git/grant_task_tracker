using GrantTracker.Api.Contracts;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Time;
using GrantTracker.Domain;
using GrantTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Services;

/// <summary>The read side of the API (DESIGN.md §5). Reads don't track entities, and contain no business rules.</summary>
public sealed class GrantQueryService(AppDbContext db, IAppClock clock)
{
    /// <summary><c>GET /grants?status=</c>, ordered by reference.</summary>
    public async Task<IReadOnlyList<GrantListItemDto>> ListGrantsAsync(GrantStatus? status, CancellationToken cancellationToken)
    {
        var grants = db.Grants.AsNoTracking();
        if (status is not null)
        {
            grants = grants.Where(g => g.Status == status);
        }

        return await grants
            .OrderBy(g => g.Reference)
            .Select(g => new GrantListItemDto(
                g.Id,
                g.Reference,
                g.OrganisationName,
                g.ApplicationDate,
                g.Status,
                g.ClosedAtUtc,
                db.GrantTasks.Where(GrantTaskQueries.IsPending).Count(t => t.GrantId == g.Id)))
            .ToListAsync(cancellationToken);
    }

    /// <summary><c>GET /grants/{id}</c>, with <c>today</c> and the tasks.</summary>
    /// <exception cref="NotFoundException">No grant has this id.</exception>
    public async Task<GrantDto> GetGrantAsync(Guid id, CancellationToken cancellationToken)
    {
        var grant = await db.Grants.AsNoTracking().Include(g => g.Tasks).SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException("Grant not found", $"No grant has the id {id}.");

        return GrantDto.From(grant, clock.Today);
    }

    /// <summary>
    /// <c>GET /tasks/overdue</c> (BR-6): pending tasks due before today, most overdue first, then High priority first,
    /// then by id so the order never changes between calls.
    /// </summary>
    public async Task<IReadOnlyList<OverdueTaskDto>> ListOverdueTasksAsync(CancellationToken cancellationToken)
    {
        return await db.GrantTasks.AsNoTracking()
            .Where(GrantTaskQueries.IsOverdue(clock.Today))
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.Id)
            .Select(t => new OverdueTaskDto(t.Id, t.GrantId, t.Grant.Reference, t.Grant.OrganisationName, t.Title, t.DueDate, t.Priority))
            .ToListAsync(cancellationToken);
    }
}

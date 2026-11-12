using System.Linq.Expressions;

namespace GrantTracker.Domain;

/// <summary>
/// The task-status rules as expressions EF Core can translate to SQL. <see cref="GrantTask.Status"/> isn't a
/// column, so queries use the timestamp columns directly (DESIGN.md §3.3). Tested against SQLite in Part 4.
/// </summary>
public static class GrantTaskQueries
{
    /// <summary>Neither completed nor cancelled.</summary>
    public static readonly Expression<Func<GrantTask, bool>> IsPending =
        t => t.CompletedAtUtc == null && t.CancelledAtUtc == null;

    /// <summary>BR-6: pending and due before <paramref name="today"/>. Same rule as <see cref="GrantTask.IsOverdue"/>.</summary>
    public static Expression<Func<GrantTask, bool>> IsOverdue(DateOnly today) =>
        t => t.CompletedAtUtc == null && t.CancelledAtUtc == null && t.DueDate < today;
}

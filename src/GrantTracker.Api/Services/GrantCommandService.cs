using GrantTracker.Api.Contracts;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Time;
using GrantTracker.Domain;
using GrantTracker.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Services;

/// <summary>
/// The write side of the API (DESIGN.md §2): load the aggregate, take <c>today</c> and <c>nowUtc</c> from the clock,
/// call the domain method, save, and map to a DTO. The rules live in the domain; this class has none.
/// Every write runs inside <see cref="WithRetryAsync{T}"/> (§6.2), and task creation is safe to repeat (§6.3).
/// </summary>
public sealed class GrantCommandService(AppDbContext db, IAppClock clock)
{
    /// <summary>Attempts in total, including the first (§6.2).</summary>
    public const int MaxAttempts = 3;

    /// <summary>SQLite's extended error code for a primary-key violation.</summary>
    private const int SqlitePrimaryKeyViolation = 1555;

    /// <summary>
    /// <c>POST /grants/{id}/tasks</c> (§6.3). The id is looked up first: the same task again returns it as it is now
    /// (<c>Created</c> is false), and a different task with the same id is a conflict. Only a new id is checked against
    /// BR-1, BR-2 and BR-3, so a repeat that arrives after midnight or after the grant closed still succeeds.
    /// </summary>
    /// <exception cref="NotFoundException">No grant has this id.</exception>
    /// <exception cref="DuplicateTaskIdException">The id belongs to a task with other details or on another grant.</exception>
    public Task<CreateTaskResult> AddTaskAsync(
        Guid grantId, Guid taskId, string title, DateOnly dueDate, TaskPriority priority, CancellationToken cancellationToken) =>
        WithRetryAsync(async () =>
        {
            var existing = await db.GrantTasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken);
            if (existing is not null)
            {
                var isSameTask = existing.GrantId == grantId
                    && existing.Title == title.Trim()
                    && existing.DueDate == dueDate
                    && existing.Priority == priority;

                return isSameTask
                    ? new CreateTaskResult(TaskDto.From(existing, clock.Today), Created: false)
                    : throw new DuplicateTaskIdException(taskId);
            }

            var grant = await db.Grants.SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken)
                ?? throw GrantNotFound(grantId);

            var today = clock.Today;
            var task = grant.AddTask(taskId, title, dueDate, priority, today, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return new CreateTaskResult(TaskDto.From(task, today), Created: true);
        });

    /// <summary><c>PUT /tasks/{id}/complete</c>: BR-4 and the read-only rule (§4.7), checked by <see cref="GrantTask.Complete"/>.</summary>
    /// <exception cref="NotFoundException">No task has this id.</exception>
    public Task<TaskDto> CompleteTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        WithRetryAsync(async () =>
        {
            // The task's grant is loaded too, because Complete checks whether the grant is closed.
            var task = await db.GrantTasks.Include(t => t.Grant).SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
                ?? throw new NotFoundException("Task not found", $"No task has the id {taskId}.");

            task.Complete(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return TaskDto.From(task, clock.Today);
        });

    /// <summary><c>PUT /grants/{id}/close</c>: BR-5 and the note, checked by <see cref="Grant.Close"/>.</summary>
    /// <exception cref="NotFoundException">No grant has this id.</exception>
    public Task<GrantDto> CloseGrantAsync(Guid grantId, string? note, CancellationToken cancellationToken) =>
        WithRetryAsync(async () =>
        {
            // The tasks are loaded too, because Close checks for pending High tasks and cancels the pending ones.
            var grant = await db.Grants.Include(g => g.Tasks).SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken)
                ?? throw GrantNotFound(grantId);

            grant.Close(note, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return GrantDto.From(grant, clock.Today);
        });

    /// <summary>
    /// Runs load → domain method → save, and runs it again when the save conflicts with another request (§6.2), up to
    /// <see cref="MaxAttempts"/> in total. The tracked entities are cleared first, so the next attempt loads the
    /// current data and the domain checks every rule against it. A retry therefore ends in success, a no-op, or the
    /// rule's own error; it can't bypass a rule.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">Every attempt conflicted.</exception>
    private async Task<T> WithRetryAsync<T>(Func<Task<T>> attempt)
    {
        for (var attemptNumber = 1; ; attemptNumber++)
        {
            try
            {
                return await attempt();
            }
            catch (Exception conflict) when (IsConflict(conflict))
            {
                db.ChangeTracker.Clear();
                if (attemptNumber == MaxAttempts)
                {
                    throw new ConcurrencyConflictException(MaxAttempts, conflict);
                }
            }
        }
    }

    /// <summary>
    /// A version check failed, or a new task's id was inserted by another request first (two copies of the same
    /// create, §6.3). Other database errors aren't conflicts and aren't retried.
    /// </summary>
    private static bool IsConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException { InnerException: SqliteException { SqliteExtendedErrorCode: SqlitePrimaryKeyViolation } } update =>
            update.Entries.Any(e => e.Entity is GrantTask && e.State == EntityState.Added),
        _ => false,
    };

    private static NotFoundException GrantNotFound(Guid grantId) => new("Grant not found", $"No grant has the id {grantId}.");
}

/// <summary>The task, and whether this request created it (201) or found it already created (200).</summary>
public sealed record CreateTaskResult(TaskDto Task, bool Created);

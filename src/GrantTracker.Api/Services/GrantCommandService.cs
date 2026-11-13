using GrantTracker.Api.Contracts;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Time;
using GrantTracker.Domain;
using GrantTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Api.Services;

/// <summary>
/// The write side of the API (DESIGN.md §2): load the aggregate, take <c>today</c> and <c>nowUtc</c> from the clock,
/// call the domain method, save, and map to a DTO. The rules live in the domain; this class has none.
/// The retry on conflict (§6.2) and repeated task creation (§6.3) are added in Part 7.
/// </summary>
public sealed class GrantCommandService(AppDbContext db, IAppClock clock)
{
    /// <summary><c>POST /grants/{id}/tasks</c>: BR-1, then BR-2 and BR-3 (checked by <see cref="Grant.AddTask"/>).</summary>
    /// <exception cref="NotFoundException">No grant has this id.</exception>
    public async Task<TaskDto> AddTaskAsync(
        Guid grantId, Guid taskId, string title, DateOnly dueDate, TaskPriority priority, CancellationToken cancellationToken)
    {
        var grant = await db.Grants.SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken)
            ?? throw GrantNotFound(grantId);

        var today = clock.Today;
        var task = grant.AddTask(taskId, title, dueDate, priority, today, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, today);
    }

    /// <summary><c>PUT /tasks/{id}/complete</c>: BR-4 and the read-only rule (§4.7), checked by <see cref="GrantTask.Complete"/>.</summary>
    /// <exception cref="NotFoundException">No task has this id.</exception>
    public async Task<TaskDto> CompleteTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        // The task's grant is loaded too, because Complete checks whether the grant is closed.
        var task = await db.GrantTasks.Include(t => t.Grant).SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found", $"No task has the id {taskId}.");

        task.Complete(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, clock.Today);
    }

    /// <summary><c>PUT /grants/{id}/close</c>: BR-5 and the note, checked by <see cref="Grant.Close"/>.</summary>
    /// <exception cref="NotFoundException">No grant has this id.</exception>
    public async Task<GrantDto> CloseGrantAsync(Guid grantId, string? note, CancellationToken cancellationToken)
    {
        // The tasks are loaded too, because Close checks for pending High tasks and cancels the pending ones.
        var grant = await db.Grants.Include(g => g.Tasks).SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken)
            ?? throw GrantNotFound(grantId);

        grant.Close(note, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return GrantDto.From(grant, clock.Today);
    }

    private static NotFoundException GrantNotFound(Guid grantId) => new("Grant not found", $"No grant has the id {grantId}.");
}

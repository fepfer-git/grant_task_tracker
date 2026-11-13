using GrantTracker.Api.Contracts;
using GrantTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController(GrantQueryService queries, GrantCommandService commands) : ControllerBase
{
    /// <summary>Overdue tasks across all grants, most overdue first (BR-6).</summary>
    [HttpGet("overdue")]
    public Task<IReadOnlyList<OverdueTaskDto>> ListOverdue(CancellationToken cancellationToken) =>
        queries.ListOverdueTasksAsync(cancellationToken);

    /// <summary>Completes the task (BR-4). Completing it again returns 200 with the original <c>completedAtUtc</c>.</summary>
    [HttpPut("{id:guid}/complete")]
    public Task<TaskDto> Complete(Guid id, CancellationToken cancellationToken) =>
        commands.CompleteTaskAsync(id, cancellationToken);
}

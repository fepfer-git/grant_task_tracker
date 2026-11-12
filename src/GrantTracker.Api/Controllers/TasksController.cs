using GrantTracker.Api.Contracts;
using GrantTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController(GrantQueryService queries) : ControllerBase
{
    /// <summary>Overdue tasks across all grants, most overdue first (BR-6).</summary>
    [HttpGet("overdue")]
    public Task<IReadOnlyList<OverdueTaskDto>> ListOverdue(CancellationToken cancellationToken) =>
        queries.ListOverdueTasksAsync(cancellationToken);
}

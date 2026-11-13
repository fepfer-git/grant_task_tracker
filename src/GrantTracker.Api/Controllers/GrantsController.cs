using GrantTracker.Api.Contracts;
using GrantTracker.Api.Services;
using GrantTracker.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("grants")]
public sealed class GrantsController(GrantQueryService queries, GrantCommandService commands) : ControllerBase
{
    /// <summary>All grants, or only Open or only Closed ones. An unknown status is a 400.</summary>
    [HttpGet]
    public Task<IReadOnlyList<GrantListItemDto>> List([FromQuery] GrantStatus? status, CancellationToken cancellationToken) =>
        queries.ListGrantsAsync(status, cancellationToken);

    /// <summary>One grant with <c>today</c> and its tasks. An unknown id, or one that isn't a Guid, is a 404.</summary>
    [HttpGet("{id:guid}")]
    public Task<GrantDto> Get(Guid id, CancellationToken cancellationToken) =>
        queries.GetGrantAsync(id, cancellationToken);

    /// <summary>
    /// Adds a task. 201 with <c>Location</c> pointing to the grant, because there is no endpoint for a single task
    /// </summary>
    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> AddTask(Guid id, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        if (request.Id is null || request.Id == Guid.Empty)
        {
            ModelState.AddModelError("id", "Id is required and can't be the empty GUID.");
        }

        if (request.DueDate is null)
        {
            ModelState.AddModelError("dueDate", "Due date is required.");
        }

        if (request.Priority is null)
        {
            ModelState.AddModelError("priority", "Priority is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await commands.AddTaskAsync(
            id, request.Id!.Value, request.Title ?? string.Empty, request.DueDate!.Value, request.Priority!.Value, cancellationToken);

        // The same create sent again returns the task as it is now, with 200 (§6.3).
        return result.Created ? Created($"/grants/{id}", result.Task) : Ok(result.Task);
    }

    /// <summary>
    /// Closes the grant (BR-5). The body is optional (the parameter is nullable, so an empty body binds to null), and so
    /// is the note. Closing a closed grant changes nothing.
    /// </summary>
    [HttpPut("{id:guid}/close")]
    public Task<GrantDto> Close(Guid id, CloseGrantRequest? request, CancellationToken cancellationToken) =>
        commands.CloseGrantAsync(id, request?.Note, cancellationToken);
}

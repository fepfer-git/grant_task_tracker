using GrantTracker.Api.Contracts;
using GrantTracker.Api.Services;
using GrantTracker.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("grants")]
public sealed class GrantsController(GrantQueryService queries) : ControllerBase
{
    /// <summary>All grants, or only Open or only Closed ones. An unknown status is a 400.</summary>
    [HttpGet]
    public Task<IReadOnlyList<GrantListItemDto>> List([FromQuery] GrantStatus? status, CancellationToken cancellationToken) =>
        queries.ListGrantsAsync(status, cancellationToken);

    /// <summary>One grant with <c>today</c> and its tasks. An unknown id, or one that isn't a Guid, is a 404.</summary>
    [HttpGet("{id:guid}")]
    public Task<GrantDto> Get(Guid id, CancellationToken cancellationToken) =>
        queries.GetGrantAsync(id, cancellationToken);
}

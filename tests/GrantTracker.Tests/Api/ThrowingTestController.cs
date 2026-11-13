using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Tests.Api;

/// <summary>
/// Test-only endpoint that fails unexpectedly. Added to the app by <see cref="GrantTrackerApiFactory"/>. Left out of
/// the OpenAPI document, which lists only the app's own endpoints.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("test/throw")]
public sealed class ThrowingTestController : ControllerBase
{
    public const string SecretMessage = "Secret internal detail: Data Source=C:\\secret\\granttracker.db";

    [HttpGet]
    public IActionResult Throw() => throw new InvalidOperationException(SecretMessage);
}

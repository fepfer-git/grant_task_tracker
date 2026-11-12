using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Errors;

/// <summary>
/// The one place that turns exceptions into ProblemDetails bodies (DESIGN.md §5.1). Anything it doesn't recognise
/// is a 500 with a generic message: the exception is logged, never sent to the client.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            NotFoundException notFound => ProblemTypes.Create(
                StatusCodes.Status404NotFound, ProblemTypes.NotFound, notFound.Title, notFound.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = ProblemTypes.Create(
                StatusCodes.Status500InternalServerError,
                ProblemTypes.Internal,
                "An unexpected error occurred.",
                "The server couldn't complete the request. Try again, and contact support if it keeps happening.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}

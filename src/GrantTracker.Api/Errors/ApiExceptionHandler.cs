using GrantTracker.Domain;
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
            DomainValidationException invalid => ProblemTypes.CreateValidation(invalid.Errors),
            BusinessRuleException rule => FromBusinessRule(rule),
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

    /// <summary>
    /// 409 with the rule's code (BR-1, BR-5, grant-closed), the §5.1 title, and the exception's message as
    /// <c>detail</c>. Extension fields from the exception, such as BR-5's <c>blockingTaskIds</c>, are copied.
    /// </summary>
    private static ProblemDetails FromBusinessRule(BusinessRuleException rule)
    {
        var title = rule.Code switch
        {
            BusinessRuleException.Br1GrantClosed => "Grant is closed",
            BusinessRuleException.GrantClosed => "Grant is closed; its tasks are read-only",
            BusinessRuleException.Br5PendingHighTasks => "Grant has pending High-priority tasks",
            _ => "A business rule prevents this change",
        };

        var problem = ProblemTypes.Create(StatusCodes.Status409Conflict, rule.Code, title, rule.Message);
        foreach (var (key, value) in rule.Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }
}

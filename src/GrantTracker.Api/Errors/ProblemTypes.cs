using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Errors;

/// <summary>
/// The error codes and problem <c>type</c> URIs (DESIGN.md §5.1), and the defaults that give every error body the
/// same shape: <c>type</c>, <c>code</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>instance</c> and <c>traceId</c>.
/// </summary>
public static class ProblemTypes
{
    /// <summary>
    /// Every <c>type</c> is this base plus the code. <c>.example</c> is reserved for documentation, and nothing is
    /// published there yet (DESIGN.md §10.8).
    /// </summary>
    public const string TypeBaseUri = "https://granttracker.example/problems/";

    /// <summary>The extension field with the short code. The UI switches on it.</summary>
    public const string CodeField = "code";

    public const string Validation = "validation";
    public const string NotFound = "not-found";
    public const string Internal = "internal";

    /// <summary>The start of the links ASP.NET Core puts in <c>type</c> when it writes an error body itself.</summary>
    private const string FrameworkTypePrefix = "https://tools.ietf.org/";

    public static string TypeFor(string code) => TypeBaseUri + code;

    /// <summary>An error body with its <c>type</c> and <c>code</c> set from <paramref name="code"/>.</summary>
    public static ProblemDetails Create(int status, string code, string title, string detail) => new()
    {
        Status = status,
        Type = TypeFor(code),
        Title = title,
        Detail = detail,
        Extensions = { [CodeField] = code },
    };

    /// <summary>
    /// Runs for every ProblemDetails body, including those ASP.NET Core writes itself (model validation, and a
    /// route that doesn't match, such as an id that isn't a Guid). For 400, 404 and 500 it replaces the framework's
    /// RFC link with our <c>type</c> and <c>code</c>. Bodies built with <see cref="Create"/> already have both and are
    /// left as they are. It also fills in <c>detail</c>, <c>instance</c> and <c>traceId</c> when they're missing.
    /// </summary>
    public static void ApplyDefaults(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        var isFrameworkType = problem.Type is null || problem.Type.StartsWith(FrameworkTypePrefix, StringComparison.Ordinal);
        if (isFrameworkType && !problem.Extensions.ContainsKey(CodeField))
        {
            var code = status switch
            {
                StatusCodes.Status400BadRequest => Validation,
                StatusCodes.Status404NotFound => NotFound,
                StatusCodes.Status500InternalServerError => Internal,
                _ => null,
            };

            if (code is not null)
            {
                problem.Type = TypeFor(code);
                problem.Extensions[CodeField] = code;
            }
        }

        problem.Detail ??= status switch
        {
            StatusCodes.Status400BadRequest => "See the errors field for the fields to correct.",
            StatusCodes.Status404NotFound => "Nothing exists at this address.",
            _ => problem.Title,
        };

        problem.Instance ??= context.HttpContext.Request.Path;

        if (!problem.Extensions.ContainsKey("traceId"))
        {
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        }
    }
}

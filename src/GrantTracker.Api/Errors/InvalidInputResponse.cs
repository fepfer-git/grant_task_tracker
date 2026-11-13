using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrantTracker.Api.Errors;

/// <summary>
/// Builds the 400 for input that can't be read (DESIGN.md §6.3 step 1). When the JSON reader fails on a field, ASP.NET
/// Core keys the error on its JSON path (<c>$.dueDate</c>) with a message naming .NET types. This keys it on the field
/// (<c>dueDate</c>), as the UI expects, with a message that reveals nothing internal.
/// </summary>
public static class InvalidInputResponse
{
    public const string InvalidValueMessage = "The value isn't in the expected format.";

    private const string JsonPathRoot = "$";

    public static IActionResult Create(ActionContext context)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            if (key.StartsWith(JsonPathRoot, StringComparison.Ordinal))
            {
                var field = key == JsonPathRoot ? "body" : key[(JsonPathRoot.Length + 1)..];
                modelState.AddModelError(field, key == JsonPathRoot ? "The request body isn't valid JSON." : InvalidValueMessage);
                continue;
            }

            foreach (var error in entry.Errors)
            {
                modelState.AddModelError(key, error.ErrorMessage);
            }
        }

        var problemDetailsFactory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = problemDetailsFactory.CreateValidationProblemDetails(context.HttpContext, modelState, StatusCodes.Status400BadRequest);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}

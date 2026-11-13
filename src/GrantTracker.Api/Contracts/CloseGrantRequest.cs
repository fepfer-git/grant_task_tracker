namespace GrantTracker.Api.Contracts;

/// <summary>
/// Body of <c>PUT /grants/{id}/close</c> (DESIGN.md BR-5). The body and the note are both optional. The default value
/// is what tells the OpenAPI document that <c>note</c> isn't required.
/// </summary>
public sealed record CloseGrantRequest(string? Note = null);

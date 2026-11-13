namespace GrantTracker.Api.Contracts;

/// <summary>Body of <c>PUT /grants/{id}/close</c> (DESIGN.md BR-5). The body and the note are both optional.</summary>
public sealed record CloseGrantRequest(string? Note);

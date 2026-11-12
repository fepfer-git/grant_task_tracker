namespace GrantTracker.Domain;

/// <summary>Stored as an int, so database order matches business order: High &gt; Medium &gt; Low (DESIGN.md §3.2).</summary>
public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
}

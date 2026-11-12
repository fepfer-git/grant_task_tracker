namespace GrantTracker.Domain;

/// <summary>Derived from the task's timestamps, never stored (DESIGN.md §3.3).</summary>
public enum GrantTaskStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
}

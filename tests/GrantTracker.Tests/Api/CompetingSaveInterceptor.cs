using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrantTracker.Tests.Api;

/// <summary>
/// Makes races happen the same way on every run (DESIGN.md §9.1, OP-2). Once armed, it runs a competing change
/// (usually another officer's request to the API) just before the request under test saves, after it has loaded its
/// data. That save then meets data that changed under it, exactly as if the other officer had saved first.
/// </summary>
/// <remarks>
/// <see cref="GrantTrackerApiFactory"/> adds one instance to every DbContext the app creates. It does nothing until
/// <see cref="Arm"/> is called. Saves made by the competing change itself pass straight through. The request under
/// test waits for the competing change to finish, so the two never use the database at the same moment.
/// </remarks>
public sealed class CompetingSaveInterceptor : SaveChangesInterceptor
{
    private Func<Task>? _competingChange;
    private bool _onEverySave;
    private bool _runningCompetingChange;

    /// <summary>How many times the request under test tried to save while armed. Each retry adds one.</summary>
    public int RequestSaves { get; private set; }

    /// <param name="competingChange">The other officer's change.</param>
    /// <param name="onEverySave">
    /// <c>false</c>: compete before the first save only, so the retry runs against the new data.
    /// <c>true</c>: compete before every save, so every attempt conflicts.
    /// </param>
    public void Arm(Func<Task> competingChange, bool onEverySave = false)
    {
        _competingChange = competingChange;
        _onEverySave = onEverySave;
        RequestSaves = 0;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        BeforeSaveAsync().GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await BeforeSaveAsync();
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task BeforeSaveAsync()
    {
        if (_competingChange is null || _runningCompetingChange)
        {
            return;
        }

        RequestSaves++;
        if (RequestSaves > 1 && !_onEverySave)
        {
            return;
        }

        _runningCompetingChange = true;
        try
        {
            await _competingChange();
        }
        finally
        {
            _runningCompetingChange = false;
        }
    }
}

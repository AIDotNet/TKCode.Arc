namespace TKCode.Arc;

/// <summary>One simulated check of a <see cref="ProviderStatusSimulation"/>: what it returns, or that it fails.</summary>
public sealed class ProviderStatusSimulationStep
{
    /// <summary>The providers the check returns.</summary>
    public IReadOnlyList<ProviderStatusItem> Providers { get; set; } = [];

    /// <summary>The check fails instead: the card shows its error state, keeps the last results and retries.</summary>
    public bool Fails { get; set; }
}

/// <summary>
/// A simulated status feed for a <see cref="ProviderStatus"/> that has no <see cref="ProviderStatus.Providers"/> and no
/// <see cref="ProviderStatus.RefreshHandler"/>, for demos and prototypes. The card shows the first step's providers, then
/// runs a simulated check every <see cref="Interval"/> on its countdown (or when the refresh button is pressed): each
/// check takes <see cref="CheckDelay"/> and moves to the next step, looping, so incidents can open, post updates and
/// resolve. Nothing is fetched. Create one per card: it keeps its place in the story.
/// </summary>
public sealed class ProviderStatusSimulation
{
    private IReadOnlyList<ProviderStatusSimulationStep> _steps = [];

    /// <summary>The story, one step per check; the first step is what the card shows before any check.</summary>
    public IReadOnlyList<ProviderStatusSimulationStep> Steps
    {
        get => _steps;
        set
        {
            _steps = value ?? [];
            Restart();
        }
    }

    /// <summary>How often the simulation checks again (8 seconds by default).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>How long a simulated check takes (900ms by default).</summary>
    public TimeSpan CheckDelay { get; set; } = TimeSpan.FromMilliseconds(900);

    /// <summary>How many checks have run since the start.</summary>
    public int Check { get; private set; }

    /// <summary>The providers of the last successful step (the first step's before any check).</summary>
    public IReadOnlyList<ProviderStatusItem> Current { get; private set; } = [];

    /// <summary>Starts the story again from its first step.</summary>
    public void Restart()
    {
        Check = 0;
        Current = _steps.Count > 0 ? _steps[0].Providers : [];
    }

    /// <summary>
    /// Runs the next check: waits <see cref="CheckDelay"/>, moves to the next step and returns its providers, or throws when
    /// the step fails.
    /// </summary>
    public async Task<IReadOnlyList<ProviderStatusItem>> CheckAsync(CancellationToken cancellationToken)
    {
        if (CheckDelay > TimeSpan.Zero)
        {
            await Task.Delay(CheckDelay, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var next = Check + 1;
        Check = next;
        if (_steps.Count == 0)
        {
            return Current;
        }
        var step = _steps[next % _steps.Count];
        if (step.Fails)
        {
            throw new ProviderStatusSimulationException();
        }
        Current = step.Providers;
        return Current;
    }
}

/// <summary>The failure of a simulated check whose step <see cref="ProviderStatusSimulationStep.Fails"/>.</summary>
internal sealed class ProviderStatusSimulationException : Exception
{
}

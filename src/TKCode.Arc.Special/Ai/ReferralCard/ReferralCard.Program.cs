using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The simulated program: the simulation's beats play once when the card is 40% on screen, and simulated sends move their
// first address through the stages.
public partial class ReferralCard
{
    /// <summary>How much of the card must be on screen before the program starts.</summary>
    internal const double ProgramThreshold = 0.4;

    /// <summary>A simulated send takes this long.</summary>
    internal static readonly TimeSpan SimulatedSendTime = TimeSpan.FromSeconds(0.9);

    /// <summary>After a simulated send, the first address signs up after this pause, then pays after the same pause again.</summary>
    internal static readonly TimeSpan SimulatedStageStep = TimeSpan.FromSeconds(2.6);

    private ViewportVisibility? _viewport;
    private bool _programStarted;
    private CancellationTokenSource? _programCts;

    /// <summary>True once the program has started (it runs once per simulation).</summary>
    internal bool ProgramStarted => _programStarted;

    private void WatchViewport()
    {
        if (_programStarted || !Simulates || ShownSimulation is not { Beats.Count: > 0 } || !IsAttachedToVisualTree())
        {
            return;
        }
        if (_viewport is null)
        {
            _viewport = new ViewportVisibility(this, ProgramThreshold);
            _viewport.VisibilityChanged += OnViewportChanged;
        }
    }

    private bool IsAttachedToVisualTree() => Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(this);

    private void OnViewportChanged(object? sender, bool visible)
    {
        if (visible)
        {
            _viewport?.Dispose();
            _viewport = null;
            _ = PlayProgramAsync();
        }
    }

    private CancellationToken CurrentProgramToken()
    {
        _programCts ??= new CancellationTokenSource();
        return _programCts.Token;
    }

    /// <summary>The pause between beats: the script's time, halved without full motion (reduced motion plays twice as fast).</summary>
    internal TimeSpan Pace(TimeSpan delay) => ArcMotion.IsFull(this) ? delay : TimeSpan.FromTicks(delay.Ticks / 2);

    /// <summary>
    /// Plays the simulation's beats once, each after its pause. Stops when the card leaves the screen, gets real referrals
    /// or another simulation.
    /// </summary>
    internal async Task PlayProgramAsync()
    {
        if (_programStarted || !Simulates || ShownSimulation is not { } simulation)
        {
            return;
        }
        _programStarted = true;
        var token = CurrentProgramToken();
        foreach (var beat in simulation.Beats.ToList())
        {
            try
            {
                await Delay(Pace(beat.Delay), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || !ReferenceEquals(ShownSimulation, simulation))
            {
                return;
            }
            MoveTo(beat.Id, beat.Stage, beat.Name);
        }
    }

    /// <summary>Moves the first address of a simulated send through signed up and earned, one pause apart.</summary>
    private async Task AdvanceAsync(string id, CancellationToken token)
    {
        foreach (var stage in new[] { ReferralStage.SignedUp, ReferralStage.Earned })
        {
            try
            {
                await Delay(Pace(SimulatedStageStep), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || !MoveTo(id, stage, null))
            {
                return;
            }
        }
    }

    /// <summary>Moves one referral to a stage (giving it a name when it signs up), and says so.</summary>
    private bool MoveTo(string id, ReferralStage stage, string? name)
    {
        var current = _items.Find(r => r.Id == id);
        if (current is null)
        {
            return false;
        }
        var next = current.WithStage(stage);
        if (name is not null && string.IsNullOrWhiteSpace(next.Name))
        {
            next = next.WithName(name);
        }
        // Said first, so a milestone this change unlocks is the last thing the live region says.
        Announce(Labels.StageAnnouncement(ReferralCardFormat.Who(next), StageWord(stage, Labels)));
        return ReplaceItem(next);
    }

    private void CancelWork()
    {
        _programCts?.Cancel();
        _programCts?.Dispose();
        _programCts = null;
        _sendCts?.Cancel();
    }
}

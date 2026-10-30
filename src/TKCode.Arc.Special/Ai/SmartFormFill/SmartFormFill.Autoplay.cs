using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The steps of the docs loop: paste a sample, read and fill it, check a guess, undo, clear.</summary>
internal enum SmartFormFillAutoplayStep
{
    Paste,
    Review,
    Undo,
    Clear
}

public partial class SmartFormFill
{
    /// <summary>The wait before the loop pastes, after a fill before it checks a guess, before Undo and before it clears.</summary>
    internal static readonly TimeSpan AutoplayPasteDelay = TimeSpan.FromMilliseconds(900);

    internal static readonly TimeSpan AutoplayReviewDelay = TimeSpan.FromMilliseconds(2200);

    internal static readonly TimeSpan AutoplayUndoDelay = TimeSpan.FromMilliseconds(1600);

    internal static readonly TimeSpan AutoplayClearDelay = TimeSpan.FromMilliseconds(1600);

    private bool _autoplayStopped;
    private bool _attached;
    private bool _onScreen;
    private ViewportVisibility? _viewport;
    private IDisposable? _autoplayShot;
    private SmartFormFillAutoplayStep _autoplayNext = SmartFormFillAutoplayStep.Paste;
    private bool _autoplayAwaitingFill;

    /// <summary>
    /// Whether the loop may play: AutoPlay is true, or unset while a simulation plays; there are samples; motion is full;
    /// nobody has pressed a pointer or a key inside the card yet.
    /// </summary>
    internal bool AutoplayEnabled =>
        !_autoplayStopped && (AutoPlay ?? PlayingSimulation is not null) && EffectiveSamples.Count > 0 && ArcMotion.IsFull(this);

    /// <summary>Whether a loop step is waiting on its timer, or the loop waits for its read to fill.</summary>
    internal bool IsAutoplaying => _autoplayShot is not null || _autoplayAwaitingFill;

    /// <summary>The loop's next step.</summary>
    internal SmartFormFillAutoplayStep AutoplayNext => _autoplayNext;

    /// <summary>Whether the loop stopped for good (a pointer or key press inside the card).</summary>
    internal bool AutoplayStopped => _autoplayStopped;

    /// <summary>Tests: tells the loop the card is on screen or not, as the viewport watcher would.</summary>
    internal void SetOnScreen(bool onScreen) => OnViewportVisibility(this, onScreen);

    private void AttachAutoplay()
    {
        _attached = true;
        _viewport?.Dispose();
        _viewport = new ViewportVisibility(this, 0.05, once: false);
        _viewport.VisibilityChanged += OnViewportVisibility;
    }

    private void DetachAutoplay()
    {
        _attached = false;
        _onScreen = false;
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibility;
            _viewport.Dispose();
            _viewport = null;
        }
        PauseAutoplay();
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        _onScreen = visible;
        UpdateAutoplay();
    }

    /// <summary>Runs or pauses the loop: it plays only while attached, on screen and enabled; a paused loop resumes at its next step.</summary>
    private void UpdateAutoplay()
    {
        if (_attached && _onScreen && AutoplayEnabled)
        {
            if (_autoplayShot is null && !_autoplayAwaitingFill)
            {
                ScheduleAutoplay(_autoplayNext, _autoplayNext == SmartFormFillAutoplayStep.Paste ? AutoplayPasteDelay : TimeSpan.FromMilliseconds(600));
            }
        }
        else
        {
            PauseAutoplay();
        }
    }

    private void PauseAutoplay()
    {
        _autoplayShot?.Dispose();
        _autoplayShot = null;
    }

    /// <summary>The first pointer or key press inside the card stops the loop for good; what is on screen stays.</summary>
    private void StopAutoplayForGood()
    {
        if (_autoplayStopped)
        {
            return;
        }
        _autoplayStopped = true;
        _autoplayAwaitingFill = false;
        PauseAutoplay();
    }

    private void ScheduleAutoplay(SmartFormFillAutoplayStep step, TimeSpan delay)
    {
        PauseAutoplay();
        _autoplayNext = step;
        if (!_attached || !_onScreen || !AutoplayEnabled)
        {
            return;
        }
        _autoplayShot = Timers.Start(delay, () =>
        {
            _autoplayShot = null;
            RunAutoplay(step);
        });
    }

    private void RunAutoplay(SmartFormFillAutoplayStep step)
    {
        if (!AutoplayEnabled)
        {
            return;
        }
        switch (step)
        {
            case SmartFormFillAutoplayStep.Paste:
                _autoplayAwaitingFill = true;
                _autoplayNext = SmartFormFillAutoplayStep.Review;
                _ = PasteSampleForAutoplay();
                break;
            case SmartFormFillAutoplayStep.Review:
                var guess = _views.FirstOrDefault(v => v.State == SmartFormFillFieldState.Review);
                if (guess is not null)
                {
                    Check(guess.Field.Id);
                }
                ScheduleAutoplay(SmartFormFillAutoplayStep.Undo, AutoplayUndoDelay);
                break;
            case SmartFormFillAutoplayStep.Undo:
                Undo();
                ScheduleAutoplay(SmartFormFillAutoplayStep.Clear, AutoplayClearDelay);
                break;
            case SmartFormFillAutoplayStep.Clear:
                SetCurrentValue(TextProperty, string.Empty);
                ShowSource(false);
                SetStatus(ReadyStatus());
                ScheduleAutoplay(SmartFormFillAutoplayStep.Paste, AutoplayPasteDelay);
                break;
        }
    }

    private async Task PasteSampleForAutoplay()
    {
        var read = await TrySampleAsync();
        if (!read && _autoplayAwaitingFill)
        {
            // The read did not go through (stopped, replaced): start over from a clean paste.
            _autoplayAwaitingFill = false;
            ScheduleAutoplay(SmartFormFillAutoplayStep.Clear, AutoplayClearDelay);
        }
    }

    /// <summary>
    /// A new <see cref="Simulation"/> (or none): the loop stops, a read in flight is dropped, what the old samples filled is
    /// undone and the pasted text cleared, and the loop starts over with the new samples.
    /// </summary>
    private void OnSimulationChanged()
    {
        PauseAutoplay();
        _autoplayAwaitingFill = false;
        _autoplayNext = SmartFormFillAutoplayStep.Paste;
        _sampleIndex = 0;
        Stop();
        if (CanUndo)
        {
            Undo();
        }
        if (!string.IsNullOrEmpty(Text))
        {
            SetCurrentValue(TextProperty, string.Empty);
        }
        ShowSource(false);
        SetStatus(ReadyStatus());
        RefreshChrome();
        UpdateAutoplay();
    }

    /// <summary>The loop's read has filled the form: the next step waits for the person to look at it.</summary>
    private void OnAutoplayFilled()
    {
        if (!_autoplayAwaitingFill)
        {
            return;
        }
        _autoplayAwaitingFill = false;
        ScheduleAutoplay(SmartFormFillAutoplayStep.Review, AutoplayReviewDelay);
    }
}

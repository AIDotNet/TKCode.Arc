using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ReplyDraft
{
    /// <summary>How long a finished draft rests before the tour's next step.</summary>
    internal static readonly TimeSpan TourPause = TimeSpan.FromMilliseconds(1800);

    /// <summary>How long "Inserted" rests before the tour starts over.</summary>
    internal static readonly TimeSpan TourInsertedPause = TimeSpan.FromMilliseconds(2400);

    private ViewportVisibility? _viewport;
    private bool _onScreen;
    private bool _touched;
    private DispatcherTimer? _tourTimer;
    private bool _tourDue;
    private int _tourStep;

    internal bool EffectiveAutoPlay => CanDraft && (AutoPlay ?? PlayingSimulation is not null);

    /// <summary>Whether the reader has pressed or typed inside the card (the tour then stays stopped).</summary>
    internal bool IsTouched => _touched;

    /// <summary>The tour's next step (0 to 5).</summary>
    internal int TourStep => _tourStep;

    /// <summary>True while the tour waits to take its next step.</summary>
    internal bool IsTourScheduled => _tourTimer?.IsEnabled == true || _tourDue;

    /// <summary>Watches the card come into view: auto start writes the first draft, the tour runs only on screen.</summary>
    private void WatchViewport()
    {
        if (_viewport is null)
        {
            _viewport = new ViewportVisibility(this, 0.25, once: false);
            _viewport.VisibilityChanged += OnViewportVisibility;
            if (_viewport.IsVisible)
            {
                OnViewportVisibility(this, true);
            }
        }
        else if (_onScreen)
        {
            OnViewportVisibility(this, true);
        }
    }

    private void StopWatching()
    {
        StopTourTimer();
        _onScreen = false;
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibility;
            _viewport.Dispose();
            _viewport = null;
        }
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        _onScreen = visible;
        if (!visible)
        {
            // Off screen the tour pauses; the step it was waiting for runs when the card is back.
            if (_tourTimer?.IsEnabled == true)
            {
                _tourTimer.Stop();
                _tourDue = true;
            }
            return;
        }
        if (!_started && AutoStart && _draftSwap is not null)
        {
            StartDraft();
            return;
        }
        if (_tourDue)
        {
            ScheduleTour();
        }
    }

    /// <summary>Stops the tour for good (the reader pressed or typed inside the card).</summary>
    private void StopTour()
    {
        _touched = true;
        StopTourTimer();
    }

    private void StopTourTimer()
    {
        _tourTimer?.Stop();
        _tourDue = false;
    }

    /// <summary>Waits, then takes the tour's next step, when the tour is on and the card is at rest and on screen.</summary>
    private void ScheduleTour()
    {
        StopTourTimer();
        if (!EffectiveAutoPlay || _touched || Status is not (ReplyDraftStatus.Ready or ReplyDraftStatus.Inserted))
        {
            return;
        }
        if (!_onScreen)
        {
            _tourDue = true;
            return;
        }
        if (_tourTimer is null)
        {
            _tourTimer = new DispatcherTimer();
            _tourTimer.Tick += (_, _) =>
            {
                _tourTimer.Stop();
                TourNext();
            };
        }
        _tourTimer.Interval = Status == ReplyDraftStatus.Inserted ? TourInsertedPause : TourPause;
        _tourTimer.Start();
    }

    /// <summary>
    /// The tour, one step per finished draft: Concise, Formal, Short, the second quick reply, Insert, then back to the
    /// first quick reply, Friendly and Medium with a fresh draft.
    /// </summary>
    internal void TourNext()
    {
        _tourDue = false;
        if (!EffectiveAutoPlay || _touched || Status is not (ReplyDraftStatus.Ready or ReplyDraftStatus.Inserted))
        {
            return;
        }
        var step = _tourStep;
        _tourStep = (_tourStep + 1) % 6;
        switch (step)
        {
            case 0:
                SetCurrentValue(ToneProperty, ReplyDraftTone.Concise);
                break;
            case 1:
                SetCurrentValue(ToneProperty, ReplyDraftTone.Formal);
                break;
            case 2:
                SetCurrentValue(LengthProperty, ReplyDraftLength.Short);
                break;
            case 3:
                var list = DisplayedSuggestions;
                if (list.Count > 1)
                {
                    SelectSuggestion(list[1], fromReader: false);
                }
                else
                {
                    // No second quick reply: go straight to Insert.
                    TourNext();
                }
                break;
            case 4:
                _ = Insert();
                ScheduleTour();
                break;
            default:
                _quiet = true;
                try
                {
                    SetCurrentValue(ToneProperty, ReplyDraftTone.Friendly);
                    SetCurrentValue(LengthProperty, ReplyDraftLength.Medium);
                    SetCurrentValue(SelectedSuggestionIdProperty, DisplayedSuggestions.Count > 0 ? DisplayedSuggestions[0].Id : null);
                }
                finally
                {
                    _quiet = false;
                }
                SyncChips();
                Begin(ReplyDraftMode.Draft, hold: true);
                break;
        }
    }
}

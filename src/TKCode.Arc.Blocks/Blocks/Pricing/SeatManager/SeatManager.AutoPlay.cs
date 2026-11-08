using Avalonia;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class SeatManager
{
    /// <summary>One step of the demonstration loop, at a time from the start of the cycle.</summary>
    internal readonly record struct AutoPlayStep(int AtMs, string Name);

    /// <summary>
    /// The demonstration loop as measured on Arc's preview (notes 2b.3, six identical cycles): +1, +1, +1, Update seats,
    /// Pay (900 ms charge), resend the first invite (660 ms), the done message returns to the card line by itself
    /// (2.8 s), revoke the second invite (3 s undo window, then the row collapses), reset, and the next cycle at 12.7 s.
    /// </summary>
    internal static readonly AutoPlayStep[] AutoPlayScript =
    [
        new(0, "add"),
        new(282, "add"),
        new(582, "add"),
        new(1683, "review"),
        new(2984, "pay"),
        new(5281, "resend"),
        new(6798, "revoke"),
        new(11399, "reset")
    ];

    /// <summary>Length of one cycle.</summary>
    internal const int AutoPlayCycleMs = 12700;

    /// <summary>Wait between the card coming into view and the first step.</summary>
    internal const int AutoPlayLeadInMs = 900;

    private CancellationTokenSource? _autoPlay;
    private ViewportVisibility? _viewport;
    private bool _onScreen;
    private bool _autoPlayStopped;
    private TaskCompletionSource? _visibleSignal;
    private int _snapshotPurchased;
    private List<SeatManagerInvite>? _snapshotInvites;

    /// <summary>Overrides whether the card counts as on screen (tests).</summary>
    internal bool? OnScreenOverride
    {
        get => _onScreenOverride;
        set
        {
            _onScreenOverride = value;
            SetOnScreen(value ?? _viewport?.IsVisible ?? false);
        }
    }

    private bool? _onScreenOverride;

    private bool WantsAutoPlay => !_autoPlayStopped && (IsAutoPlayEnabled ?? IsSimulated);

    /// <summary>Stops the demonstration loop for good and leaves the card as it is.</summary>
    public void StopAutoPlay()
    {
        if (_autoPlayStopped)
        {
            return;
        }
        _autoPlayStopped = true;
        CancelAutoPlay();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport ??= CreateViewport();
        UpdateAutoPlay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelAutoPlay();
        _viewport?.Dispose();
        _viewport = null;
        _onScreen = false;
        _saving?.Cancel();
        _noticeTimer?.Cancel();
        _noticeTimer = null;
        _limitNotice = null;
    }

    private ViewportVisibility CreateViewport()
    {
        var viewport = new ViewportVisibility(this, threshold: 0.2, once: false);
        viewport.VisibilityChanged += (_, visible) =>
        {
            if (_onScreenOverride is null)
            {
                SetOnScreen(visible);
            }
        };
        if (_onScreenOverride is null)
        {
            _onScreen = viewport.IsVisible;
        }
        return viewport;
    }

    private void SetOnScreen(bool visible)
    {
        _onScreen = visible;
        if (visible)
        {
            _visibleSignal?.TrySetResult();
            _visibleSignal = null;
        }
        UpdateAutoPlay();
    }

    private void UpdateAutoPlay()
    {
        if (WantsAutoPlay && VisualRoot is not null)
        {
            if (_autoPlay is null)
            {
                _autoPlay = new CancellationTokenSource();
                RunAutoPlay(_autoPlay.Token);
            }
        }
        else
        {
            CancelAutoPlay();
        }
    }

    private void CancelAutoPlay()
    {
        if (_autoPlay is null)
        {
            return;
        }
        _autoPlay.Cancel();
        _autoPlay = null;
        _visibleSignal?.TrySetCanceled();
        _visibleSignal = null;
        IsAutoPlaying = false;
    }

    private async Task WhenOnScreen(CancellationToken cancel)
    {
        while (!_onScreen)
        {
            _visibleSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancel.Register(() => _visibleSignal?.TrySetCanceled()))
            {
                await _visibleSignal.Task;
            }
            cancel.ThrowIfCancellationRequested();
        }
    }

    private async void RunAutoPlay(CancellationToken cancel)
    {
        try
        {
            await WhenOnScreen(cancel);
            await Delay(TimeSpan.FromMilliseconds(AutoPlayLeadInMs), cancel);
            IsAutoPlaying = true;
            while (!cancel.IsCancellationRequested)
            {
                _snapshotPurchased = Purchased;
                _snapshotInvites = _invites.ToList();
                var at = 0;
                foreach (var step in AutoPlayScript)
                {
                    await Delay(TimeSpan.FromMilliseconds(step.AtMs - at), cancel);
                    at = step.AtMs;
                    // Off screen the loop waits where it is (Arc: "the demo loop pauses off screen").
                    await WhenOnScreen(cancel);
                    RunStep(step.Name);
                }
                await Delay(TimeSpan.FromMilliseconds(AutoPlayCycleMs - at), cancel);
                await WhenOnScreen(cancel);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped, detached or handed to the person.
        }
    }

    /// <summary>Runs one step of the loop without announcing it (Arc keeps the live region quiet while it plays).</summary>
    internal void RunStep(string name)
    {
        _quiet = true;
        try
        {
            switch (name)
            {
                case "add":
                    Increment();
                    break;
                case "review":
                    Review();
                    break;
                case "pay":
                    _ = ConfirmAsync();
                    break;
                case "resend":
                    if (_invites.Count > 0)
                    {
                        Resend(_invites[0]);
                    }
                    break;
                case "revoke":
                    if (_invites.Count > 1)
                    {
                        Revoke(_invites[1]);
                    }
                    break;
                case "reset":
                    ResetForLoop();
                    break;
            }
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>Back to the start of the loop: seats, invites (a removed row opens again), resend buttons, footer.</summary>
    private void ResetForLoop()
    {
        foreach (var pending in _pending.Values)
        {
            pending.Cancel.Cancel();
        }
        _pending.Clear();
        _resend.Clear();
        _saving?.Cancel();
        _saving = null;
        _doneTimer?.Cancel();
        _doneTimer = null;
        _noticeTimer?.Cancel();
        _noticeTimer = null;
        _limitNotice = null;
        ErrorMessage = null;
        Phase = SeatManagerPhase.Idle;
        if (_snapshotInvites is not null)
        {
            var same = _snapshotInvites.Count == _invites.Count && _snapshotInvites.SequenceEqual(_invites);
            if (!same)
            {
                _invites.Clear();
                foreach (var invite in _snapshotInvites)
                {
                    _invites.Add(invite);
                }
            }
        }
        foreach (var row in _rows.Values)
        {
            row.IsRevoked = false;
            row.ResendState = SeatManagerResendState.Idle;
            if (row.IsLeaving)
            {
                row.Reopen();
            }
        }
        Purchased = _snapshotPurchased;
        Target = _snapshotPurchased;
        Refresh();
    }
}

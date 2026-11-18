using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class FeedbackMorph
{
    // Arc Feedback morph (design reference): opening fades the button out over 100 ms while the panel comes in 60 ms later;
    // closing fades the panel over 100 ms and brings the button back 70 ms later; a successful send fades the panel over
    // 80 ms and the thank-you pill in 60 ms later; folding the pill fades it over 70 ms and the button in 60 ms later.
    private static readonly TimeSpan OpenExit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan CloseExit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(70);
    private static readonly TimeSpan SentExit = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan SentDelay = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan FoldExit = TimeSpan.FromMilliseconds(70);
    private static readonly TimeSpan FoldDelay = TimeSpan.FromMilliseconds(60);

    private TopLevel? _pressRoot;
    private DispatcherTimer? _resetTimer;
    private CancellationTokenSource? _submitCts;
    private bool _syncingOpen;
    private int _focusRequest;

    /// <summary>Opens the panel from the button (the thank-you pill, if showing, goes at once).</summary>
    public void Open()
    {
        if (State is FeedbackMorphState.Open or FeedbackMorphState.Sending)
        {
            SyncIsOpen(true);
            return;
        }
        if (State == FeedbackMorphState.Sent)
        {
            EndSent(animate: false);
        }
        var panel = EnsurePanel();
        FitToViewport(panel);
        panel.ClearMessage();
        SetState(FeedbackMorphState.Open, null);
        _surface?.Show(panel, OpenExit, OpenDelay, scaleIn: true);
        _trigger.IsTabStop = false;
        AutomationProperties.SetAccessibilityView(_trigger, AccessibilityView.Raw);
        WatchOutsidePresses(true);
        var method = SpecialFocusModality.For(this);
        PostFocus(() => State == FeedbackMorphState.Open && panel.FocusFirst(method));
    }

    /// <summary>Folds the panel back into the button, keeping the draft (a send in progress is cancelled).</summary>
    public void Close() => CloseCore(returnFocus: FocusIsInside());

    /// <summary>Sends the picked face and the note, as the send button does; with nothing to send it shows the hint.</summary>
    public async void Submit()
    {
        if (State != FeedbackMorphState.Open || _panel is not { } panel)
        {
            return;
        }
        if (!HasContent)
        {
            panel.ShowEmptyHint();
            Announce(EmptyHint);
            return;
        }
        var submission = new FeedbackMorphSubmission(SelectedMood, (Message ?? string.Empty).Trim());
        SetState(FeedbackMorphState.Sending, null);
        panel.SetBusy(true);
        Announce(SendingLabel);
        if (SubmitHandler is { } handler)
        {
            var cts = new CancellationTokenSource();
            _submitCts = cts;
            try
            {
                await handler(submission, cts.Token);
            }
            catch (OperationCanceledException)
            {
                FinishSubmit(cts);
                return;
            }
            catch (Exception error)
            {
                if (FinishSubmit(cts))
                {
                    panel.SetBusy(false);
                    SetState(FeedbackMorphState.Open, error);
                    panel.ShowError();
                    Announce(ErrorMessage);
                }
                return;
            }
            if (!FinishSubmit(cts))
            {
                return;
            }
        }
        panel.SetBusy(false);
        RaiseEvent(new FeedbackMorphSubmittedEventArgs(SubmittedEvent, submission));
        if (State == FeedbackMorphState.Sending)
        {
            Succeed();
        }
    }

    /// <summary>Clears the picked face and the note.</summary>
    public void ClearDraft()
    {
        SetCurrentValue(SelectedMoodProperty, null);
        SetCurrentValue(MessageProperty, string.Empty);
        _panel?.ClearMessage();
    }

    /// <summary>Ends a run of the handler; false when it was cancelled (the panel was closed meanwhile).</summary>
    private bool FinishSubmit(CancellationTokenSource cts)
    {
        var current = ReferenceEquals(_submitCts, cts) && !cts.IsCancellationRequested;
        if (ReferenceEquals(_submitCts, cts))
        {
            _submitCts = null;
        }
        cts.Dispose();
        return current;
    }

    private void CancelSubmit()
    {
        if (_submitCts is not { } cts)
        {
            return;
        }
        _submitCts = null;
        cts.Cancel();
        _panel?.SetBusy(false);
    }

    private void Succeed()
    {
        var sent = EnsureSent();
        var focusInside = FocusIsInside();
        SetState(FeedbackMorphState.Sent, null);
        WatchOutsidePresses(false);
        _surface?.Show(sent, SentExit, SentDelay, scaleIn: true);
        Announce(SuccessLabel);
        if (focusInside)
        {
            // Keep the keyboard on the control: the pill takes focus (it is not in the tab order).
            PostFocus(() => State == FeedbackMorphState.Sent && sent.Focus(SpecialFocusModality.For(this)));
        }
        _resetTimer ??= CreateResetTimer();
        _resetTimer.Stop();
        _resetTimer.Interval = Positive(ResetAfter);
        _resetTimer.Start();
    }

    private DispatcherTimer CreateResetTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            EndSent(animate: true);
        };
        return timer;
    }

    /// <summary>Folds the thank-you pill back into the button and starts a fresh draft.</summary>
    internal void EndSent(bool animate)
    {
        _resetTimer?.Stop();
        if (State != FeedbackMorphState.Sent)
        {
            return;
        }
        var focusInside = FocusIsInside();
        ClearDraft();
        SetState(FeedbackMorphState.Closed, null);
        ShowTrigger(animate ? FoldExit : TimeSpan.Zero, FoldDelay);
        if (focusInside)
        {
            _trigger.Focus(SpecialFocusModality.For(this));
        }
        if (!animate)
        {
            _surface?.FinishExits();
        }
    }

    private void CloseCore(bool returnFocus)
    {
        if (State == FeedbackMorphState.Sent)
        {
            EndSent(animate: true);
            return;
        }
        if (State is not (FeedbackMorphState.Open or FeedbackMorphState.Sending))
        {
            SyncIsOpen(false);
            return;
        }
        CancelSubmit();
        _focusRequest++;
        SetState(FeedbackMorphState.Closed, null);
        WatchOutsidePresses(false);
        _panel?.OnLeaving(ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full);
        ShowTrigger(CloseExit, CloseDelay);
        if (returnFocus)
        {
            // Give focus back before the panel leaves, or it would fall to the window without a ring.
            _trigger.Focus(SpecialFocusModality.For(this));
        }
    }

    private void ShowTrigger(TimeSpan exit, TimeSpan delay)
    {
        _trigger.IsTabStop = true;
        AutomationProperties.SetAccessibilityView(_trigger, AccessibilityView.Default);
        _surface?.Show(_trigger, exit, delay, scaleIn: false);
    }

    /// <summary>The close button (focus goes back to the button).</summary>
    internal void CloseFromPanel(bool returnFocus) => CloseCore(returnFocus);

    /// <summary>A face picked in the panel.</summary>
    internal void PickFromPanel(FeedbackMood mood)
    {
        if (State != FeedbackMorphState.Open)
        {
            return;
        }
        SetCurrentValue(SelectedMoodProperty, mood);
        _panel?.ClearMessage();
    }

    /// <summary>The note edited in the panel.</summary>
    internal void MessageFromPanel(string text) => SetCurrentValue(MessageProperty, text);

    private void SetState(FeedbackMorphState state, Exception? error)
    {
        var old = State;
        if (old == state && error is null)
        {
            return;
        }
        State = state;
        UpdatePseudoClasses();
        SyncIsOpen(state is FeedbackMorphState.Open or FeedbackMorphState.Sending);
        RaiseEvent(new FeedbackMorphStateChangedEventArgs(StateChangedEvent, old, state, error));
    }

    private void SyncIsOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }
        _syncingOpen = true;
        try
        {
            SetCurrentValue(IsOpenProperty, open);
        }
        finally
        {
            _syncingOpen = false;
        }
    }

    private Control FaceFor(FeedbackMorphState state) => state switch
    {
        FeedbackMorphState.Open or FeedbackMorphState.Sending => EnsurePanel(),
        FeedbackMorphState.Sent => EnsureSent(),
        _ => _trigger
    };

    private FeedbackMorphPanel EnsurePanel()
    {
        if (_panel is null)
        {
            _panel = new FeedbackMorphPanel(this);
            ApplyWords(_panel);
        }
        return _panel;
    }

    private FeedbackMorphSentFace EnsureSent()
    {
        return _sent ??= new FeedbackMorphSentFace { Text = SuccessLabel };
    }

    /// <summary>Caps the panel at the viewport width less the margins, so it fits narrow phones.</summary>
    private void FitToViewport(FeedbackMorphPanel panel)
    {
        var width = Token(FeedbackMorphTokenKind.PanelWidth, 352);
        var margin = Token(FeedbackMorphTokenKind.ViewportMargin, 16);
        if (ArcViewport.GetSize(this) is { Width: > 0 } viewport)
        {
            width = Math.Max(0, Math.Min(width, viewport.Width - 2 * margin));
        }
        panel.Width = width;
    }

    private double Token(FeedbackMorphTokenKind kind, double fallback) =>
        this.TryFindResource(FeedbackMorphTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private bool FocusIsInside()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }
        return ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused)
               || (focused is ILogical logical && this.IsLogicalAncestorOf(logical));
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (State == FeedbackMorphState.Closed)
        {
            Open();
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (State is FeedbackMorphState.Open or FeedbackMorphState.Sending or FeedbackMorphState.Sent)
        {
            e.Handled = true;
            CloseCore(returnFocus: true);
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // Tabbing past the send button (or focus moving anywhere outside) folds the panel, as Arc does; the draft stays.
        if (State != FeedbackMorphState.Open)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (State == FeedbackMorphState.Open && !FocusIsInside()
                && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not null)
            {
                CloseCore(returnFocus: false);
            }
        }, DispatcherPriority.Input);
    }

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (State is not (FeedbackMorphState.Open or FeedbackMorphState.Sending) || e.Source is not Visual source)
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)
            || (source is ILogical logical && this.IsLogicalAncestorOf(logical)))
        {
            return;
        }
        CloseCore(returnFocus: false);
    }
}

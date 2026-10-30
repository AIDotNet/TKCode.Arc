using Avalonia;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The flow of <see cref="PiiRedaction"/>: scan, send, stream, restore, stop, reset, retry and the autoplay loop.</summary>
public partial class PiiRedaction
{
    /// <summary>The time between two marks of the scan.</summary>
    internal static readonly TimeSpan MarkStagger = TimeSpan.FromMilliseconds(120);

    /// <summary>One hop of the dot along a connector (ease in out).</summary>
    internal static readonly TimeSpan HopDuration = TimeSpan.FromMilliseconds(460);

    /// <summary>The time between two chunks of the streaming reply.</summary>
    internal static readonly TimeSpan ChunkInterval = TimeSpan.FromMilliseconds(34);

    /// <summary>How long the simulated model takes before it answers.</summary>
    internal static readonly TimeSpan SimulatedThinking = TimeSpan.FromMilliseconds(900);

    /// <summary>How long the guard visibly works on putting the values back.</summary>
    internal static readonly TimeSpan RestoreHold = TimeSpan.FromMilliseconds(360);

    /// <summary>The autoplay's pauses: before the scan, between scan and send, and on the restored reply.</summary>
    internal static readonly TimeSpan AutoPlayLead = TimeSpan.FromMilliseconds(1200);

    internal static readonly TimeSpan AutoPlayBeat = TimeSpan.FromMilliseconds(1100);

    internal static readonly TimeSpan AutoPlayRest = TimeSpan.FromMilliseconds(3400);

    private PiiRedactionResult? _result;
    private string? _raw;
    private string? _restoredText;
    private List<PiiRestoredRange> _restoredRanges = [];
    private string? _error;
    private PiiRedactionStep _failed;
    private bool _stopped;
    private bool _editingFromField;
    private CancellationTokenSource? _run;
    private CancellationTokenSource? _auto;
    private bool _tookOver;
    private bool _onScreen;
    private ViewportVisibility? _viewport;
    private MotionValue<double>? _hopOut;
    private MotionValue<double>? _hopIn;

    /// <summary>Creates the card.</summary>
    public PiiRedaction()
    {
        for (var i = 0; i < _cellStates.Length; i++)
        {
            _cellStates[i] = PiiRedactionCellState.NotChecked;
        }
    }

    /// <summary>Waits for the flow (tests replace it with a clock of their own).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>Whether someone has interacted, which stops the autoplay for good.</summary>
    internal bool HasTakenOver => _tookOver;

    /// <summary>Whether the autoplay loop is running.</summary>
    internal bool IsAutoPlaying => _auto is not null;

    /// <summary>
    /// Scans the message: marks each entity in turn and swaps the message for the safe prompt. Does nothing unless the
    /// card is idle (or failed while scanning). Stops the autoplay for good.
    /// </summary>
    public Task Scan()
    {
        TakeOver();
        return ScanCore();
    }

    /// <summary>Sends the safe prompt and restores the reply. Does nothing unless the message is redacted. Stops the autoplay for good.</summary>
    public Task Send()
    {
        TakeOver();
        return SendCore();
    }

    /// <summary>
    /// Stops the scan or the request in flight: a scan goes back to the editable message, a request back to the safe
    /// prompt. Cancels the token passed to <see cref="Detector"/> or <see cref="Sender"/>.
    /// </summary>
    public void Stop()
    {
        TakeOver();
        StopCore();
    }

    /// <summary>Returns to the editable message, cancelling anything in flight, and moves focus to the field.</summary>
    public void Reset()
    {
        TakeOver();
        ResetCore(focus: true);
    }

    private Task Wait(TimeSpan time, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return time <= TimeSpan.Zero ? Task.CompletedTask : Delay(time, token);
    }

    /// <summary>A wait that only exists for motion: none under reduced motion or Off.</summary>
    private Task MotionWait(TimeSpan time, CancellationToken token) =>
        ArcMotion.IsFull(this) ? Wait(time, token) : Wait(TimeSpan.Zero, token);

    private CancellationToken BeginRun()
    {
        _run?.Cancel();
        _run = new CancellationTokenSource();
        return _run.Token;
    }

    private void OnPhaseChanged(PiiRedactionPhase old, PiiRedactionPhase phase)
    {
        RaiseEvent(new PiiRedactionPhaseChangedEventArgs(PhaseChangedEvent, old, phase));
        Refresh(instant: false);
        var text = StatusFor(phase);
        if (phase == PiiRedactionPhase.Done && IsSimulated)
        {
            text += ". " + Labels.SimulatedNotice;
        }
        _announcer?.Announce(text);
    }

    internal async Task ScanCore()
    {
        if (!ShowsEditor)
        {
            return;
        }
        var token = BeginRun();
        _stopped = false;
        _error = null;
        _failed = PiiRedactionStep.None;
        var message = EffectiveMessage;
        if (_editor?.IsFocused == true)
        {
            _primary?.Focus();
        }
        try
        {
            _result = null;
            for (var i = 0; i < 4; i++)
            {
                _cellStates[i] = new PiiRedactionCellState(PiiRedactionCellKind.Checking, []);
            }
            Phase = PiiRedactionPhase.Scanning;
            IReadOnlyList<PiiMatch> matches;
            if (Detector is { } detector)
            {
                matches = await detector(message, token);
            }
            else
            {
                matches = PiiRedactor.Detect(message, EffectiveNames);
            }
            token.ThrowIfCancellationRequested();
            var result = PiiRedactor.Redact(message, matches);
            _result = result;
            RebuildMessageLayers();
            Refresh(instant: false);

            // Each entity in turn, 120ms apart: its mark tints in 160ms and its type cell crossfades to its token.
            var found = new List<string>[4];
            for (var i = 0; i < result.Entities.Count; i++)
            {
                if (i > 0)
                {
                    await MotionWait(MarkStagger, token);
                }
                var entity = result.Entities[i];
                StartTint(_originalMarks[i]);
                var tokens = found[(int)entity.Type] ??= [];
                if (!tokens.Contains(entity.Token))
                {
                    tokens.Add(entity.Token);
                }
                _cellStates[(int)entity.Type] = new PiiRedactionCellState(PiiRedactionCellKind.Found, tokens.ToArray());
                RefreshCells();
            }
            await MotionWait(LayerIn, token);
            FinishTints();
            for (var i = 0; i < 4; i++)
            {
                if (found[i] is null)
                {
                    _cellStates[i] = new PiiRedactionCellState(PiiRedactionCellKind.None, []);
                }
            }
            SafePrompt = result.Prompt;
            Phase = PiiRedactionPhase.Redacted;
            RaiseEvent(new PiiRedactedEventArgs(RedactedEvent, result.Prompt, result.Entities));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Fail(PiiRedactionStep.Scan, ex);
            }
        }
    }

    internal async Task SendCore()
    {
        if (_result is null || !(_phase == PiiRedactionPhase.Redacted || _phase == PiiRedactionPhase.Error && _failed == PiiRedactionStep.Send))
        {
            return;
        }
        var token = BeginRun();
        _stopped = false;
        _error = null;
        _failed = PiiRedactionStep.None;
        var prompt = _result.Prompt;
        var sender = Sender;
        try
        {
            ClearReply();
            Phase = PiiRedactionPhase.Sending;
            await Hop(_flowOut, ref _hopOut, reverse: false, token);
            SetActive(_flowGuard);
            await MotionWait(TimeSpan.FromMilliseconds(120), token);
            await Hop(_flowIn, ref _hopIn, reverse: false, token);

            Phase = PiiRedactionPhase.Thinking;
            SetActive(_flowModel);
            string raw;
            if (sender is not null)
            {
                raw = await sender(prompt, token) ?? string.Empty;
            }
            else
            {
                await Wait(SimulatedThinking, token);
                raw = SimulatedReply(_result.Entities);
            }
            token.ThrowIfCancellationRequested();

            // The reply comes back to the guard, then streams in three to six words at a time.
            _raw = raw;
            _restoredText = PiiRedactor.RestoreWithRanges(raw, _result.Entities, out _restoredRanges);
            RebuildReplyLayers();
            Phase = PiiRedactionPhase.Streaming;
            SetActive(_flowGuard);
            await Hop(_flowIn, ref _hopIn, reverse: true, token);
            if (ArcMotion.IsFull(this) && _stream is not null)
            {
                foreach (var end in PiiRedactionStreamText.Chunks(raw))
                {
                    _stream.RevealTo(end, fade: true);
                    await Wait(ChunkInterval, token);
                }
                await Wait(PiiRedactionStreamText.ChunkFade, token);
            }
            else
            {
                _stream?.RevealAll();
            }

            // The guard puts the values back: one crossfade to the restored text, then the dot carries it to the person.
            Phase = PiiRedactionPhase.Restoring;
            await MotionWait(RestoreHold, token);
            await Hop(_flowOut, ref _hopOut, reverse: true, token);
            SetActive(null);
            Reply = _restoredText;
            Phase = PiiRedactionPhase.Done;
            RaiseEvent(new PiiReplyRestoredEventArgs(ReplyRestoredEvent, raw, _restoredText, sender is null));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Fail(PiiRedactionStep.Send, ex);
            }
        }
    }

    private void Fail(PiiRedactionStep step, Exception ex)
    {
        FinishTints();
        HideDots();
        SetActive(null);
        _failed = step;
        _error = ex.Message;
        if (step == PiiRedactionStep.Scan)
        {
            _result = null;
            ResetCells();
            RebuildMessageLayers();
        }
        else
        {
            ClearReply();
        }
        Phase = PiiRedactionPhase.Error;
    }

    internal void StopCore()
    {
        var phase = _phase;
        if (!IsWorking(phase))
        {
            return;
        }
        _run?.Cancel();
        FinishTints();
        HideDots();
        SetActive(null);
        _stopped = true;
        if (phase == PiiRedactionPhase.Scanning)
        {
            _result = null;
            SafePrompt = null;
            ResetCells();
            RebuildMessageLayers();
            Phase = PiiRedactionPhase.Idle;
        }
        else
        {
            ClearReply();
            Phase = PiiRedactionPhase.Redacted;
        }
        Refresh(instant: false);
    }

    internal void ResetCore(bool focus)
    {
        _run?.Cancel();
        FinishTints();
        HideDots();
        SetActive(null);
        _stopped = false;
        _error = null;
        _failed = PiiRedactionStep.None;
        _result = null;
        SafePrompt = null;
        ResetCells();
        ClearReply();
        RebuildMessageLayers();
        SetCurrentValue(ShowOriginalProperty, false);
        Phase = PiiRedactionPhase.Idle;
        Refresh(instant: false);
        if (focus)
        {
            _editor?.Focus();
        }
    }

    private void ResetCells()
    {
        for (var i = 0; i < 4; i++)
        {
            _cellStates[i] = PiiRedactionCellState.NotChecked;
        }
        RefreshCells();
    }

    private void ClearReply()
    {
        _raw = null;
        _restoredText = null;
        _restoredRanges = [];
        Reply = null;
        RebuildReplyLayers();
    }

    private void SetActive(PiiRedactionFlowNode? node)
    {
        foreach (var n in new[] { _flowYou, _flowGuard, _flowModel })
        {
            if (n is not null)
            {
                n.IsActive = ReferenceEquals(n, node);
            }
        }
    }

    private void HideDots()
    {
        _hopOut?.Stop();
        _hopIn?.Stop();
        foreach (var c in new[] { _flowOut, _flowIn })
        {
            if (c is not null)
            {
                c.IsDotVisible = false;
            }
        }
    }

    /// <summary>
    /// One hop: a dot travels the connector in 460ms on an ease in out curve. Code-driven because the dot belongs to the
    /// running step (awaited, cancelled with it), which a ControlTheme transition cannot express. Reduced motion and Off: no travel.
    /// </summary>
    private Task Hop(PiiRedactionConnector? connector, ref MotionValue<double>? motion, bool reverse, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (connector is null || !ArcMotion.IsFull(this))
        {
            return Task.CompletedTask;
        }
        if (motion is null)
        {
            var target = connector;
            motion = new MotionValue<double>(connector, 0, MotionVectors.Double);
            motion.Changed += value => target.Progress = value;
        }
        connector.IsReversed = reverse;
        motion.Jump(0);
        connector.Progress = 0;
        connector.IsDotVisible = true;
        return Travel(connector, motion, token);
    }

    private static async Task Travel(PiiRedactionConnector connector, MotionValue<double> motion, CancellationToken token)
    {
        await motion.AnimateTo(1, MotionSpec.Tween(HopDuration, ArcEasings.InOut));
        connector.IsDotVisible = false;
        token.ThrowIfCancellationRequested();
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        TakeOver();
        switch (_phase)
        {
            case PiiRedactionPhase.Idle:
                _ = ScanCore();
                break;
            case PiiRedactionPhase.Redacted:
                _ = SendCore();
                break;
            case PiiRedactionPhase.Done:
                ResetCore(focus: false);
                _ = ScanCore();
                break;
            case PiiRedactionPhase.Error:
                _ = _failed == PiiRedactionStep.Send ? SendCore() : ScanCore();
                break;
            default:
                StopCore();
                break;
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        TakeOver();
        ResetCore(focus: true);
    }

    private void OnEditorScanRequested(object? sender, EventArgs e)
    {
        TakeOver();
        if (ShowsEditor)
        {
            _primary?.Focus();
            _ = ScanCore();
        }
    }

    private void OnEditorEdited(object? sender, EventArgs e)
    {
        if (_editor is null || !ShowsEditor)
        {
            return;
        }
        TakeOver();
        _editingFromField = true;
        try
        {
            SetCurrentValue(MessageProperty, _editor.Text ?? string.Empty);
            _original?.SetCurrentValue(Avalonia.Controls.TextBlock.TextProperty, _editor.Text ?? string.Empty);
        }
        finally
        {
            _editingFromField = false;
        }
    }

    /// <summary>Someone interacted: the autoplay stops for good, and whatever it was doing stays as it is.</summary>
    private void TakeOver()
    {
        if (_tookOver)
        {
            return;
        }
        _tookOver = true;
        _auto?.Cancel();
        _auto = null;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport?.Dispose();
        _viewport = new ViewportVisibility(this, threshold: 0.25, once: false);
        _viewport.VisibilityChanged += OnViewportChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportChanged;
            _viewport.Dispose();
            _viewport = null;
        }
        _onScreen = false;
        StopAutoPlay();
    }

    private void OnViewportChanged(object? sender, bool visible)
    {
        _onScreen = visible;
        UpdateAutoPlay();
    }

    /// <summary>Starts or stops the loop: it runs while autoplay is on, nobody has interacted, a quarter is on screen and motion is full.</summary>
    private void UpdateAutoPlay()
    {
        var wanted = AutoPlay && !_tookOver && _onScreen && ArcMotion.IsFull(this) && EffectiveMessage.Trim().Length > 0;
        if (wanted && _auto is null)
        {
            _auto = new CancellationTokenSource();
            _ = AutoPlayLoop(_auto.Token);
        }
        else if (!wanted)
        {
            StopAutoPlay();
        }
    }

    private void StopAutoPlay()
    {
        if (_auto is null)
        {
            return;
        }
        _auto.Cancel();
        _auto = null;
        if (!_tookOver)
        {
            // Off screen or switched off before anyone touched it: start over quietly next time.
            ResetCore(focus: false);
        }
    }

    private async Task AutoPlayLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && ArcMotion.IsFull(this))
            {
                if (_phase != PiiRedactionPhase.Idle)
                {
                    ResetCore(focus: false);
                }
                await Delay(AutoPlayLead, token);
                await ScanCore();
                if (token.IsCancellationRequested || _phase != PiiRedactionPhase.Redacted)
                {
                    break;
                }
                await Delay(AutoPlayBeat, token);
                await SendCore();
                if (token.IsCancellationRequested || _phase != PiiRedactionPhase.Done)
                {
                    break;
                }
                await Delay(AutoPlayRest, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_auto?.Token == token)
            {
                _auto = null;
            }
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class MeetingSummary
{
    /// <summary>The summarizing pass the card plays on its own: about one second.</summary>
    internal static readonly TimeSpan PassDuration = TimeSpan.FromMilliseconds(1000);

    /// <summary>The hover intent before a quote opens (later lines switch at once while one is open).</summary>
    internal static readonly TimeSpan HoverIntent = TimeSpan.FromMilliseconds(300);

    /// <summary>The grace before a hover quote closes, so moving between lines does not flicker.</summary>
    internal static readonly TimeSpan CloseGrace = TimeSpan.FromMilliseconds(120);

    /// <summary>How long a simulated send takes.</summary>
    internal static readonly TimeSpan SimulatedSend = TimeSpan.FromMilliseconds(900);

    /// <summary>A simulation's moves after the notes are in: tick one item off, then send another.</summary>
    internal static readonly TimeSpan SampleCheckDelay = TimeSpan.FromMilliseconds(900);

    internal static readonly TimeSpan SampleSendDelay = TimeSpan.FromMilliseconds(800);

    private readonly List<MeetingSummaryLineView> _summaryRows = [];
    private readonly List<MeetingSummaryLineView> _decisionRows = [];
    private readonly List<MeetingSummaryActionRow> _actionRows = [];
    private readonly List<MeetingSummaryRow> _rows = [];
    private readonly HashSet<string> _done = [];
    private readonly HashSet<string> _seeded = [];
    private readonly Dictionary<(string Item, string Target), SendRecord> _sends = [];
    private readonly MeetingSummaryStream _stream = new();
    private CancellationTokenSource _lifetime = new();
    private MeetingSummaryStatus _ownStatus = MeetingSummaryStatus.Summarizing;
    private string? _ownError;
    private MeetingSummaryData? _snapshot;
    private ViewportVisibility? _visibility;
    private IDisposable? _passTimer;
    private IDisposable? _tickTimer;
    private IDisposable? _sampleTimer;
    private IDisposable? _hoverTimer;
    private IDisposable? _closeTimer;
    private bool _started;
    private bool _interacted;
    private bool _sampleMovesPlayed;
    private bool _streamSession;
    private bool _summarizerRunning;
    private int _simulatedCount;
    private ArcPopup? _popup;
    private MeetingSummaryQuoteCard? _quoteCard;
    private MeetingSummaryRow? _quoteRow;
    private bool _pinned;
    private TopLevel? _dismissTop;

    private sealed record SendRecord(MeetingSummarySendState State, MeetingSummarySendResult? Result);

    /// <summary>The timers (tests replace them).</summary>
    internal IMeetingSummaryTimers Timers { get; set; } = MeetingSummaryDispatcherTimers.Instance;

    /// <summary>Every note line in order: summary, decisions, action items.</summary>
    internal IReadOnlyList<MeetingSummaryRow> Rows => _rows;

    /// <summary>The action rows.</summary>
    internal IReadOnlyList<MeetingSummaryActionRow> ActionRows => _actionRows;

    /// <summary>True while the notes stream in.</summary>
    internal bool IsStreaming => _streamSession;

    /// <summary>The stream's pacing.</summary>
    internal MeetingSummaryStream Stream => _stream;

    /// <summary>The quote popup (created on first use).</summary>
    internal ArcPopup? QuotePopup => _popup;

    /// <summary>The quote card.</summary>
    internal MeetingSummaryQuoteCard? QuoteCard => _quoteCard;

    /// <summary>The line whose quote is open.</summary>
    internal MeetingSummaryRow? QuotedRow => _quoteRow;

    /// <summary>Whether the open quote is pinned.</summary>
    internal bool IsQuotePinned => _pinned;

    /// <summary>Whether the action item is checked off on this card.</summary>
    internal bool IsDone(MeetingActionItem item) => _done.Contains(item.Id);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_lifetime.IsCancellationRequested)
        {
            _lifetime = new CancellationTokenSource();
        }
        Arm();
    }

    /// <summary>
    /// Before the first pass: without <see cref="AutoPlay"/> (or with no notes at all) the card starts ready; otherwise it
    /// waits to scroll into view.
    /// </summary>
    private void Arm()
    {
        _visibility?.Dispose();
        _visibility = null;
        if (!_started)
        {
            if (Status is null && (!AutoPlay || !HasNotesSource))
            {
                _started = true;
                _ownStatus = MeetingSummaryStatus.Ready;
                SyncRows(stream: false);
                Refresh();
                return;
            }
            _visibility = new ViewportVisibility(this, 0.25, once: true);
            _visibility.VisibilityChanged += OnFirstSeen;
        }
    }

    /// <summary>
    /// A new <see cref="Simulation"/> or <see cref="Summarizer"/> (or one taken away): the card starts over as if new, with
    /// its pass when it next scrolls into view.
    /// </summary>
    private void OnSourceChanged()
    {
        CancelWork();
        _done.Clear();
        _seeded.Clear();
        _sends.Clear();
        _snapshot = null;
        _sampleMovesPlayed = false;
        _simulatedCount = 0;
        _started = false;
        _ownError = null;
        _ownStatus = MeetingSummaryStatus.Summarizing;
        ClearRows();
        SyncRows(stream: false);
        Refresh();
        if (VisualRoot is not null)
        {
            Arm();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        if (Status is null && _ownStatus == MeetingSummaryStatus.Summarizing)
        {
            // A pass cut short plays again when the card is next on screen.
            _started = false;
        }
        CancelWork();
        _lifetime.Cancel();
    }

    private void OnFirstSeen(object? sender, bool visible)
    {
        if (!visible || _started)
        {
            return;
        }
        _started = true;
        if (Status is null)
        {
            StartPass();
        }
    }

    /// <summary>The card's own pass: summarizing for about a second (or until the summarizer's first snapshot), then ready.</summary>
    private void StartPass()
    {
        _ownError = null;
        SetOwnStatus(MeetingSummaryStatus.Summarizing);
        Announce(Labels.AnnounceSummarizing);
        if (Summarizer is { } summarizer)
        {
            RunSummarizer(summarizer, _lifetime.Token);
            return;
        }
        _passTimer = Timers.Start(PassDuration, () =>
        {
            _passTimer = null;
            SetOwnStatus(MeetingSummaryStatus.Ready);
        });
    }

    private async void RunSummarizer(Func<CancellationToken, IAsyncEnumerable<MeetingSummaryData>> summarizer, CancellationToken token)
    {
        var first = true;
        _summarizerRunning = true;
        try
        {
            await foreach (var snapshot in summarizer(token).WithCancellation(token))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                _snapshot = snapshot;
                if (first)
                {
                    first = false;
                    SetOwnStatus(MeetingSummaryStatus.Ready);
                }
                else
                {
                    OnNotesChanged();
                }
            }
            _summarizerRunning = false;
            if (first)
            {
                SetOwnStatus(MeetingSummaryStatus.Ready);
            }
            else if (!_streamSession)
            {
                AnnounceReady();
                Refresh();
            }
            else if (_stream.IsDone)
            {
                FinishStream();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _summarizerRunning = false;
        }
        catch (Exception ex)
        {
            _summarizerRunning = false;
            if (token.IsCancellationRequested)
            {
                return;
            }
            StopStream(finish: true);
            _ownError = ex.Message;
            SetOwnStatus(MeetingSummaryStatus.Error);
        }
    }

    private void SetOwnStatus(MeetingSummaryStatus status)
    {
        var before = EffectiveStatus;
        _ownStatus = status;
        if (Status is null)
        {
            OnStatusChanged(before);
        }
    }

    private void OnStatusChanged(MeetingSummaryStatus before)
    {
        var status = EffectiveStatus;
        switch (status)
        {
            case MeetingSummaryStatus.Summarizing:
                StopStream(finish: true);
                CloseQuote();
                if (before != MeetingSummaryStatus.Summarizing && Status is not null)
                {
                    Announce(Labels.AnnounceSummarizing);
                }
                break;
            case MeetingSummaryStatus.Ready when before == MeetingSummaryStatus.Summarizing:
                BeginReady(StreamIn);
                break;
            case MeetingSummaryStatus.Ready:
                SyncRows(stream: false);
                break;
            default:
                StopStream(finish: true);
                CloseQuote();
                Announce(Labels.AnnounceError);
                break;
        }
        Refresh();
    }

    private void OnMeetingChanged()
    {
        CloseQuote();
        OnNotesChanged();
    }

    /// <summary>New notes (a new <see cref="Meeting"/> or a summarizer snapshot): rows follow, streaming ones keep streaming.</summary>
    private void OnNotesChanged()
    {
        SyncRows(stream: _streamSession);
        if (_streamSession && _tickTimer is null && !_stream.IsDone)
        {
            ScheduleTick();
        }
        Refresh();
    }

    /// <summary>The notes arrived: stream them in (full motion, StreamIn, loaded) or show them whole.</summary>
    private void BeginReady(bool wantsStream)
    {
        var data = EffectiveMeeting;
        var streams = wantsStream && IsLoaded && ArcMotion.IsFull(this) && !data.IsEmpty;
        SyncRows(stream: false);
        if (!streams)
        {
            if (!_summarizerRunning)
            {
                AnnounceReady();
                ScheduleSampleMoves();
            }
            return;
        }
        _streamSession = true;
        _stream.Start(_rows.Select(r => r.Point.Text).ToList());
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].SetText(string.Empty);
            _rows[i].IsStreaming = !_stream.IsLineDone(i);
        }
        ScheduleTick();
    }

    private void ScheduleTick() => _tickTimer = Timers.Start(MeetingSummaryStream.Interval, StreamTick);

    /// <summary>One chunk: 3 to 8 words fade in; a line that is whole lets its timestamp, owner and buttons fade in.</summary>
    internal void StreamTick()
    {
        _tickTimer = null;
        if (!_streamSession)
        {
            return;
        }
        foreach (var (line, text) in _stream.Tick())
        {
            if (line < _rows.Count)
            {
                _rows[line].Append(text, fade: true);
            }
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            var streaming = !_stream.IsLineDone(i);
            if (_rows[i].IsStreaming != streaming)
            {
                _rows[i].IsStreaming = streaming;
            }
        }
        if (!_stream.IsDone)
        {
            ScheduleTick();
        }
        else if (!_summarizerRunning)
        {
            FinishStream();
        }
    }

    private void FinishStream()
    {
        StopStream(finish: true);
        AnnounceReady();
        Refresh();
        ScheduleSampleMoves();
    }

    /// <summary>Ends a stream; <paramref name="finish"/> shows every line whole.</summary>
    private void StopStream(bool finish)
    {
        _tickTimer?.Dispose();
        _tickTimer = null;
        if (!_streamSession)
        {
            return;
        }
        _streamSession = false;
        if (finish)
        {
            _stream.Finish();
            foreach (var row in _rows)
            {
                row.SetText(row.Point.Text);
                row.IsStreaming = false;
            }
        }
    }

    private void AnnounceReady()
    {
        var data = EffectiveMeeting;
        Announce(data.IsEmpty
            ? Labels.AnnounceEmpty
            : Labels.AnnounceReady(data.Summary.Count, data.Decisions.Count, data.ActionItems.Count));
    }

    /// <summary>Stops the pass, the stream, the summarizer, the sample's moves, hover timers and every send in flight.</summary>
    private void CancelWork()
    {
        _passTimer?.Dispose();
        _passTimer = null;
        _sampleTimer?.Dispose();
        _sampleTimer = null;
        StopStream(finish: true);
        CloseQuote(immediate: true);
        _lifetime.Cancel();
        _lifetime = new CancellationTokenSource();
        _summarizerRunning = false;
    }

    // ---------------------------------------------------------------- rows

    private void ClearRows()
    {
        CloseQuote(immediate: true);
        foreach (var row in _rows)
        {
            Unhook(row);
        }
        _summaryList?.Children.Clear();
        _decisionsList?.Children.Clear();
        _actionsList?.Children.Clear();
        _summaryRows.Clear();
        _decisionRows.Clear();
        _actionRows.Clear();
        _rows.Clear();
    }

    /// <summary>
    /// Brings the rows in line with the notes, reusing rows by section and position so a new snapshot never rebuilds a line.
    /// While streaming the text follows the stream; otherwise every line is whole.
    /// </summary>
    private void SyncRows(bool stream)
    {
        if (_summaryList is null || _decisionsList is null || _actionsList is null)
        {
            return;
        }
        var data = EffectiveMeeting;
        var labels = Labels;
        var targets = SendTargets ?? [];
        var compact = PseudoClasses.Contains(CompactPseudoClass);
        SyncLines(_summaryList, _summaryRows, data.Summary, decision: false, labels);
        SyncLines(_decisionsList, _decisionRows, data.Decisions, decision: true, labels);
        for (var i = 0; i < data.ActionItems.Count; i++)
        {
            var item = data.ActionItems[i];
            if (_seeded.Add(item.Id) && item.IsDone)
            {
                _done.Add(item.Id);
            }
            MeetingSummaryActionRow row;
            if (i < _actionRows.Count)
            {
                row = _actionRows[i];
            }
            else
            {
                row = new MeetingSummaryActionRow(item, labels);
                Hook(row);
                row.CheckRequested += OnCheckRequested;
                row.SendPressed += OnSendPressed;
                _actionRows.Add(row);
                _actionsList.Children.Add(row);
            }
            row.Configure(item, data.Attendee(item.OwnerId), targets, labels);
            row.SetCompact(compact);
            var done = _done.Contains(item.Id);
            if (row.IsDone != done || row.Check?.IsChecked != done)
            {
                row.SetDone(done, animate: false);
            }
            foreach (var send in row.Sends)
            {
                var record = _sends.GetValueOrDefault((item.Id, send.Target.Id));
                var state = record?.State ?? MeetingSummarySendState.Idle;
                if (send.State != state || !ReferenceEquals(send.Result, record?.Result))
                {
                    send.SetState(state, record?.Result);
                }
            }
        }
        while (_actionRows.Count > data.ActionItems.Count)
        {
            var row = _actionRows[^1];
            Unhook(row);
            row.CheckRequested -= OnCheckRequested;
            row.SendPressed -= OnSendPressed;
            _actionRows.RemoveAt(_actionRows.Count - 1);
            _actionsList.Children.Remove(row);
        }

        _rows.Clear();
        _rows.AddRange(_summaryRows);
        _rows.AddRange(_decisionRows);
        _rows.AddRange(_actionRows);
        if (_quoteRow is not null && !_rows.Contains(_quoteRow))
        {
            CloseQuote(immediate: true);
        }

        if (stream)
        {
            _stream.Retarget(_rows.Select(r => r.Point.Text).ToList());
            for (var i = 0; i < _rows.Count; i++)
            {
                var shown = _stream.ShownText(i);
                if (_rows[i].ShownText != shown)
                {
                    _rows[i].SetText(shown);
                }
                _rows[i].IsStreaming = !_stream.IsLineDone(i);
            }
        }
        else
        {
            foreach (var row in _rows)
            {
                row.SetText(row.Point.Text);
                row.IsStreaming = false;
            }
        }
    }

    private void SyncLines(Panel list, List<MeetingSummaryLineView> rows, List<MeetingSummaryPoint> points, bool decision,
        MeetingSummaryLabels labels)
    {
        for (var i = 0; i < points.Count; i++)
        {
            if (i < rows.Count)
            {
                rows[i].SetPoint(points[i], labels);
                continue;
            }
            var row = new MeetingSummaryLineView(points[i], decision, labels);
            Hook(row);
            rows.Add(row);
            list.Children.Add(row);
        }
        while (rows.Count > points.Count)
        {
            var row = rows[^1];
            Unhook(row);
            rows.RemoveAt(rows.Count - 1);
            list.Children.Remove(row);
        }
    }

    private void Hook(MeetingSummaryRow row)
    {
        row.PointerEntered += OnRowPointerEntered;
        row.PointerExited += OnRowPointerExited;
        row.AddHandler(AvaloniaButton.ClickEvent, OnRowClick);
        row.AddHandler(GotFocusEvent, OnRowGotFocus);
        row.AddHandler(LostFocusEvent, OnRowLostFocus);
    }

    private void Unhook(MeetingSummaryRow row)
    {
        row.PointerEntered -= OnRowPointerEntered;
        row.PointerExited -= OnRowPointerExited;
        row.RemoveHandler(AvaloniaButton.ClickEvent, OnRowClick);
        row.RemoveHandler(GotFocusEvent, OnRowGotFocus);
        row.RemoveHandler(LostFocusEvent, OnRowLostFocus);
    }

    // ---------------------------------------------------------------- quotes

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        // Hover quotes need a fine pointer; on touch a tap on the timestamp pins the quote.
        if (sender is not MeetingSummaryRow row || e.Pointer.Type == PointerType.Touch || row.IsStreaming)
        {
            return;
        }
        HoverRow(row);
    }

    /// <summary>The pointer is over <paramref name="row"/>: open its quote after the hover intent, or at once while another is open.</summary>
    internal void HoverRow(MeetingSummaryRow row)
    {
        if (_pinned)
        {
            return;
        }
        _hoverTimer?.Dispose();
        _hoverTimer = null;
        if (row.Point.Quote is null)
        {
            return;
        }
        _closeTimer?.Dispose();
        _closeTimer = null;
        if (_quoteRow is not null)
        {
            ShowQuote(row, pin: false);
            return;
        }
        _hoverTimer = Timers.Start(HoverIntent, () =>
        {
            _hoverTimer = null;
            ShowQuote(row, pin: false);
        });
    }

    private void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not MeetingSummaryRow row || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        LeaveRow(row);
    }

    /// <summary>The pointer left <paramref name="row"/>: an unpinned quote closes after a short grace.</summary>
    internal void LeaveRow(MeetingSummaryRow row)
    {
        _hoverTimer?.Dispose();
        _hoverTimer = null;
        if (_pinned || !ReferenceEquals(_quoteRow, row) || (row.Stamp?.IsKeyboardFocusWithin ?? false))
        {
            return;
        }
        _closeTimer?.Dispose();
        _closeTimer = Timers.Start(CloseGrace, () =>
        {
            _closeTimer = null;
            if (!_pinned)
            {
                CloseQuote();
            }
        });
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MeetingSummaryRow row || e.Source is not MeetingSummaryStamp)
        {
            return;
        }
        _interacted = true;
        if (ReferenceEquals(_quoteRow, row) && _pinned)
        {
            CloseQuote();
        }
        else
        {
            ShowQuote(row, pin: true);
        }
        e.Handled = true;
    }

    private void OnRowGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is MeetingSummaryRow row && e.Source is MeetingSummaryStamp
            && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional && !_pinned)
        {
            ShowQuote(row, pin: false);
        }
    }

    private void OnRowLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is MeetingSummaryRow row && e.Source is MeetingSummaryStamp && ReferenceEquals(_quoteRow, row) && !_pinned
            && !row.IsPointerOver)
        {
            CloseQuote();
        }
    }

    /// <summary>
    /// Opens the quote of <paramref name="row"/> under its line (it flips above when the window runs out of room). While a
    /// quote is open, another line's quote replaces it at once, without a second fade.
    /// </summary>
    internal void ShowQuote(MeetingSummaryRow row, bool pin)
    {
        var quote = row.Point.Quote;
        if (quote is null || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _hoverTimer?.Dispose();
        _hoverTimer = null;
        _closeTimer?.Dispose();
        _closeTimer = null;
        EnsurePopup();
        var speaker = EffectiveMeeting.Attendee(quote.SpeakerId);
        var card = _quoteCard!;
        card.Speaker = speaker?.Name ?? Labels.UnknownSpeaker;
        card.Role = speaker?.Role;
        card.Portrait = speaker?.Portrait;
        card.Time = MeetingSummaryFormat.Timestamp(quote.At);
        card.Quote = "“" + quote.Text + "”";
        if (!ReferenceEquals(_quoteRow, row))
        {
            if (_quoteRow is not null)
            {
                _quoteRow.IsQuoted = false;
            }
            row.IsQuoted = true;
            _quoteRow = row;
        }
        SetPinned(pin);
        var popup = _popup!;
        popup.PlacementTarget = row;
        popup.MarginToAnchor = QuoteGap;
        popup.EnterOffset = QuoteEnterOffset;
        if (popup.IsOpen)
        {
            popup.Reposition();
            return;
        }
        popup.ClearValue(ArcPopup.IsMotionEnabledProperty);
        if (popup.Parent is null)
        {
            ((ISetLogicalParent)popup).SetParent(this);
        }
        popup.IsOpen = true;
    }

    /// <summary>Closes the open quote.</summary>
    internal void CloseQuote(bool immediate = false)
    {
        _hoverTimer?.Dispose();
        _hoverTimer = null;
        _closeTimer?.Dispose();
        _closeTimer = null;
        SetPinned(false);
        if (_quoteRow is not null)
        {
            _quoteRow.IsQuoted = false;
            _quoteRow = null;
        }
        if (_popup is { IsOpen: true } popup)
        {
            if (immediate)
            {
                popup.IsMotionEnabled = false;
            }
            popup.IsOpen = false;
        }
    }

    private void SetPinned(bool pinned)
    {
        _pinned = pinned;
        var top = pinned ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(top, _dismissTop))
        {
            return;
        }
        _dismissTop?.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _dismissTop = top;
        _dismissTop?.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        _quoteCard = new MeetingSummaryQuoteCard();
        _popup = new ArcPopup
        {
            Child = _quoteCard,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = false,
            EnterOffset = QuoteEnterOffset,
            EnterScale = 1,
            MarginToAnchor = QuoteGap,
            TakesFocusFromNativeControl = false,
            WindowManagerAddShadowHint = false,
        };
        _popup.Closed += (_, _) =>
        {
            if (_popup is { IsOpen: false } closed && closed.Parent is not null)
            {
                ((ISetLogicalParent)closed).SetParent(null);
            }
        };
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _quoteRow is not null)
        {
            CloseQuote();
            e.Handled = true;
        }
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e) => DismissPinned(e.Source);

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e) => DismissPinned(e.Source);

    /// <summary>A press anywhere but a timestamp closes a pinned quote.</summary>
    private void DismissPinned(object? source)
    {
        if (!_pinned)
        {
            return;
        }
        if (source is Visual visual && (visual is MeetingSummaryStamp || visual.FindAncestorOfType<MeetingSummaryStamp>() is not null))
        {
            return;
        }
        CloseQuote();
    }

    // ---------------------------------------------------------------- checks and sends

    private void OnCheckRequested(object? sender, EventArgs e)
    {
        if (sender is not MeetingSummaryActionRow row)
        {
            return;
        }
        _interacted = true;
        Toggle(row, raise: true);
    }

    private void Toggle(MeetingSummaryActionRow row, bool raise)
    {
        var item = row.Item;
        var done = !_done.Contains(item.Id);
        if (done)
        {
            _done.Add(item.Id);
        }
        else
        {
            _done.Remove(item.Id);
        }
        row.SetDone(done, animate: true);
        if (raise)
        {
            RaiseEvent(new MeetingActionItemChangedEventArgs(ActionItemChangedEvent, item, done));
        }
        Announce(Labels.AnnounceChecked(item.Text, done));
        UpdateCopy();
    }

    private void UpdateCopy()
    {
        if (_copy is not null && _copy.IsEnabled)
        {
            _copy.Value = MeetingSummaryFormat.PlainText(EffectiveMeeting, Labels, IsDone);
        }
    }

    private void OnSendPressed(object? sender, MeetingSummarySendButton button)
    {
        if (sender is not MeetingSummaryActionRow row)
        {
            return;
        }
        _interacted = true;
        if (button.State == MeetingSummarySendState.Sent)
        {
            if (button.Result?.Url is { } url && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(url);
            }
            return;
        }
        Send(row, button);
    }

    /// <summary>
    /// Sends the row's item to the button's target: raises <see cref="SendRequested"/>, awaits the handler's task (or
    /// simulates one), then shows the sent chip or Retry. Removal and regenerate cancel it, which returns the button to
    /// its mark.
    /// </summary>
    private async void Send(MeetingSummaryActionRow row, MeetingSummarySendButton button)
    {
        if (button.State == MeetingSummarySendState.Sending)
        {
            return;
        }
        var item = row.Item;
        var target = button.Target;
        var key = (item.Id, target.Id);
        var token = _lifetime.Token;
        SetSend(key, button, MeetingSummarySendState.Sending, null);
        var args = new MeetingSummarySendEventArgs(SendRequestedEvent, item, target, token);
        RaiseEvent(args);
        var simulated = args.Completion is null;
        var task = args.Completion ?? Simulate(target, token);
        try
        {
            var result = await task;
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (simulated && result is null && PlayingSimulation is { } simulation)
            {
                var number = simulation.FirstReferenceNumber + _simulatedCount++;
                result = new MeetingSummarySendResult(simulation.Reference(target, number));
            }
            SetSend(key, FindButton(key) ?? button, MeetingSummarySendState.Sent, result);
            RaiseEvent(new MeetingSummarySentEventArgs(ActionItemSentEvent, item, target, result, simulated));
            var said = Labels.AnnounceSent(target.Name, result?.Reference);
            Announce(simulated ? said + " " + Labels.AnnounceSimulated : said);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            _sends.Remove(key);
            FindButton(key)?.SetState(MeetingSummarySendState.Idle);
        }
        catch (Exception)
        {
            SetSend(key, FindButton(key) ?? button, MeetingSummarySendState.Failed, null);
            Announce(Labels.AnnounceFailed(target.Name));
        }
    }

    private void SetSend((string Item, string Target) key, MeetingSummarySendButton button, MeetingSummarySendState state,
        MeetingSummarySendResult? result)
    {
        _sends[key] = new SendRecord(state, result);
        button.SetState(state, result);
    }

    private MeetingSummarySendButton? FindButton((string Item, string Target) key)
    {
        foreach (var row in _actionRows)
        {
            if (row.Item.Id != key.Item)
            {
                continue;
            }
            foreach (var send in row.Sends)
            {
                if (send.Target.Id == key.Target)
                {
                    return send;
                }
            }
        }
        return null;
    }

    private Task<MeetingSummarySendResult?> Simulate(MeetingSummarySendTarget target, CancellationToken token)
    {
        var done = new TaskCompletionSource<MeetingSummarySendResult?>();
        var timer = Timers.Start(SimulatedSend, () => done.TrySetResult(null));
        token.Register(() =>
        {
            timer.Dispose();
            done.TrySetCanceled(token);
        });
        return done.Task;
    }

    /// <summary>A playing simulation, once and only if nobody has used the card: tick off the second item, then send the first to the first target.</summary>
    private void ScheduleSampleMoves()
    {
        if (!UsesSimulation || Status is not null || !AutoPlay || _interacted || _sampleMovesPlayed || _actionRows.Count < 2)
        {
            return;
        }
        _sampleMovesPlayed = true;
        _sampleTimer = Timers.Start(SampleCheckDelay, () =>
        {
            _sampleTimer = null;
            if (_interacted || _actionRows.Count < 2)
            {
                return;
            }
            if (!_done.Contains(_actionRows[1].Item.Id))
            {
                Toggle(_actionRows[1], raise: false);
            }
            _sampleTimer = Timers.Start(SampleSendDelay, () =>
            {
                _sampleTimer = null;
                if (_interacted || _actionRows.Count == 0 || _actionRows[0].Sends.Count == 0)
                {
                    return;
                }
                Send(_actionRows[0], _actionRows[0].Sends[0]);
            });
        });
    }
}

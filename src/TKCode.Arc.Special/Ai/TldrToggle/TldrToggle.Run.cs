using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Where the result on screen is.</summary>
internal enum TldrTogglePhase
{
    /// <summary>No result: the article (or the empty state) shows.</summary>
    Idle,

    /// <summary>Waiting for the first event: the body's line shimmers.</summary>
    Thinking,

    /// <summary>Text arrives: the footer's Writing label shimmers.</summary>
    Writing,

    /// <summary>The result is complete (and cached).</summary>
    Done,

    /// <summary>Stopped by the reader; what arrived stays.</summary>
    Stopped,

    /// <summary>The summarizer failed: an alert with Try again.</summary>
    Failed
}

/// <summary>A finished result, kept per mode and length.</summary>
internal sealed record TldrToggleResult(string Text, IReadOnlyList<TldrKeyPoint>? Points, int Words);

public partial class TldrToggle
{
    private readonly Dictionary<(TldrMode Mode, int Length), TldrToggleResult> _cache = [];
    private readonly StringBuilder _summary = new();
    private readonly List<(string? Source, StringBuilder Text)> _points = [];
    private CancellationTokenSource? _run;
    private int _runId;
    private (TldrMode Mode, int Length)? _shownKey;
    private TldrTogglePhase _phase;
    private TldrToggleArticleView? _articleView;
    private TldrToggleStreamText? _summaryView;
    private TldrTogglePointList? _pointsView;
    private TldrToggleStatus? _shownStatus;
    private TldrToggleActions? _shownActions;
    private string? _error;
    private MotionValue<double>? _scroll;
    private ScrollViewer? _scrolling;

    /// <summary>Where the result on screen is.</summary>
    internal TldrTogglePhase Phase => _phase;

    internal bool IsRunning => _phase is TldrTogglePhase.Thinking or TldrTogglePhase.Writing;

    /// <summary>How many requests the card has made.</summary>
    internal int RequestCount { get; private set; }

    /// <summary>The finished results kept.</summary>
    internal int CachedCount => _cache.Count;

    /// <summary>The paragraphs of the article view.</summary>
    internal IReadOnlyList<TldrToggleParagraph> ParagraphViews => _articleView?.Paragraphs.ToList() ?? [];

    /// <summary>The key point rows on screen (empty unless a list shows).</summary>
    internal IReadOnlyList<TldrTogglePointRow> PointRows =>
        _body?.Content is TldrTogglePointList list ? list.Rows : [];

    /// <summary>The summary text on screen, or null.</summary>
    internal string? SummaryText => _body?.Content is TldrToggleStreamText text ? text.Text : null;

    internal TldrToggleStatus? StatusView => _shownStatus;

    internal TldrToggleActions? ActionsView => _shownActions;

    /// <summary>The last announcement.</summary>
    internal string? LastAnnouncement { get; private set; }

    /// <summary>The text Copy copies: the summary, or the points one per line.</summary>
    internal string CurrentText => _summaryView is not null && _body?.Content == _summaryView
        ? _summaryView.Text
        : string.Join("\n", _points.Select(p => "• " + p.Text.ToString().Trim()));

    /// <summary>Asks for the current mode and length again, replacing the kept result. Does nothing in Full mode.</summary>
    public void Regenerate()
    {
        if (Mode == TldrMode.Full || DisplayedArticle.Paragraphs.Count == 0)
        {
            return;
        }
        _cache.Remove((Mode, Length));
        StartRun((Mode, Length));
    }

    /// <summary>Stops a running result and keeps what arrived (Escape does the same).</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }
        var nothing = _phase == TldrTogglePhase.Thinking;
        CancelRun();
        SetPhase(TldrTogglePhase.Stopped);
        if (nothing)
        {
            ShowView(new TldrToggleNote { Text = Labels.StoppedEarly });
        }
        UpdateFooter();
        Announce(Labels.Stopped);
    }

    /// <summary>
    /// Switches to the article, waits for the height to settle, scrolls the paragraph <paramref name="paragraphId"/> into
    /// view, focuses it and highlights it, as a key point's jump link does. Raises <see cref="Jumping"/> first.
    /// </summary>
    /// <param name="paragraphId">The paragraph's id.</param>
    public async Task JumpTo(string paragraphId)
    {
        if (_articleView?.Paragraphs.FirstOrDefault(p => p.ParagraphId == paragraphId) is not { } paragraph)
        {
            return;
        }
        RaiseEvent(new TldrJumpEventArgs(JumpingEvent, paragraphId));
        // A jump link clicked with the mouse lands without the keyboard ring.
        var method = SpecialFocusModality.For(this);
        SetMode(TldrMode.Full);
        // Let the swap lay out so the height spring has its new target, then wait for it to rest.
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        for (var i = 0; i < 8 && _bodyBox is { IsMoving: true } box; i++)
        {
            await box.WhenSettled();
        }
        if (Mode != TldrMode.Full || !paragraph.IsAttachedToVisualTree())
        {
            return;
        }
        await ScrollIntoView(paragraph);
        paragraph.Focus(method);
        await paragraph.Highlight();
    }

    /// <summary>Drops the kept results and any run (a new article or summarizer).</summary>
    private void ResetResults()
    {
        CancelRun();
        _cache.Clear();
        _shownKey = null;
        SetPhase(TldrTogglePhase.Idle);
    }

    /// <summary>Shows what Mode and Length ask for: the article, a kept result, or a new run.</summary>
    private void Sync()
    {
        if (_body is null)
        {
            return;
        }
        var article = DisplayedArticle;
        if (article.Paragraphs.Count == 0)
        {
            CancelRun();
            _shownKey = null;
            SetPhase(TldrTogglePhase.Idle);
            ShowView(new TldrToggleNote { Text = Labels.Empty });
            UpdateFooter();
            return;
        }
        if (Mode == TldrMode.Full)
        {
            CancelRun();
            _shownKey = null;
            SetPhase(TldrTogglePhase.Idle);
            if (_articleView is not null)
            {
                ShowView(_articleView);
            }
            UpdateFooter();
            return;
        }
        var key = (Mode, Length);
        if (_shownKey == key)
        {
            return;
        }
        if (_cache.TryGetValue(key, out var result))
        {
            CancelRun();
            ShowResult(key, result);
            return;
        }
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        StartRun(key);
    }

    private void SetPhase(TldrTogglePhase phase)
    {
        _phase = phase;
        PseudoClasses.Set(BusyPseudoClass, IsRunning);
    }

    private void CancelRun()
    {
        _runId++;
        _run?.Cancel();
        _run = null;
    }

    private void StartRun((TldrMode Mode, int Length) key)
    {
        CancelRun();
        var run = new CancellationTokenSource();
        _run = run;
        var id = _runId;
        _shownKey = key;
        _summary.Clear();
        _points.Clear();
        _summaryView = null;
        _pointsView = null;
        _error = null;
        SetPhase(TldrTogglePhase.Thinking);
        ShowView(new TldrToggleThinking { Text = Labels.Reading });
        UpdateFooter();
        Announce(Labels.Summarizing(DisplayedModel.Name));
        RequestCount++;
        var request = new TldrRequest(key.Mode, key.Length, DisplayedArticle, run.Token);
        _ = RunAsync(id, run, request);
    }

    private async Task RunAsync(int id, CancellationTokenSource run, TldrRequest request)
    {
        try
        {
            var stream = PlayingSimulation is { } simulation
                ? simulation.Summarize(request)
                : Summarizer is { } summarizer ? summarizer(request) : TldrStreams.Simulate(request);
            await foreach (var item in stream.WithCancellation(run.Token).ConfigureAwait(true))
            {
                if (id != _runId)
                {
                    return;
                }
                Apply(request.Mode, item);
            }
            if (id == _runId)
            {
                Finish(request);
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested || id != _runId)
        {
            // Stopped, switched or detached: whoever cancelled has already updated the card.
        }
        catch (Exception exception)
        {
            if (id == _runId)
            {
                Fail(exception.Message);
            }
        }
        finally
        {
            run.Dispose();
        }
    }

    /// <summary>Applies one event: the first one swaps the thinking line for the result, text fades in chunk by chunk.</summary>
    private void Apply(TldrMode mode, TldrStreamEvent item)
    {
        if (_phase == TldrTogglePhase.Thinking)
        {
            SetPhase(TldrTogglePhase.Writing);
            if (mode == TldrMode.KeyPoints)
            {
                _pointsView = new TldrTogglePointList();
                ShowView(_pointsView);
            }
            else
            {
                _summaryView = new TldrToggleStreamText();
                ShowView(_summaryView);
            }
            UpdateFooter();
        }
        if (mode == TldrMode.KeyPoints)
        {
            if (item.Kind == TldrStreamEventKind.Point)
            {
                TickLastPoint();
                AddPoint(item.Source);
            }
            else if (!string.IsNullOrEmpty(item.Text))
            {
                if (_points.Count == 0)
                {
                    AddPoint(null);
                }
                _points[^1].Text.Append(item.Text);
                _pointsView?.Rows[^1].Append(item.Text, fade: true);
            }
            return;
        }
        if (item.Kind == TldrStreamEventKind.Text && !string.IsNullOrEmpty(item.Text))
        {
            _summary.Append(item.Text);
            _summaryView?.Append(item.Text, fade: true);
        }
    }

    private void AddPoint(string? source)
    {
        _points.Add((source, new StringBuilder()));
        _pointsView?.Children.Add(CreateRow(source, string.Empty, done: false));
    }

    private void TickLastPoint()
    {
        if (_pointsView?.Rows is { Count: > 0 } rows)
        {
            rows[^1].IsDone = true;
        }
    }

    private TldrTogglePointRow CreateRow(string? source, string text, bool done)
    {
        var number = 0;
        var paragraphs = DisplayedArticle.Paragraphs;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (source is not null && paragraphs[i].Id == source)
            {
                number = i + 1;
                break;
            }
        }
        var row = new TldrTogglePointRow { Source = number > 0 ? source : null, Number = number };
        if (number > 0)
        {
            row.LinkTip = Labels.JumpTip(number);
        }
        if (text.Length > 0)
        {
            row.Append(text, fade: false);
        }
        row.IsDone = done;
        row.JumpRequested += OnRowJump;
        if (done)
        {
            Link(row);
        }
        return row;
    }

    /// <summary>Turns on a finished row's jump link, named after its point.</summary>
    private void Link(TldrTogglePointRow row)
    {
        if (row.Source is null)
        {
            return;
        }
        row.LinkName = Labels.JumpTo(row.Number, row.Text.Trim());
        row.IsLinked = true;
    }

    private void OnRowJump(object? sender, EventArgs e)
    {
        if (sender is TldrTogglePointRow { Source: { } source, IsLinked: true })
        {
            _touched = true;
            _ = JumpTo(source);
        }
    }

    private void Finish(TldrRequest request)
    {
        _run = null;
        TldrToggleResult result;
        if (request.Mode == TldrMode.KeyPoints)
        {
            var points = _points.Select(p => new TldrKeyPoint(p.Source, p.Text.ToString().Trim())).ToArray();
            var text = string.Join("\n", points.Select(p => p.Text));
            result = new TldrToggleResult(text, points, points.Sum(p => TldrStreams.WordCount(p.Text)));
            if (_pointsView is not null)
            {
                foreach (var row in _pointsView.Rows)
                {
                    row.IsDone = true;
                    Link(row);
                }
            }
        }
        else
        {
            var text = _summary.ToString().Trim();
            result = new TldrToggleResult(text, null, TldrStreams.WordCount(text));
        }
        _cache[(request.Mode, request.Length)] = result;
        SetPhase(TldrTogglePhase.Done);
        if (_summaryView is null && _pointsView is null)
        {
            // An empty stream still ends in a result view.
            ShowResult((request.Mode, request.Length), result);
        }
        UpdateFooter();
        Announce(result.Points is { } list ? Labels.PointsReady(list.Count) : Labels.SummaryReady(result.Words));
        RaiseEvent(new TldrCompletedEventArgs(CompletedEvent, request.Mode, request.Length, result.Text, result.Points, result.Words));
    }

    private void Fail(string? message)
    {
        _run = null;
        var text = string.IsNullOrWhiteSpace(message) ? Labels.FailedMessage : message.Trim();
        if (EffectiveSimulated)
        {
            text = text.TrimEnd('.') + ". " + Labels.Simulated + ".";
        }
        _error = text;
        SetPhase(TldrTogglePhase.Failed);
        var alert = new TldrToggleAlert { Message = text, RetryText = Labels.TryAgain };
        alert.RetryRequested += (_, _) =>
        {
            _touched = true;
            Regenerate();
        };
        ShowView(alert);
        UpdateFooter();
        _announcer?.Announce(text);
        LastAnnouncement = text;
    }

    /// <summary>Shows a kept result whole, with no fade.</summary>
    private void ShowResult((TldrMode Mode, int Length) key, TldrToggleResult result)
    {
        _shownKey = key;
        _summary.Clear();
        _points.Clear();
        _summaryView = null;
        _pointsView = null;
        SetPhase(TldrTogglePhase.Done);
        if (result.Points is { } points)
        {
            _pointsView = new TldrTogglePointList();
            foreach (var point in points)
            {
                _points.Add((point.Source, new StringBuilder(point.Text)));
                _pointsView.Children.Add(CreateRow(point.Source, point.Text, done: true));
            }
            ShowView(_pointsView);
        }
        else
        {
            _summary.Append(result.Text);
            _summaryView = new TldrToggleStreamText();
            _summaryView.SetText(result.Text);
            ShowView(_summaryView);
        }
        UpdateFooter();
    }

    private void ShowView(Control view)
    {
        if (_body is not null && !ReferenceEquals(_body.Content, view))
        {
            _body.Content = view;
        }
    }

    /// <summary>The footer: the state (icon and word), the model and the actions; each part crossfades when it changes.</summary>
    private void UpdateFooter()
    {
        var labels = Labels;
        var model = DisplayedModel;
        // With no model to credit (the empty card, or your article without one) the footer names none.
        var credited = !string.IsNullOrWhiteSpace(model.Name);
        if (_modelName is not null)
        {
            _modelName.Text = model.Name;
            _modelName.IsVisible = credited;
        }
        if (_modelMark is not null)
        {
            _modelMark.IsVisible = credited;
            _modelMark.Data = model.Mark ?? ArcIcons.Sparkles;
            if (model.MarkBrush is { } brush)
            {
                _modelMark.Foreground = brush;
            }
            else
            {
                _modelMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        var cached = _shownKey is { } key && _cache.TryGetValue(key, out var kept) ? kept : null;
        var (kind, text) = _phase switch
        {
            TldrTogglePhase.Thinking => (TldrToggleStatusKind.Thinking, labels.Thinking),
            TldrTogglePhase.Writing => (TldrToggleStatusKind.Writing, labels.Writing),
            TldrTogglePhase.Done when cached?.Points is { } points => (TldrToggleStatusKind.Done, labels.Points(points.Count)),
            TldrTogglePhase.Done => (TldrToggleStatusKind.Done, labels.Words(cached?.Words ?? 0)),
            TldrTogglePhase.Stopped => (TldrToggleStatusKind.Stopped, labels.Stopped),
            TldrTogglePhase.Failed => (TldrToggleStatusKind.Failed, labels.Failed),
            _ => (TldrToggleStatusKind.Full, labels.FullArticle),
        };
        if (_state is not null && (_shownStatus is null || _shownStatus.Kind != kind || _shownStatus.Text != text))
        {
            _shownStatus = new TldrToggleStatus(kind, text);
            _state.Content = _shownStatus;
        }
        var actions = _phase switch
        {
            TldrTogglePhase.Thinking or TldrTogglePhase.Writing => TldrToggleActionsKind.Running,
            TldrTogglePhase.Done or TldrTogglePhase.Stopped => TldrToggleActionsKind.Finished,
            _ => TldrToggleActionsKind.None,
        };
        if (_actions is not null && (_shownActions is null || _shownActions.Kind != actions))
        {
            _shownActions = new TldrToggleActions(actions);
            _shownActions.StopRequested += (_, _) => Stop();
            _shownActions.RegenerateRequested += (_, _) => Regenerate();
            _actions.Content = _shownActions;
        }
        if (_shownActions is not null)
        {
            _shownActions.StopText = labels.Stop;
            _shownActions.RegenerateText = labels.Regenerate;
            _shownActions.CopyLabel = labels.Copy;
            _shownActions.CopyText = actions == TldrToggleActionsKind.Finished ? cached?.Text ?? CurrentText : null;
        }
    }

    private void Announce(string text)
    {
        var said = EffectiveSimulated ? text + ". " + Labels.Simulated : text;
        LastAnnouncement = said;
        _announcer?.Announce(said);
    }

    /// <summary>
    /// Scrolls the nearest scroll viewer so the paragraph is in view (centered when it fits), smoothly with full motion.
    /// Code-driven because the distance is the paragraph's measured position, which a ControlTheme cannot express.
    /// </summary>
    private async Task ScrollIntoView(Control paragraph)
    {
        if (paragraph.FindAncestorOfType<ScrollViewer>() is not { } scroll)
        {
            paragraph.BringIntoView();
            return;
        }
        if (paragraph.TranslatePoint(default, scroll) is not { } top)
        {
            return;
        }
        var height = paragraph.Bounds.Height;
        var viewport = scroll.Viewport.Height;
        if (top.Y >= 0 && top.Y + height <= viewport)
        {
            return;
        }
        var offset = scroll.Offset.Y + top.Y - (height < viewport ? (viewport - height) / 2 : 16);
        offset = Math.Clamp(offset, 0, Math.Max(0, scroll.Extent.Height - viewport));
        if (!ArcMotion.IsFull(this))
        {
            scroll.Offset = scroll.Offset.WithY(offset);
            return;
        }
        _scrolling = scroll;
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, scroll.Offset.Y, MotionVectors.Double);
            _scroll.Changed += value =>
            {
                if (_scrolling is { } target)
                {
                    target.Offset = target.Offset.WithY(value);
                }
            };
        }
        _scroll.Jump(scroll.Offset.Y);
        await _scroll.AnimateTo(offset, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.InOut));
    }

    /// <summary>Builds the article view once per article; it is kept so a jump can find its paragraphs.</summary>
    private void BuildArticleView()
    {
        if (_body is null)
        {
            return;
        }
        var view = new TldrToggleArticleView();
        var paragraphs = DisplayedArticle.Paragraphs;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            view.Children.Add(new TldrToggleParagraph
            {
                Text = paragraphs[i].Text,
                ParagraphId = paragraphs[i].Id,
                Number = i + 1,
            });
        }
        var showing = _articleView is not null && ReferenceEquals(_body.Content, _articleView);
        _articleView = view;
        if (showing)
        {
            ShowView(view);
        }
    }
}

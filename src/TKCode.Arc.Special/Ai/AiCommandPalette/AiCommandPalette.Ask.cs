using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where an answer is.</summary>
internal enum AiCommandPaletteAnswerState
{
    /// <summary>The model works; no text yet.</summary>
    Thinking,

    /// <summary>Text is streaming in.</summary>
    Streaming,

    /// <summary>The answer is complete and its actions are listed.</summary>
    Done,

    /// <summary>The model failed; a retry row is listed.</summary>
    Failed
}

/// <summary>One question and everything that came back for it.</summary>
internal sealed class AiCommandPaletteSession(string question, CancellationTokenSource cancellation)
{
    public string Question { get; } = question;
    public CancellationTokenSource Cancellation { get; } = cancellation;
    public AiCommandPaletteWordQueue Words { get; } = new();
    public List<AiCommandPaletteWord> Shown { get; } = [];
    public List<AiCommandPaletteSource> Sources { get; } = [];
    public List<AiCommandPaletteAction> Actions { get; } = [];
    public Dictionary<AiCommandPaletteAction, AiCommandPaletteActionState> ActionStates { get; } = [];
    public Dictionary<AiCommandPaletteAction, (string Title, string? Meta)> ActionCopy { get; } = [];
    public AiCommandPaletteAnswerState State { get; set; } = AiCommandPaletteAnswerState.Thinking;
    public bool Appended { get; set; }
    public bool ResponderDone { get; set; }
    public string? Error { get; set; }
    public IDisposable? Tick { get; set; }
    public Random Pace { get; } = new(question.Length * 31 + 7);

    public AiCommandPaletteActionState StateOf(AiCommandPaletteRow row) =>
        row.Payload is AiCommandPaletteAction action && ActionStates.TryGetValue(action, out var state) ? state : AiCommandPaletteActionState.Idle;
}

public partial class AiCommandPalette
{
    /// <summary>The pace of the reveal: a batch of 3 to 7 words every 34ms (each batch fades in over 140ms).</summary>
    internal static readonly TimeSpan RevealInterval = TimeSpan.FromMilliseconds(34);

    /// <summary>Reduced motion and Off reveal in large steps: up to 24 words every 240ms, without fades.</summary>
    internal static readonly TimeSpan ReducedRevealInterval = TimeSpan.FromMilliseconds(240);

    internal const int ReducedRevealWords = 24;

    private static readonly TimeSpan s_chunkFade = TimeSpan.FromMilliseconds(140);

    private AiCommandPaletteSession? _session;
    private readonly List<AiCommandPaletteSourceChip> _chips = [];
    private readonly List<AiCommandPaletteCitation> _citations = [];
    private bool _follow = true;
    private double _ownOffset = double.NaN;

    internal AiCommandPaletteSession? Session => _session;

    internal AiCommandPaletteAnswerState? AnswerState => _session?.State;

    internal IReadOnlyList<AiCommandPaletteSourceChip> Chips => _chips;

    internal IReadOnlyList<AiCommandPaletteCitation> Citations => _citations;

    /// <summary>The words on screen, joined (citations included as [n]).</summary>
    internal string ShownText => _session is null
        ? string.Empty
        : string.Join(" ", _session.Shown.Select(w => w.IsBreak ? "\n" : w.Text)).Replace(" \n ", "\n", StringComparison.Ordinal);

    /// <summary>Asks <paramref name="question"/>: any earlier answer and its actions are cancelled.</summary>
    private async void StartAsk(string question)
    {
        CancelAsk();
        var session = new AiCommandPaletteSession(question, new CancellationTokenSource());
        _session = session;
        _follow = true;
        if (!string.IsNullOrEmpty(Query))
        {
            SetQuery(string.Empty);
        }
        RebuildAnswer();
        ShowView(View.Answer, animate: true);
        Announce(Labels.Asking(question));

        var token = session.Cancellation.Token;
        var context = new AiCommandPaletteAskContext(question, token,
            text =>
            {
                if (ReferenceEquals(_session, session) && !token.IsCancellationRequested && !string.IsNullOrEmpty(text))
                {
                    session.Appended = true;
                    session.Words.Push(text);
                    EnsurePacing(session);
                }
            },
            sources =>
            {
                if (ReferenceEquals(_session, session) && !token.IsCancellationRequested)
                {
                    SetSources(session, sources);
                }
            });
        // No responder and no simulation: the question gets no answer.
        var responder = Responder ?? Simulation?.Responder(Scheduler, () => Labels) ?? (_ => Task.FromResult<AiCommandPaletteAnswer?>(null));
        try
        {
            var answer = await responder(context);
            if (!ReferenceEquals(_session, session) || token.IsCancellationRequested)
            {
                return;
            }
            if (answer?.Sources is { Count: > 0 } sources && session.Sources.Count == 0)
            {
                SetSources(session, sources);
            }
            if (!session.Appended && !string.IsNullOrEmpty(answer?.Text))
            {
                session.Words.Push(answer.Text);
            }
            if (answer?.Actions is { } actions)
            {
                session.Actions.AddRange(actions);
            }
            session.ResponderDone = true;
            session.Words.Complete();
            EnsurePacing(session);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Asked again, went back, or left the window.
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_session, session) && !token.IsCancellationRequested)
            {
                Fail(session, error.Message);
            }
        }
    }

    /// <summary>Cancels the current answer and its running actions.</summary>
    private void CancelAsk()
    {
        if (_session is not { } session)
        {
            return;
        }
        _session = null;
        session.Tick?.Dispose();
        session.Tick = null;
        session.Cancellation.Cancel();
        session.Cancellation.Dispose();
    }

    private void SetSources(AiCommandPaletteSession session, IReadOnlyList<AiCommandPaletteSource> sources)
    {
        session.Sources.Clear();
        session.Sources.AddRange(sources);
        BuildChips(session);
    }

    /// <summary>Keeps the reveal ticking while words wait (one batch per tick), and finishes once the stream has drained.</summary>
    private void EnsurePacing(AiCommandPaletteSession session)
    {
        if (session.Tick is not null || !ReferenceEquals(_session, session))
        {
            return;
        }
        if (session.Words.IsDrained)
        {
            Finish(session);
            return;
        }
        if (session.Words.Count == 0)
        {
            return;
        }
        var full = this.IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        session.Tick = Scheduler.Schedule(full ? RevealInterval : ReducedRevealInterval, () =>
        {
            session.Tick = null;
            if (!ReferenceEquals(_session, session))
            {
                return;
            }
            RevealBatch(session);
            EnsurePacing(session);
        });
    }

    /// <summary>Shows the next 3 to 7 words (reduced motion: up to 24), each batch fading in over 140ms.</summary>
    private void RevealBatch(AiCommandPaletteSession session)
    {
        var level = this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        var count = full ? 3 + session.Pace.Next(5) : ReducedRevealWords;
        var words = session.Words.Take(count);
        if (words.Count == 0)
        {
            return;
        }
        if (session.State == AiCommandPaletteAnswerState.Thinking)
        {
            session.State = AiCommandPaletteAnswerState.Streaming;
            UpdateAnswerStatus(session);
        }
        session.Shown.AddRange(words);
        var created = AddWords(session, words);
        if (full && created.Count > 0)
        {
            // One fade per batch: every word of it rises from 0 to 1 together.
            foreach (var control in created)
            {
                control.Opacity = 0;
            }
            var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            fade.Changed += value =>
            {
                foreach (var control in created)
                {
                    control.Opacity = value;
                }
            };
            _ = fade.AnimateTo(1, MotionSpec.Tween(s_chunkFade, ArcEasings.Standard));
        }
    }

    /// <summary>Adds word controls for <paramref name="words"/> to the answer text.</summary>
    private List<Control> AddWords(AiCommandPaletteSession session, IReadOnlyList<AiCommandPaletteWord> words)
    {
        var created = new List<Control>(words.Count);
        if (_answerText is null)
        {
            return created;
        }
        _answerText.IsVisible = true;
        foreach (var word in words)
        {
            if (word.IsBreak)
            {
                var paragraph = new AiCommandPaletteParagraphBreak();
                _answerText.Children.Add(paragraph);
                continue;
            }
            var view = new AiCommandPaletteAnswerWord { Text = word.Text };
            foreach (var (text, number) in AiCommandPaletteCitations.Split(word.Text, session.Sources.Count))
            {
                if (number == 0)
                {
                    view.Children.Add(new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
                }
                else
                {
                    view.Children.Add(CreateCitation(session.Sources[number - 1], number));
                }
            }
            _answerText.Children.Add(view);
            created.Add(view);
        }
        return created;
    }

    private AiCommandPaletteCitation CreateCitation(AiCommandPaletteSource source, int number)
    {
        var citation = new AiCommandPaletteCitation { Number = number, Source = source };
        AutomationProperties.SetName(citation, SourceName(source, number));
        Tooltip.SetTip(citation, source.Description ?? source.Title);
        citation.Click += (_, _) => SelectSource(source, fromCitation: true);
        citation.PointerEntered += (_, _) => SetPartner(number, true);
        citation.PointerExited += (_, _) => SetPartner(number, false);
        citation.GotFocus += (_, _) => SetPartner(number, true);
        citation.LostFocus += (_, _) => SetPartner(number, false);
        _citations.Add(citation);
        return citation;
    }

    private string SourceName(AiCommandPaletteSource source, int number) =>
        Labels.SourceName(number, string.IsNullOrEmpty(source.Description) ? source.Title : source.Description);

    private void BuildChips(AiCommandPaletteSession session)
    {
        if (_sourcesPanel is null)
        {
            return;
        }
        _sourcesPanel.Children.Clear();
        _chips.Clear();
        for (var i = 0; i < session.Sources.Count; i++)
        {
            var source = session.Sources[i];
            var number = i + 1;
            var chip = new AiCommandPaletteSourceChip
            {
                Number = number,
                Title = source.Title,
                Mark = source.Mark ?? ArcIcons.FileText,
                Source = source,
            };
            AutomationProperties.SetName(chip, SourceName(source, number));
            Tooltip.SetTip(chip, source.Description ?? source.Title);
            chip.Click += (_, _) => SelectSource(source, fromCitation: false);
            chip.PointerEntered += (_, _) => SetPartner(number, true);
            chip.PointerExited += (_, _) => SetPartner(number, false);
            chip.GotFocus += (_, _) => SetPartner(number, true);
            chip.LostFocus += (_, _) => SetPartner(number, false);
            _chips.Add(chip);
            _sourcesPanel.Children.Add(chip);
        }
        _sourcesPanel.IsVisible = _chips.Count > 0;
        if (ArcMotion.IsFull(this) && _chips.Count > 0)
        {
            // The chips land first: one 160ms fade for the row.
            var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            fade.Changed += value => _sourcesPanel.Opacity = value;
            _sourcesPanel.Opacity = 0;
            _ = fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Standard));
        }
        else
        {
            _sourcesPanel.Opacity = 1;
        }
    }

    /// <summary>Lights a source's chip and every citation of it together (hover or focus on either).</summary>
    internal void SetPartner(int number, bool on)
    {
        foreach (var chip in _chips)
        {
            if (chip.Number == number)
            {
                chip.SetPartner(on);
            }
        }
        foreach (var citation in _citations)
        {
            if (citation.Number == number)
            {
                citation.SetPartner(on);
            }
        }
    }

    internal void SelectSource(AiCommandPaletteSource source, bool fromCitation)
    {
        StopWalkthroughForGood();
        var args = new AiCommandPaletteSourceEventArgs(SourceSelectedEvent, source, fromCitation);
        RaiseEvent(args);
        if (!args.Handled && source.Href is { } href && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(href);
        }
    }

    /// <summary>The stream has drained: the answer is done, its actions are listed and the whole answer is announced.</summary>
    private void Finish(AiCommandPaletteSession session)
    {
        if (session.State is AiCommandPaletteAnswerState.Done or AiCommandPaletteAnswerState.Failed)
        {
            return;
        }
        session.State = AiCommandPaletteAnswerState.Done;
        UpdateAnswerStatus(session);
        BuildActions(session, animateIn: true);
        var text = AiCommandPaletteCitations.Plain(string.Join(" ", session.Shown.Where(w => !w.IsBreak).Select(w => w.Text)));
        Announce(text);
    }

    private void Fail(AiCommandPaletteSession session, string message)
    {
        session.Tick?.Dispose();
        session.Tick = null;
        session.State = AiCommandPaletteAnswerState.Failed;
        session.Error = message;
        UpdateAnswerStatus(session);
        BuildActions(session, animateIn: false);
        Announce(Labels.AnswerFailed + ". " + message);
    }

    /// <summary>Rebuilds the whole answer view from the session (template change, new question, new labels).</summary>
    private void RebuildAnswer()
    {
        _citations.Clear();
        _answerText?.Children.Clear();
        if (_answerText is not null)
        {
            _answerText.IsVisible = false;
        }
        var session = _session;
        if (_question is not null)
        {
            _question.Text = session?.Question;
        }
        if (session is null)
        {
            _sourcesPanel?.Children.Clear();
            _chips.Clear();
            if (_sourcesPanel is not null)
            {
                _sourcesPanel.IsVisible = false;
            }
            _actionList?.SetEntries([], -1);
            if (_error is not null)
            {
                _error.IsVisible = false;
            }
            return;
        }
        BuildChips(session);
        if (session.Shown.Count > 0)
        {
            AddWords(session, session.Shown);
        }
        UpdateAnswerStatus(session);
        BuildActions(session, animateIn: false);
        _answerView?.ScrollToHome();
    }

    private void UpdateAnswerStatus(AiCommandPaletteSession session)
    {
        var thinking = session.State == AiCommandPaletteAnswerState.Thinking;
        if (_thinking is not null)
        {
            _thinking.Text = Labels.Thinking;
            _thinking.IsShimmering = thinking;
            _thinking.IsVisible = thinking;
        }
        if (_thinkingDot is not null)
        {
            _thinkingDot.IsVisible = thinking;
        }
        var failed = session.State == AiCommandPaletteAnswerState.Failed;
        if (_error is not null)
        {
            _error.IsVisible = failed;
        }
        if (_errorText is not null)
        {
            _errorText.Text = session.Error;
            _errorText.IsVisible = !string.IsNullOrEmpty(session.Error);
        }
        UpdateHints();
    }

    /// <summary>The suggested actions (or the retry row of a failed answer) under the answer.</summary>
    private void BuildActions(AiCommandPaletteSession session, bool animateIn)
    {
        if (_actionList is null)
        {
            return;
        }
        var entries = new List<Control>();
        if (session.State == AiCommandPaletteAnswerState.Failed)
        {
            entries.Add(new AiCommandPaletteRow
            {
                Kind = AiCommandPaletteRowKind.Retry,
                Icon = ArcIcons.RotateCcw,
                Title = Labels.Retry,
                Trailing = AiCommandPaletteKeycap.Row("↵"),
            });
        }
        else if (session.State == AiCommandPaletteAnswerState.Done && session.Actions.Count > 0)
        {
            entries.Add(new AiCommandPaletteGroupHeading { Text = Labels.SuggestedActions });
            foreach (var action in session.Actions)
            {
                var row = new AiCommandPaletteRow
                {
                    Kind = AiCommandPaletteRowKind.Action,
                    Payload = action,
                    Icon = action.Mark ?? ArcIcons.ArrowRight,
                };
                var state = session.ActionStates.GetValueOrDefault(action);
                var (title, meta) = session.ActionCopy.TryGetValue(action, out var copy) ? copy : (action.Label, action.Meta);
                row.Title = title;
                row.Meta = meta;
                row.SetState(state == AiCommandPaletteActionState.Done, state == AiCommandPaletteActionState.Failed);
                row.Trailing = new ArcContentSwap { Mode = ContentSwapMode.Crossfade, Content = StatusIcon(state, draw: false) };
                entries.Add(row);
            }
        }
        _actionList.SetEntries(entries, entries.Any(e => e is AiCommandPaletteRow) ? 0 : -1);
        if (animateIn && entries.Count > 0 && ArcMotion.IsFull(this))
        {
            var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
            fade.Changed += value => _actionList.Opacity = value;
            _actionList.Opacity = 0;
            _ = fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Standard));
        }
        else
        {
            _actionList.Opacity = 1;
        }
        UpdateHints();
    }

    /// <summary>The status icon at the end of an action row: nothing, a spinner, a check that draws once, or an alert.</summary>
    private Control? StatusIcon(AiCommandPaletteActionState state, bool draw)
    {
        switch (state)
        {
            case AiCommandPaletteActionState.Running:
            {
                var spinner = new ArcSpinner { Width = 14, Height = 14, Thickness = 1.75 };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                return spinner;
            }
            case AiCommandPaletteActionState.Done:
            {
                var check = new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, DrawOnAttach = draw, Progress = draw ? 0 : 1 };
                ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                return check;
            }
            case AiCommandPaletteActionState.Failed:
            {
                var alert = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 16 };
                ArcTokens.Bind(alert, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
                return alert;
            }
            default:
                return null;
        }
    }

    /// <summary>Runs a suggested action in place: running, then done (the check draws once) or failed (Enter retries).</summary>
    private async void RunAction(AiCommandPaletteRow row, AiCommandPaletteAction action)
    {
        if (_session is not { } session)
        {
            return;
        }
        var state = session.ActionStates.GetValueOrDefault(action);
        if (state is AiCommandPaletteActionState.Running or AiCommandPaletteActionState.Done)
        {
            return;
        }
        var labels = Labels;
        var token = session.Cancellation.Token;
        SetActionState(session, row, action, AiCommandPaletteActionState.Running, action.RunningLabel ?? labels.Running(action.Label), action.Meta);
        try
        {
            var result = action.Run is { } run ? await run(token) : null;
            if (token.IsCancellationRequested || !ReferenceEquals(_session, session))
            {
                return;
            }
            var done = result ?? action.DoneLabel ?? labels.Done;
            var meta = action.DoneMeta ?? action.Meta;
            SetActionState(session, row, action, AiCommandPaletteActionState.Done, done, meta);
            Announce(string.IsNullOrEmpty(action.DoneMeta) ? done : done + ". " + action.DoneMeta);
            RaiseEvent(new AiCommandPaletteActionEventArgs(ActionCompletedEvent, action, done));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The answer was replaced or closed.
        }
        catch (Exception)
        {
            if (ReferenceEquals(_session, session) && !token.IsCancellationRequested)
            {
                var failed = action.ErrorLabel ?? labels.ActionFailed;
                SetActionState(session, row, action, AiCommandPaletteActionState.Failed, failed, action.Meta);
                Announce(action.Label + ": " + failed);
            }
        }
    }

    private void SetActionState(AiCommandPaletteSession session, AiCommandPaletteRow row, AiCommandPaletteAction action,
        AiCommandPaletteActionState state, string title, string? meta)
    {
        session.ActionStates[action] = state;
        session.ActionCopy[action] = (title, meta);
        // Status is never color alone: the icon and the label change together, with one crossfade.
        row.SwapCopy(title, meta);
        row.SetState(state == AiCommandPaletteActionState.Done, state == AiCommandPaletteActionState.Failed);
        if (row.Trailing is ArcContentSwap swap)
        {
            swap.Content = StatusIcon(state, draw: true);
        }
        UpdateHints();
    }

    /// <summary>The answer follows the text down while it streams, unless the person has scrolled up.</summary>
    private void OnAnswerScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_answerView is not { } view)
        {
            return;
        }
        var bottom = Math.Max(0, view.Extent.Height - view.Viewport.Height);
        if (e.OffsetDelta.Y != 0 && !(Math.Abs(view.Offset.Y - _ownOffset) < 0.5))
        {
            // Scrolled by the person (wheel, bar, keys): follow again only once back at the bottom.
            _follow = bottom - view.Offset.Y <= 8;
        }
        if (_follow && (e.ExtentDelta.Y > 0 || e.ViewportDelta.Y != 0) && view.Offset.Y < bottom - 0.5)
        {
            _ownOffset = bottom;
            view.Offset = new Vector(view.Offset.X, bottom);
        }
    }
}

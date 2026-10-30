using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>One step of an <see cref="AgentCursor"/> run: wait this long (story seconds), or for a task, or hold while a person is on the card.</summary>
internal readonly record struct AgentCursorBeat(double Seconds, Task? Task = null, bool Holds = false);

/// <summary>A block on screen: its text now, the text at the start of the run, and its parts.</summary>
internal sealed class AgentCursorLiveBlock
{
    public required string Id { get; init; }

    public required AgentCursorBlockKind Kind { get; init; }

    public required string Text { get; set; }

    /// <summary>The text when the run started (or when the changes were last accepted); null for a block the agent added.</summary>
    public string? Original { get; set; }

    /// <summary>
    /// The block's height box: it follows the text on the bounce-free 0.24s spring, grows an inserted block from zero and
    /// shrinks an undone one to zero; while held (a rewrite clears the text and streams the new one) it never gets shorter.
    /// The text fades itself, so the box never fades it.
    /// </summary>
    public required AiHeightBox Host { get; init; }

    public required AgentCursorTextView View { get; init; }

    public bool Inserted => Original is null;

    public bool Changed => Original is null || Original != Text;

    public AgentCursorBlock ToBlock() => new(Id, Kind, Text);
}

public partial class AgentCursor
{
    private static readonly TimeSpan s_oldTextFade = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan s_insertFade = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan s_barFade = TimeSpan.FromMilliseconds(200);

    /// <summary>Seconds between streamed chunks (Arc: 34ms).</summary>
    internal const double ChunkSeconds = 0.034;

    /// <summary>How long the looping sample waits in review before the teammate accepts.</summary>
    internal const double LoopReviewSeconds = 2.4;

    private readonly List<AgentCursorLiveBlock> _blocks = [];
    private readonly TranslateTransform _barShift = new();
    private readonly Random _drift = new(7);
    private List<AgentCursorBlock> _base = [];
    private List<AgentCursorBlock>? _runStart;
    private AgentCursorTextView? _meta;
    private AgentCursorFlight? _agentFlight;
    private AgentCursorFlight? _teammateFlight;
    private Point _teammateRest;
    private IEnumerator<AgentCursorBeat>? _script;
    private double _wait;
    private Task? _waitTask;
    private bool _waitHolds;
    private bool _loopAgain;
    private LiveLoop? _clock;
    private CancellationTokenSource? _cts;
    private MotionValue<double>? _barFadeValue;
    private bool _barShown;
    private int _shownChanges;
    private string? _editStatus;
    private string? _acceptedBy;
    private AgentCursorTextView? _dragView;
    private (AgentCursorLiveBlock Block, string Text)? _inFlight;
    private double _driftIn = 1.6;
    private bool _documentDirty;

    /// <summary>Tests drive the story clock by hand with <see cref="Advance"/> instead of frames.</summary>
    internal bool ManualClock { get; set; }

    /// <summary>The blocks on screen.</summary>
    internal IReadOnlyList<AgentCursorLiveBlock> LiveBlocks => _blocks;

    /// <summary>The document on screen as blocks.</summary>
    internal List<AgentCursorBlock> CurrentDocument() => _blocks.Select(b => b.ToBlock()).ToList();

    internal AgentCursorFlight? AgentFlight => _agentFlight;

    internal AgentCursorFlight? TeammateFlight => _teammateFlight;

    /// <summary>Whether a run is playing (its story clock has steps left).</summary>
    internal bool IsPlaying => _script is not null;

    internal bool IsReviewBarShown => _barShown;

    private bool IsHeldByPerson => IsPointerOver || IsKeyboardFocusWithin;

    private double EffectiveSpeed => Speed;

    private bool Full => ArcMotion.IsFull(this);

    /// <summary>
    /// Starts a run (Ask, Run again, Retry). Changes still waiting for review are dropped first; the sample starts from its
    /// original text. Does nothing while a run is playing.
    /// </summary>
    public void Ask()
    {
        if (Phase is AgentCursorPhase.Thinking or AgentCursorPhase.Editing || _script is not null)
        {
            return;
        }
        var simulated = PlayingSimulation is not null;
        if (_pendingChanges > 0 || _documentDirty || (simulated && !AgentCursorText.SameContent(CurrentDocument(), EffectiveBlocks)))
        {
            LoadDocument(simulated || _documentDirty ? EffectiveBlocks : _base);
        }
        Begin();
    }

    /// <summary>Stops the agent: cancels <see cref="Run"/>, puts back an edit in progress and keeps finished edits for review.</summary>
    public void Stop()
    {
        if (Phase is not (AgentCursorPhase.Thinking or AgentCursorPhase.Editing))
        {
            return;
        }
        EndScript();
        RevertInFlight();
        ReturnAgent();
        SetPhase(AgentCursorPhase.Stopped);
        if (_pendingChanges > 0)
        {
            ShowReviewBar();
        }
        ApplyPendingDocument();
    }

    /// <summary>Accepts the changes waiting for review and raises <see cref="Accepted"/> with the edited document.</summary>
    public void Accept()
    {
        if (_pendingChanges == 0 || Phase is AgentCursorPhase.Thinking or AgentCursorPhase.Editing)
        {
            return;
        }
        EndScript();
        AcceptCore(null);
    }

    /// <summary>Undoes the changes waiting for review and raises <see cref="Undone"/> with the restored document.</summary>
    public void Undo()
    {
        if (_pendingChanges == 0 || Phase is AgentCursorPhase.Thinking or AgentCursorPhase.Editing)
        {
            return;
        }
        EndScript();
        UndoCore();
    }

    /// <summary>Moves the story clock by <paramref name="seconds"/> of wall time (the frame loop, and tests).</summary>
    internal void Advance(double seconds)
    {
        if (_script is null)
        {
            return;
        }
        Drift(seconds);
        if (_waitTask is { IsCompleted: false })
        {
            return;
        }
        if (_waitHolds && IsHeldByPerson)
        {
            return;
        }
        _wait -= seconds * EffectiveSpeed;
        var guard = 0;
        while (_script is { } script && _wait <= 0 && guard++ < 100_000)
        {
            if (_waitTask is { IsCompleted: false })
            {
                _wait = 0;
                break;
            }
            if (!script.MoveNext())
            {
                if (ReferenceEquals(_script, script))
                {
                    _script = null;
                }
                break;
            }
            if (!ReferenceEquals(_script, script))
            {
                break;
            }
            var beat = script.Current;
            _wait += beat.Seconds;
            _waitTask = beat.Task;
            _waitHolds = beat.Holds;
        }
        if (_script is null)
        {
            _clock?.Stop();
            if (_loopAgain)
            {
                _loopAgain = false;
                Begin();
            }
        }
    }

    private void Begin()
    {
        EndScript();
        _runStart = CurrentDocument();
        _acceptedBy = null;
        _editStatus = null;
        HideReviewBar();
        _script = Script().GetEnumerator();
        _wait = 0;
        _waitTask = null;
        _waitHolds = false;
        SetPhase(AgentCursorPhase.Thinking);
        Advance(0);
        if (_script is not null && !ManualClock)
        {
            _clock ??= new LiveLoop(this, dt =>
            {
                // A stalled frame loses story time instead of jumping ahead.
                Advance(Math.Min(dt, 0.1));
                return _script is not null;
            }) { MinimumLevel = ArcMotionLevel.Off };
            _clock.Start();
        }
    }

    private void EndScript()
    {
        _cts?.Cancel();
        _cts = null;
        _script = null;
        _waitTask = null;
        _waitHolds = false;
        _loopAgain = false;
        _dragView = null;
        _clock?.Stop();
    }

    private static AgentCursorBeat Wait(double seconds) => new(seconds);

    /// <summary>The whole run: read, fetch, edit one by one, review and, when looping, the teammate accepts and it starts over.</summary>
    private IEnumerable<AgentCursorBeat> Script()
    {
        // One beat so the document is laid out before anything is measured.
        yield return Wait(0.25);
        var agent = _agentFlight;
        if (agent is not null && AvatarPoint() is { } avatar)
        {
            agent.Jump(avatar);
            agent.Anchor = AvatarPoint;
            agent.SetShown(true);
        }
        ShowTeammate();
        var reading = _blocks.FirstOrDefault(b => b.Kind == AgentCursorBlockKind.Paragraph) ?? _blocks.FirstOrDefault();
        if (reading is not null)
        {
            yield return Wait(FlyAgent(() => PointIn(reading, 0)));
            yield return Wait(0.35);
            yield return Wait(FlyAgent(() => PointIn(reading, reading.View.LineEnd(0)), 0.8));
        }

        IReadOnlyList<AgentCursorEdit> edits;
        if (Run is { } run)
        {
            var cts = new CancellationTokenSource();
            _cts = cts;
            Task<IReadOnlyList<AgentCursorEdit>> task;
            try
            {
                task = run(cts.Token);
            }
            catch (Exception ex)
            {
                task = Task.FromException<IReadOnlyList<AgentCursorEdit>>(ex);
            }
            yield return new AgentCursorBeat(0, task);
            if (cts.IsCancellationRequested)
            {
                yield break;
            }
            if (task.IsFaulted || task.IsCanceled)
            {
                Fail(task.Exception?.GetBaseException());
                yield break;
            }
            edits = task.Result ?? [];
        }
        else
        {
            yield return Wait(0.3);
            edits = EffectiveEdits;
        }
        if (edits.Count == 0)
        {
            Finish(AgentCursorPhase.Empty);
            yield break;
        }

        var inserted = 0;
        foreach (var edit in edits)
        {
            var steps = edit.Kind switch
            {
                AgentCursorEditKind.Rewrite => Rewrite(edit),
                AgentCursorEditKind.Insert => Insert(edit, ++inserted),
                _ => Fix(edit),
            };
            foreach (var step in steps)
            {
                yield return step;
            }
            yield return Wait(0.35);
        }
        if (_pendingChanges == 0)
        {
            Finish(AgentCursorPhase.Empty);
            yield break;
        }
        EnterReview();
        if (!EffectiveLoop)
        {
            yield break;
        }

        // The looping sample: the teammate accepts after a pause that never runs out while a person is on the card.
        yield return new AgentCursorBeat(LoopReviewSeconds, Holds: true);
        if (TeammateShown && _teammateFlight is { } teammate)
        {
            yield return Wait(FlyCursor(teammate, AcceptPoint));
            yield return Wait(0.25);
        }
        while (IsHeldByPerson)
        {
            yield return new AgentCursorBeat(0.1, Holds: true);
        }
        AcceptCore(AgentCursorText.FirstName(EffectiveCollaborator.Name));
        yield return Wait(1.8);
        if (_runStart is { } start)
        {
            LoadDocument(start);
        }
        yield return Wait(0.5);
        _loopAgain = true;
    }

    private void SetEditStatus(string text)
    {
        _editStatus = text;
        if (Phase != AgentCursorPhase.Editing)
        {
            SetPhase(AgentCursorPhase.Editing);
        }
        else
        {
            RefreshStatus();
            Announce(text);
        }
    }

    /// <summary>A rewrite: drag-select the block line by line, fade the old text out, stream the new one in chunks.</summary>
    private IEnumerable<AgentCursorBeat> Rewrite(AgentCursorEdit edit)
    {
        var block = Find(edit.BlockId);
        if (block is null || edit.Text is null)
        {
            yield break;
        }
        var view = block.View;
        var text = edit.Text;
        SetEditStatus(string.IsNullOrEmpty(edit.Description) ? Labels.Rewriting : edit.Description);
        yield return Wait(FlyAgent(() => PointIn(block, 0)));
        _inFlight = (block, block.Text);

        // The selection fills line by line in step with the drag (measured from the live line boxes).
        _dragView = view;
        var lines = view.LineCount;
        for (var line = 0; line < lines; line++)
        {
            var l = line;
            yield return Wait(FlyAgent(() => PointIn(block, view.LineEnd(l)), 0.7));
            view.SetSelection(0, Math.Max(view.SelectionRange.Length, view.LineEnd(l)));
        }
        _dragView = null;
        view.SetSelection(0, view.Text.Length);
        yield return Wait(0.28);

        if (Full)
        {
            view.FadeText(0, s_oldTextFade);
            yield return Wait(s_oldTextFade.TotalSeconds);
        }
        block.Host.IsHeld = true;
        view.SetSelection(0, 0);
        view.TintAll();
        if (Full)
        {
            view.Text = string.Empty;
            view.JumpTextOpacity(1);
            var previous = 0;
            foreach (var end in AgentCursorText.ChunkEnds(text))
            {
                view.Text = text[..end];
                view.AddChunk(previous, end - previous);
                previous = end;
                FollowAgent(() => PointIn(block, view.Text.Length));
                yield return Wait(ChunkSeconds);
            }
        }
        else
        {
            // Reduced motion and Off: the text appears whole.
            view.Text = text;
            view.JumpTextOpacity(1);
            FollowAgent(() => PointIn(block, text.Length));
        }
        block.Text = text;
        block.Host.IsHeld = false;
        _inFlight = null;
        CountChanges();
        if (_agentFlight is { } agent)
        {
            agent.Anchor = () => PointIn(block, block.Text.Length);
        }
        ReadAlong(block);
    }

    /// <summary>An insert: the cursor goes to the end of the block before, the new block grows from zero, then its text fades in.</summary>
    private IEnumerable<AgentCursorBeat> Insert(AgentCursorEdit edit, int ordinal)
    {
        if (edit.Text is null || _document is null)
        {
            yield break;
        }
        var after = Find(edit.BlockId) ?? _blocks.LastOrDefault();
        SetEditStatus(!string.IsNullOrEmpty(edit.Description) ? edit.Description
            : edit.InsertKind == AgentCursorBlockKind.Paragraph ? Labels.AddingParagraph : Labels.AddingHeading);
        if (after is not null)
        {
            yield return Wait(FlyAgent(() => PointIn(after, after.Text.Length)));
        }
        var block = CreateBlock(new AgentCursorBlock(AgentCursorText.NewId(edit, ordinal), edit.InsertKind, edit.Text), null);
        block.Host.IsExpanded = false;
        block.View.TintAll();
        if (Full)
        {
            block.View.JumpTextOpacity(0);
        }
        var index = after is null ? _blocks.Count : _blocks.IndexOf(after) + 1;
        _blocks.Insert(index, block);
        var panelIndex = after is null ? _document.Children.Count : _document.Children.IndexOf(after.Host) + 1;
        _document.Children.Insert(panelIndex, block.Host);
        _inFlight = (block, block.Text);
        // A frame at zero height first, so the block grows from nothing.
        yield return Wait(0.02);
        block.Host.IsExpanded = true;
        yield return Wait(0.24);
        block.View.FadeText(1, s_insertFade);
        _inFlight = null;
        CountChanges();
        yield return Wait(FlyAgent(() => PointIn(block, block.Text.Length)));
        ReadAlong(block);
    }

    /// <summary>A fix: the cursor goes to the word, a wavy underline flags it, the word is selected and replaced.</summary>
    private IEnumerable<AgentCursorBeat> Fix(AgentCursorEdit edit)
    {
        var block = Find(edit.BlockId);
        var word = edit.Word;
        if (block is null || string.IsNullOrEmpty(word) || edit.Replacement is null)
        {
            yield break;
        }
        var at = AgentCursorText.FindWord(block.Text, word);
        if (at < 0)
        {
            yield break;
        }
        var view = block.View;
        SetEditStatus(string.IsNullOrEmpty(edit.Description) ? Labels.FixingTypo : edit.Description);
        yield return Wait(FlyAgent(() => PointIn(block, at)));
        _inFlight = (block, block.Text);
        view.SetFlag(at, word.Length);
        yield return Wait(0.55);
        _dragView = view;
        view.SetSelection(at, 0);
        yield return Wait(FlyAgent(() => PointIn(block, at + word.Length), 0.6));
        _dragView = null;
        view.SetSelection(at, word.Length);
        yield return Wait(0.2);
        view.ClearFlag();
        view.SetSelection(0, 0);
        view.Replace(at, word.Length, edit.Replacement);
        block.Text = view.Text;
        _inFlight = null;
        CountChanges();
        var end = at + edit.Replacement.Length;
        FollowAgent(() => PointIn(block, end));
        yield return Wait(0.2);
        ReadAlong(block);
    }

    private void CountChanges()
    {
        PendingChanges = _pendingChanges + 1;
        RefreshReviewText();
    }

    private void EnterReview()
    {
        _editStatus = null;
        SetPhase(AgentCursorPhase.Review);
        ShowReviewBar();
    }

    private void Finish(AgentCursorPhase phase)
    {
        _editStatus = null;
        ReturnAgent();
        SetPhase(phase);
        ApplyPendingDocument();
    }

    private void Fail(Exception? error)
    {
        _editStatus = null;
        RevertInFlight();
        ReturnAgent();
        SetPhase(AgentCursorPhase.Error);
        if (!string.IsNullOrWhiteSpace(error?.Message))
        {
            Announce(error.Message);
        }
        if (_pendingChanges > 0)
        {
            ShowReviewBar();
        }
    }

    private void AcceptCore(string? by)
    {
        _acceptedBy = by;
        foreach (var block in _blocks)
        {
            block.View.ClearTints(fade: true);
            block.Original = block.Text;
        }
        _base = CurrentDocument();
        PendingChanges = 0;
        HideReviewBar();
        if (by is null)
        {
            ReturnAgent();
        }
        SetPhase(AgentCursorPhase.Accepted);
        RaiseEvent(new AgentCursorDocumentEventArgs(AcceptedEvent, AgentCursorText.Copy(_base)));
        ApplyPendingDocument();
    }

    private void UndoCore()
    {
        foreach (var block in _blocks.ToList())
        {
            block.View.ClearFlag();
            block.View.SetSelection(0, 0);
            block.View.ClearTints(fade: false);
            if (block.Inserted)
            {
                _blocks.Remove(block);
                RemoveWhenClosed(block.Host);
                block.Host.IsExpanded = false;
            }
            else if (block.Original is { } original && original != block.Text)
            {
                block.View.FinishFades();
                block.Text = original;
                block.View.Text = original;
                block.View.AddChunk(0, original.Length);
            }
        }
        _base = CurrentDocument();
        PendingChanges = 0;
        HideReviewBar();
        ReturnAgent();
        SetPhase(AgentCursorPhase.Reverted);
        RaiseEvent(new AgentCursorDocumentEventArgs(UndoneEvent, AgentCursorText.Copy(_base)));
        ApplyPendingDocument();
    }

    private void RemoveWhenClosed(AiHeightBox host)
    {
        host.Closed += (_, _) =>
        {
            if (!host.IsExpanded)
            {
                _document?.Children.Remove(host);
            }
        };
    }

    /// <summary>Puts back an edit that was half done when the run stopped or failed.</summary>
    private void RevertInFlight()
    {
        _dragView = null;
        if (_inFlight is not { } flight)
        {
            return;
        }
        _inFlight = null;
        var (block, text) = flight;
        block.View.FinishFades();
        block.View.ClearFlag();
        block.View.SetSelection(0, 0);
        block.Host.IsHeld = false;
        if (block.Inserted)
        {
            _blocks.Remove(block);
            RemoveWhenClosed(block.Host);
            block.Host.IsExpanded = false;
            return;
        }
        block.Text = text;
        block.View.Text = text;
        if (!block.Changed)
        {
            block.View.ClearTints(fade: false);
        }
    }

    // ---- The document -------------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="blocks"/> as the document, with no pending changes.</summary>
    private void LoadDocument(IReadOnlyList<AgentCursorBlock> blocks)
    {
        _documentDirty = false;
        _base = AgentCursorText.Copy(blocks);
        _blocks.Clear();
        PendingChanges = 0;
        _inFlight = null;
        if (_document is null)
        {
            return;
        }
        _document.Children.Clear();
        _meta = null;
        var meta = EffectiveMeta;
        var metaAt = -1;
        if (!string.IsNullOrEmpty(meta))
        {
            var titleIndex = _base.FindIndex(b => b.Kind == AgentCursorBlockKind.Title);
            metaAt = titleIndex + 1;
        }
        for (var i = 0; i <= _base.Count; i++)
        {
            if (i == metaAt)
            {
                _meta = new AgentCursorTextView { Text = meta! };
                _meta.Classes.Add("meta");
                _document.Children.Add(_meta);
            }
            if (i == _base.Count)
            {
                break;
            }
            var block = CreateBlock(_base[i], _base[i].Text);
            _blocks.Add(block);
            _document.Children.Add(block.Host);
        }
    }

    private AgentCursorLiveBlock CreateBlock(AgentCursorBlock source, string? original)
    {
        var view = new AgentCursorTextView { Text = source.Text };
        view.Classes.Add(source.Kind switch
        {
            AgentCursorBlockKind.Title => "title",
            AgentCursorBlockKind.Heading => "heading",
            _ => "paragraph",
        });
        view.SetAccent(AgentColor);
        var host = new AiHeightBox
        {
            IsExpanded = true,
            FollowsContent = true,
            KeepsContent = true,
            HeightSpring = AiHeightBox.PanelSpring,
            FadeInDuration = TimeSpan.Zero,
            FadeOutDuration = TimeSpan.Zero,
            Content = view,
        };
        return new AgentCursorLiveBlock { Id = source.Id, Kind = source.Kind, Text = source.Text, Original = original, Host = host, View = view };
    }

    private AgentCursorLiveBlock? Find(string id) => _blocks.FirstOrDefault(b => b.Id == id);

    /// <summary>
    /// The app changed <see cref="Blocks"/> or <see cref="Meta"/>: the document follows when no run is using it and the content
    /// differs; otherwise it follows when the run ends.
    /// </summary>
    private void OnDocumentInputChanged()
    {
        if (Phase is AgentCursorPhase.Thinking or AgentCursorPhase.Editing or AgentCursorPhase.Review || _pendingChanges > 0)
        {
            _documentDirty = true;
            return;
        }
        if (!AgentCursorText.SameContent(EffectiveBlocks, CurrentDocument()) || _meta?.Text != EffectiveMeta)
        {
            _runStart = null;
            LoadDocument(EffectiveBlocks);
        }
    }

    /// <summary>
    /// A new <see cref="Simulation"/> (or none) while it is the one playing: a run in progress ends, its changes go, and
    /// the card starts over from the new document, agent and teammate.
    /// </summary>
    private void OnSimulationChanged()
    {
        if (Blocks is not null || Edits is not null || Run is not null)
        {
            return;
        }
        EndScript();
        RevertInFlight();
        ReturnAgent();
        HideReviewBar();
        _runStart = null;
        LoadDocument(EffectiveBlocks);
        SetPhase(AgentCursorPhase.Idle);
        ApplyIdentity();
        ApplyColors();
        RefreshStatus();
        RefreshChrome();
        StartAutoPlay();
    }

    private void ApplyPendingDocument()
    {
        if (!_documentDirty || _pendingChanges > 0)
        {
            return;
        }
        _documentDirty = false;
        if (!AgentCursorText.SameContent(EffectiveBlocks, CurrentDocument()) || _meta?.Text != EffectiveMeta)
        {
            LoadDocument(EffectiveBlocks);
        }
    }

    // ---- Cursors ------------------------------------------------------------------------------------------------------

    private void BuildCursors()
    {
        if (_overlay is null)
        {
            return;
        }
        _overlay.Children.Clear();
        var agent = new AgentCursorPointer();
        var teammate = new AgentCursorPointer();
        _overlay.Children.Add(teammate);
        _overlay.Children.Add(agent);
        _agentFlight = new AgentCursorFlight(this, agent);
        _agentFlight.Moved += OnAgentMoved;
        _teammateFlight = new AgentCursorFlight(this, teammate);
        _teammateRest = default;
    }

    /// <summary>While the agent drags, the selection follows its tip.</summary>
    private void OnAgentMoved(Point point)
    {
        if (_dragView is not { } view || _overlay is null)
        {
            return;
        }
        if (_overlay.TranslatePoint(point, view) is { } local)
        {
            var start = view.SelectionRange.Start;
            var index = view.IndexAt(local);
            if (index > start)
            {
                view.SetSelection(start, Math.Max(view.SelectionRange.Length, index - start));
            }
        }
    }

    /// <summary>Where character <paramref name="index"/> of a block is, in the cursor layer.</summary>
    private Point? PointIn(AgentCursorLiveBlock block, int index)
    {
        if (_overlay is null || block.View.Bounds.Width <= 0)
        {
            return null;
        }
        return block.View.TranslatePoint(block.View.CaretPoint(index), _overlay);
    }

    private Point? AvatarPoint()
    {
        if (_overlay is null || _agentAvatar is null || _agentAvatar.Bounds.Width <= 0)
        {
            return null;
        }
        return _agentAvatar.TranslatePoint(new Point(_agentAvatar.Bounds.Width / 2, _agentAvatar.Bounds.Height / 2), _overlay);
    }

    private Point? AcceptPoint()
    {
        if (_overlay is null || _accept is null || _accept.Bounds.Width <= 0)
        {
            return null;
        }
        return _accept.TranslatePoint(new Point(_accept.Bounds.Width * 0.45, _accept.Bounds.Height * 0.55), _overlay);
    }

    private double FlyAgent(Func<Point?> target, double scale = 1) =>
        _agentFlight is { } agent ? FlyCursor(agent, target, scale) : 0;

    private double FlyCursor(AgentCursorFlight flight, Func<Point?> target, double scale = 1)
    {
        if (target() is not { } point)
        {
            return 0;
        }
        flight.Anchor = target;
        return flight.FlyTo(point, EffectiveSpeed / scale);
    }

    private void FollowAgent(Func<Point?> target)
    {
        if (_agentFlight is { } agent && target() is { } point)
        {
            agent.Anchor = target;
            agent.Follow(point);
        }
    }

    /// <summary>The agent's cursor goes back into its avatar and fades.</summary>
    private void ReturnAgent()
    {
        if (_agentFlight is not { } agent || !agent.IsShown)
        {
            return;
        }
        if (AvatarPoint() is { } avatar)
        {
            agent.FlyTo(avatar, EffectiveSpeed);
            agent.Anchor = AvatarPoint;
        }
        agent.SetShown(false);
    }

    /// <summary>The teammate rests in the document, reading, once the document is laid out.</summary>
    private void ShowTeammate()
    {
        if (_teammateFlight is not { } teammate)
        {
            return;
        }
        if (!TeammateShown)
        {
            teammate.SetShown(false);
            return;
        }
        if (teammate.IsShown)
        {
            return;
        }
        var rest = _blocks.LastOrDefault(b => b.Kind == AgentCursorBlockKind.Paragraph) ?? _blocks.LastOrDefault();
        if (rest is null)
        {
            return;
        }
        Func<Point?> anchor = () => PointIn(rest, (int)(rest.View.LineEnd(0) * 0.45));
        if (anchor() is { } point)
        {
            teammate.Jump(point);
            teammate.Anchor = anchor;
            _teammateRest = point;
            teammate.SetShown(true);
        }
    }

    /// <summary>The teammate reads along: after an edit it moves to the start of the changed block's first line.</summary>
    private void ReadAlong(AgentCursorLiveBlock block)
    {
        if (!TeammateShown || _teammateFlight is not { } teammate)
        {
            return;
        }
        FlyCursor(teammate, () => PointIn(block, Math.Min(block.Text.Length, Math.Max(0, block.View.LineEnd(0) / 3))));
        if (teammate.Anchor?.Invoke() is { } rest)
        {
            _teammateRest = rest;
        }
    }

    /// <summary>The teammate's idle drift: a few pixels around where they rest every 1.6s (full motion only).</summary>
    private void Drift(double seconds)
    {
        if (_teammateFlight is not { } teammate || !teammate.IsShown || !Full || teammate.IsFlying)
        {
            return;
        }
        _driftIn -= seconds;
        if (_driftIn > 0)
        {
            return;
        }
        _driftIn = 1.6;
        var anchor = teammate.Anchor?.Invoke() ?? _teammateRest;
        var offset = new Vector((_drift.NextDouble() - 0.5) * 18, (_drift.NextDouble() - 0.5) * 6);
        teammate.Follow(anchor + offset, drift: true);
    }

    private void OnStageSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        if (_agentFlight is { } agent)
        {
            agent.StageWidth = width;
            agent.Reanchor();
        }
        if (_teammateFlight is { } teammate)
        {
            teammate.StageWidth = width;
            if (teammate.IsShown)
            {
                teammate.Reanchor();
            }
            else if (_script is not null || Phase != AgentCursorPhase.Idle)
            {
                ShowTeammate();
            }
        }
    }

    // ---- Review bar ---------------------------------------------------------------------------------------------------

    /// <summary>The review bar rises 8px and fades in over 200ms (reduced motion: the fade only; Off: at once).</summary>
    private void ShowReviewBar()
    {
        _shownChanges = _pendingChanges;
        RefreshReviewText();
        if (_barShown)
        {
            return;
        }
        _barShown = true;
        PseudoClasses.Set(ReviewPseudoClass, true);
        if (_reviewBar is null)
        {
            return;
        }
        _reviewBar.IsVisible = true;
        AnimateBar(1);
    }

    private void HideReviewBar()
    {
        if (!_barShown)
        {
            return;
        }
        _barShown = false;
        PseudoClasses.Set(ReviewPseudoClass, false);
        if (_reviewBar is null)
        {
            return;
        }
        AnimateBar(0);
    }

    private async void AnimateBar(double target)
    {
        if (_reviewBar is not { } bar)
        {
            return;
        }
        if (_barFadeValue is null)
        {
            _barFadeValue = new MotionValue<double>(this, bar.Opacity, MotionVectors.Double);
            _barFadeValue.Changed += v =>
            {
                bar.Opacity = v;
                _barShift.Y = Full ? (1 - v) * BarRise() : 0;
            };
        }
        var done = await _barFadeValue.AnimateTo(target, MotionSpec.Tween(s_barFade, target > 0 ? ArcEasings.Enter : ArcEasings.Exit));
        if (done && target <= 0 && !_barShown)
        {
            bar.IsVisible = false;
            _shownChanges = 0;
        }
    }

    private double BarRise() =>
        this.TryFindResource(AgentCursorTokenKind.ReviewBarRise, ActualThemeVariant, out var value) && value is double d ? d : 8;
}

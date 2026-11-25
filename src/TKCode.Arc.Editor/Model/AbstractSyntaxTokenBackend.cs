// Ported from vscode (src/vs/editor/common/model/tokens/abstractSyntaxTokenBackend.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - AttachedViews has no visibleLineRanges observable (only the tree-sitter backend reads it); its change event is an
//    Emitter of (view, state) pairs;
//  - AttachedViewHandler's RunOnceScheduler (50 ms) is a timer of the model's IdleScheduler;
//  - onDidChangeFontTokens is left out (font tokens come from tree-sitter and the font decorations, not ported);
//  - the events are Emitters (Core/Emitter.cs) behind .NET events, as the text model's.

using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Model;

/// <summary>The views a model is attached to, and which lines each of them shows.</summary>
internal sealed class AttachedViews
{
    private readonly Emitter<(IAttachedView View, AttachedViewState? State)> _onDidChangeVisibleRanges = new();
    private readonly HashSet<AttachedViewImpl> _views = new();

    /// <summary>A view's visible lines changed (state null: the view was detached).</summary>
    public event Action<(IAttachedView View, AttachedViewState? State)> DidChangeVisibleRanges
    {
        add => _onDidChangeVisibleRanges.Add(value);
        remove => _onDidChangeVisibleRanges.Remove(value);
    }

    public IAttachedView AttachView()
    {
        AttachedViewImpl? view = null;
        view = new AttachedViewImpl(state => _onDidChangeVisibleRanges.Fire((view!, state)));
        _views.Add(view);
        return view;
    }

    public void DetachView(IAttachedView view)
    {
        if (view is AttachedViewImpl impl)
        {
            _views.Remove(impl);
        }

        _onDidChangeVisibleRanges.Fire((view, null));
    }

    public void Dispose() => _onDidChangeVisibleRanges.Clear();
}

/// <summary>The lines a view shows, and whether they stopped changing.</summary>
internal sealed class AttachedViewState(IReadOnlyList<LineRange> visibleLineRanges, bool stabilized)
{
    public IReadOnlyList<LineRange> VisibleLineRanges { get; } = visibleLineRanges;

    public bool Stabilized { get; } = stabilized;

    public bool Equals(AttachedViewState other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!VisibleLineRanges.SequenceEqual(other.VisibleLineRanges))
        {
            return false;
        }

        return Stabilized == other.Stabilized;
    }
}

internal sealed class AttachedViewImpl(Action<AttachedViewState> handleStateChange) : IAttachedView
{
    public AttachedViewState? State { get; private set; }

    public void SetVisibleLines(IReadOnlyList<TextRange> visibleLines, bool stabilized)
    {
        var visibleLineRanges = visibleLines.Select(line => new LineRange(line.StartLineNumber, line.EndLineNumber + 1)).ToList();
        var state = new AttachedViewState(visibleLineRanges, stabilized);
        State = state;
        handleStateChange(state);
    }
}

/// <summary>Refreshes the tokens of a view's visible lines: at once when they stabilized, else after 50 ms.</summary>
internal sealed class AttachedViewHandler : IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(50);

    private readonly Action _refreshTokens;
    private readonly IdleScheduler _scheduler;
    private IDisposable? _runner;
    private IReadOnlyList<LineRange> _computedLineRanges = [];

    public AttachedViewHandler(Action refreshTokens, IdleScheduler scheduler)
    {
        _refreshTokens = refreshTokens;
        _scheduler = scheduler;
    }

    public IReadOnlyList<LineRange> LineRanges { get; private set; } = [];

    private void Update()
    {
        if (_computedLineRanges.SequenceEqual(LineRanges))
        {
            return;
        }

        _computedLineRanges = LineRanges;
        _refreshTokens();
    }

    public void HandleStateChange(AttachedViewState state)
    {
        LineRanges = state.VisibleLineRanges;
        if (state.Stabilized)
        {
            CancelRunner();
            Update();
        }
        else
        {
            // RunOnceScheduler.schedule: cancels the pending run and starts the timer again.
            CancelRunner();
            _runner = _scheduler.RunAfter(s_delay, () =>
            {
                _runner = null;
                Update();
            });
        }
    }

    private void CancelRunner()
    {
        _runner?.Dispose();
        _runner = null;
    }

    public void Dispose() => CancelRunner();
}

/// <summary>Whether a model's background tokenization has gone through the whole text.</summary>
internal enum BackgroundTokenizationState
{
    InProgress = 1,
    Completed = 2,
}

/// <summary>Computes and keeps the syntax tokens of a model.</summary>
internal abstract class AbstractSyntaxTokenBackend : IDisposable
{
    private protected readonly Emitter<ModelTokensChangedEvent> OnDidChangeTokensEmitter = new();
    private protected readonly Emitter<bool> OnDidChangeBackgroundTokenizationStateEmitter = new();

    protected AbstractSyntaxTokenBackend(Languages.ILanguageIdCodec languageIdCodec, TextModel textModel)
    {
        LanguageIdCodec = languageIdCodec;
        TextModel = textModel;
    }

    protected Languages.ILanguageIdCodec LanguageIdCodec { get; }

    protected TextModel TextModel { get; }

    public BackgroundTokenizationState BackgroundTokenizationState { get; protected set; } = BackgroundTokenizationState.InProgress;

    /// <summary>Internal, should not be exposed by the text model!</summary>
    public event Action<ModelTokensChangedEvent> DidChangeTokens
    {
        add => OnDidChangeTokensEmitter.Add(value);
        remove => OnDidChangeTokensEmitter.Remove(value);
    }

    /// <summary>Internal, should not be exposed by the text model!</summary>
    public event Action DidChangeBackgroundTokenizationState
    {
        add => OnDidChangeBackgroundTokenizationStateEmitter.Add(_ => value(), value);
        remove => OnDidChangeBackgroundTokenizationStateEmitter.Remove(value);
    }

    public abstract bool HasTokens { get; }

    /// <summary>Upstream's todo_resetTokenization.</summary>
    public abstract void ResetTokenization(bool fireTokenChangeEvent = true);

    public abstract void HandleDidChangeAttached();

    public abstract void HandleDidChangeContent(ModelContentChangedEvent e);

    public abstract void ForceTokenization(int lineNumber);

    public abstract bool HasAccurateTokensForLine(int lineNumber);

    public abstract bool IsCheapToTokenize(int lineNumber);

    public void TokenizeIfCheap(int lineNumber)
    {
        if (IsCheapToTokenize(lineNumber))
        {
            ForceTokenization(lineNumber);
        }
    }

    public abstract LineTokens GetLineTokens(int lineNumber);

    public abstract StandardTokenType GetTokenTypeIfInsertingCharacter(int lineNumber, int column, string character);

    public abstract IReadOnlyList<LineTokens>? TokenizeLinesAt(int lineNumber, IReadOnlyList<string> lines);

    public virtual void Dispose()
    {
        OnDidChangeTokensEmitter.Clear();
        OnDidChangeBackgroundTokenizationStateEmitter.Clear();
    }
}

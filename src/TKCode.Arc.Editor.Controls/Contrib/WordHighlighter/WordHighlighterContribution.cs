// Ported from vscode (src/vs/editor/contrib/wordHighlighter/browser/wordHighlighter.ts: WordHighlighter,
// WordHighlighterContribution, NextWordHighlightAction, PrevWordHighlightAction and TriggerWordHighlightAction;
// textualHighlightProvider.ts: TextualMultiDocumentHighlightFeature's registration; commit
// b57d5c9e), MIT license. The occurrences come from the engine (WordHighlighterCore.GetOccurrencesAtPosition).
// Differences:
//  - one editor and its model: no multi-document highlights (occurrencesHighlight 'multiFile'), no notebook cells and
//    no static query shared between editors (upstream's WordHighlighter.query / storedDecorationIDs);
//  - the textual fallback provider is registered by this contribution into the editor's own registry (upstream: an
//    editor feature registers it once into the process-wide registry);
//  - the delays are dispatcher timers (runDelayer 50 ms; occurrencesHighlightDelay as an option, default 0);
//  - no aria alert when moving between highlights.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.WordHighlighter;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Highlights the occurrences of the symbol at the cursor (upstream's WordHighlighterContribution).</summary>
internal sealed class WordHighlighterContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.wordHighlighter";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new WordHighlighterContribution(editor), EditorContributionInstantiation.Eager);

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _hasWordHighlights;
    private readonly List<IDisposable> _registrations = [];
    private string[] _decorationIds = [];
    private IDisposable? _runDelayer;
    private IDisposable? _renderTimer;
    private CancellationTokenSource? _workerRequest;
    private int _workerRequestTokenId;
    private bool _workerRequestCompleted = true;
    private bool _ignorePositionChangeEvent;
    private long _lastCursorPositionChangeTime;
    private bool _disposed;

    private WordHighlighterContribution(CodeEditor editor)
    {
        _editor = editor;
        _hasWordHighlights = editor.CreateContextKey(CodeEditorContextKeys.HasWordHighlights);

        _registrations.Add(editor.LanguageFeatures.DocumentHighlightProvider.Register("*", new TextualDocumentHighlightProvider(editor.LanguageConfigurationService)));
        _registrations.Add(editor.DecorationStyles.Register(HighlightDecorations.WordHighlightClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.WordHighlight }));
        _registrations.Add(editor.DecorationStyles.Register(HighlightDecorations.WordHighlightStrongClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.WordHighlightStrong }));
        _registrations.Add(editor.DecorationStyles.Register(HighlightDecorations.WordHighlightTextClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.WordHighlight }));

        editor.DidChangeCursorSelection += OnCursorSelection;
        editor.DidChangeTextFocus += OnTextFocus;
        editor.DidChangeModelContent += OnModelContent;
        editor.DidChangeModelLanguage += OnModelLanguage;
    }

    /// <summary>Whether occurrences are highlighted at all (upstream's occurrencesHighlight not 'off').</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>How long after the cursor moved the highlights show at the earliest (upstream's occurrencesHighlightDelay).</summary>
    public TimeSpan OccurrencesHighlightDelay { get; set; } = TimeSpan.Zero;

    /// <summary>Whether every provider is asked and the answers merged (upstream's occurrencesHighlightFromAllProviders).</summary>
    public bool MergeProviders { get; set; }

    /// <summary>The ranges highlighted now (tests).</summary>
    internal IReadOnlyList<TextRange> HighlightedRanges => GetSortedHighlights();

    public static WordHighlighterContribution? Get(CodeEditor editor) => editor.GetContribution<WordHighlighterContribution>(Id);

    /// <summary>Whether occurrences are highlighted (upstream's hasDecorations).</summary>
    public bool HasDecorations() => _decorationIds.Length > 0;

    public object? SaveViewState() => HasDecorations();

    public void RestoreViewState(object? state)
    {
        if (state is true)
        {
            Restore(TimeSpan.FromMilliseconds(250));
        }
    }

    private void Restore(TimeSpan delay)
    {
        if (!IsEnabled)
        {
            return;
        }

        _runDelayer?.Dispose();
        _runDelayer = CodeEditorMultiCursorAdapter.RunAfter(TimeSpan.FromMilliseconds(50), () => Run(delay));
    }

    /// <summary>Highlights the occurrences at once (upstream's trigger).</summary>
    public void Trigger()
    {
        _runDelayer?.Dispose();
        _runDelayer = null;
        Run(TimeSpan.Zero);
    }

    /// <summary>Removes the highlights (upstream's stopHighlighting).</summary>
    public void Stop()
    {
        if (IsEnabled)
        {
            StopAll();
        }
    }

    private void OnCursorSelection(CursorStateChangedEvent e)
    {
        if (_ignorePositionChangeEvent || !IsEnabled)
        {
            return;
        }

        _runDelayer?.Dispose();
        _runDelayer = CodeEditorMultiCursorAdapter.RunAfter(TimeSpan.FromMilliseconds(50), () => OnPositionChanged(e));
    }

    private void OnTextFocus(bool focused)
    {
        if (!focused || !IsEnabled || _workerRequest is not null)
        {
            return;
        }

        _runDelayer?.Dispose();
        _runDelayer = CodeEditorMultiCursorAdapter.RunAfter(TimeSpan.FromMilliseconds(50), () => Run(null));
    }

    private void OnModelContent(ModelContentChangedEvent e) => StopAll();

    private void OnModelLanguage(ModelLanguageChangedEvent e) => StopAll();

    private void OnPositionChanged(CursorStateChangedEvent e)
    {
        _runDelayer = null;
        if (!IsEnabled)
        {
            StopAll();
            return;
        }

        // ignore typing & other
        if (e.Source != "api" && e.Reason != CursorChangeReason.Explicit)
        {
            StopAll();
            return;
        }

        Run(null);
    }

    private WordAtPosition? GetWord(TextSelection selection) =>
        _editor.Model.IsDisposed()
            ? null
            : WordHighlighterCore.GetWordAtPosition(_editor.Model, new TextPosition(selection.StartLineNumber, selection.StartColumn), _editor.LanguageConfigurationService);

    private void Run(TimeSpan? delay)
    {
        _runDelayer = null;
        if (_disposed || !_editor.IsTextFocused || _editor.GetSelection() is not { } editorSelection)
        {
            StopAll();
            return;
        }

        // ignore multiline selection
        if (editorSelection.StartLineNumber != editorSelection.EndLineNumber)
        {
            StopAll();
            return;
        }

        var word = GetWord(editorSelection);

        // The selection must be inside a word or surround one word at most
        if (word is null || word.StartColumn > editorSelection.StartColumn || word.EndColumn < editorSelection.EndColumn)
        {
            StopAll();
            return;
        }

        _lastCursorPositionChangeTime = Environment.TickCount64;

        // check if the new queried word is contained in the range of a stored decoration for this model
        var position = editorSelection.Position;
        foreach (var storedRange in GetHighlightRanges())
        {
            if (storedRange.ContainsPosition(position))
            {
                return;
            }
        }

        StopAll();

        var myRequestId = ++_workerRequestTokenId;
        _workerRequestCompleted = false;
        var cancellation = new CancellationTokenSource();
        _workerRequest = cancellation;
        _ = ComputeAsync(myRequestId, editorSelection.Position, cancellation, delay ?? OccurrencesHighlightDelay);
    }

    private async Task ComputeAsync(int requestId, TextPosition position, CancellationTokenSource cancellation, TimeSpan delay)
    {
        Dictionary<Uri, IReadOnlyList<DocumentHighlight>> data;
        try
        {
            data = await WordHighlighterCore.GetOccurrencesAtPosition(
                _editor.LanguageFeatures.DocumentHighlightProvider, _editor.Model, position, cancellation.Token, MergeProviders);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (requestId != _workerRequestTokenId || _disposed)
        {
            return;
        }

        _workerRequestCompleted = true;
        var highlights = data.TryGetValue(_editor.Model.Uri, out var list) ? list : [];
        BeginRenderDecorations(highlights, delay);
    }

    private void BeginRenderDecorations(IReadOnlyList<DocumentHighlight> highlights, TimeSpan delay)
    {
        var currentTime = Environment.TickCount64;
        var minimumRenderTime = _lastCursorPositionChangeTime + (long)delay.TotalMilliseconds;
        if (currentTime >= minimumRenderTime)
        {
            RenderDecorations(highlights);
        }
        else
        {
            _renderTimer = CodeEditorMultiCursorAdapter.RunAfter(TimeSpan.FromMilliseconds(minimumRenderTime - currentTime), () => RenderDecorations(highlights));
        }
    }

    private void RenderDecorations(IReadOnlyList<DocumentHighlight> highlights)
    {
        _renderTimer = null;
        var decorations = new List<ModelDeltaDecoration>(highlights.Count);
        foreach (var highlight in highlights)
        {
            decorations.Add(new ModelDeltaDecoration(highlight.Range, HighlightDecorations.GetHighlightDecorationOptions(highlight.Kind)));
        }

        _decorationIds = _editor.Model.DeltaDecorations(_decorationIds, decorations);
        _hasWordHighlights.Set(_decorationIds.Length > 0);
        _workerRequest = null;
    }

    private IEnumerable<TextRange> GetHighlightRanges()
    {
        foreach (var id in _decorationIds)
        {
            if (_editor.Model.GetDecorationRange(id) is { } range)
            {
                yield return range;
            }
        }
    }

    private List<TextRange> GetSortedHighlights()
    {
        var ranges = GetHighlightRanges().ToList();
        ranges.Sort(TextRange.CompareRangesUsingStarts);
        return ranges;
    }

    /// <summary>Moves the cursor to the next highlighted occurrence (upstream's moveNext).</summary>
    public void MoveNext() => Move(1);

    /// <summary>Moves the cursor to the previous highlighted occurrence (upstream's moveBack).</summary>
    public void MoveBack() => Move(-1);

    private void Move(int delta)
    {
        var highlights = GetSortedHighlights();
        if (highlights.Count == 0 || _editor.GetPosition() is not { } position)
        {
            return;
        }

        var index = highlights.FindIndex(range => range.ContainsPosition(position));
        var newIndex = ((index + delta) % highlights.Count + highlights.Count) % highlights.Count;
        var dest = highlights[newIndex];
        try
        {
            _ignorePositionChangeEvent = true;
            _editor.SetPosition(dest.Start);
            _editor.RevealRange(dest, VerticalRevealType.CenterIfOutsideViewport);
        }
        finally
        {
            _ignorePositionChangeEvent = false;
        }
    }

    private void RemoveDecorations()
    {
        if (_decorationIds.Length > 0 && !_editor.Model.IsDisposed())
        {
            _decorationIds = _editor.Model.DeltaDecorations(_decorationIds, []);
        }

        _decorationIds = [];
        _hasWordHighlights.Set(false);
    }

    // upstream's _stopAll / _stopSingular for one editor
    private void StopAll()
    {
        RemoveDecorations();

        _renderTimer?.Dispose();
        _renderTimer = null;

        if (_workerRequest is not null)
        {
            _workerRequest.Cancel();
            _workerRequest = null;
        }

        if (!_workerRequestCompleted)
        {
            _workerRequestTokenId++;
            _workerRequestCompleted = true;
        }
    }

    public void Dispose()
    {
        StopAll();
        _disposed = true;
        _runDelayer?.Dispose();
        _editor.DidChangeCursorSelection -= OnCursorSelection;
        _editor.DidChangeTextFocus -= OnTextFocus;
        _editor.DidChangeModelContent -= OnModelContent;
        _editor.DidChangeModelLanguage -= OnModelLanguage;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }
    }

    // ---- actions

    public static readonly EditorAction NextWordHighlightAction = EditorAction.Create(
        "editor.action.wordHighlight.next",
        "Go to Next Symbol Highlight",
        CodeEditorContextKeys.HasWordHighlights,
        (editor, _) => Get(editor)?.MoveNext(),
        new EditorKeybinding((int)EditorKeyCode.F7) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction PrevWordHighlightAction = EditorAction.Create(
        "editor.action.wordHighlight.prev",
        "Go to Previous Symbol Highlight",
        CodeEditorContextKeys.HasWordHighlights,
        (editor, _) => Get(editor)?.MoveBack(),
        new EditorKeybinding(KeyMod.Shift | (int)EditorKeyCode.F7) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction TriggerWordHighlightAction = EditorAction.Create(
        "editor.action.wordHighlight.trigger",
        "Trigger Symbol Highlight",
        null,
        (editor, _) => Get(editor)?.RestoreViewState(true));
}

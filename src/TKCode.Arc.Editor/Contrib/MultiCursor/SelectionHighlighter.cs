// Ported from vscode (src/vs/editor/contrib/multicursor/browser/multicursor.ts: SelectionHighlighterState and
// SelectionHighlighter, commit b57d5c9e), MIT license.
// Differences:
//  - works on IMultiCursorEditor; its decorations are the editor's (owner id) through model.deltaDecorations instead
//    of a decorations collection;
//  - the 300 ms scheduler runs through the creator's runAfter hook;
//  - "the model has a document highlight provider" is a callback (the editor's language feature registry);
//  - there is no onDidChangeModel: an editor keeps its model.

using TKCode.Arc.Editor.Contrib.WordHighlighter;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>The search the selection highlights show, with its matches cached per model version (upstream's SelectionHighlighterState).</summary>
internal sealed class SelectionHighlighterState
{
    private readonly TextModel _model;
    private readonly string _searchText;
    private readonly bool _matchCase;
    private readonly string? _wordSeparators;
    private readonly int _modelVersionId;
    private List<TextRange>? _cachedFindMatches;

    public SelectionHighlighterState(TextModel model, string searchText, bool matchCase, string? wordSeparators, SelectionHighlighterState? prevState)
    {
        _model = model;
        _searchText = searchText;
        _matchCase = matchCase;
        _wordSeparators = wordSeparators;
        _modelVersionId = model.GetVersionId();
        if (prevState is not null
            && ReferenceEquals(_model, prevState._model)
            && _searchText == prevState._searchText
            && _matchCase == prevState._matchCase
            && _wordSeparators == prevState._wordSeparators
            && _modelVersionId == prevState._modelVersionId)
        {
            _cachedFindMatches = prevState._cachedFindMatches;
        }
    }

    public string SearchText => _searchText;

    public List<TextRange> FindMatches()
    {
        if (_cachedFindMatches is null)
        {
            _cachedFindMatches = _model.FindMatches(_searchText, true, false, _matchCase, _wordSeparators, false).Select(static m => m.Range).ToList();
            _cachedFindMatches.Sort(TextRange.CompareRangesUsingStarts);
        }

        return _cachedFindMatches;
    }
}

/// <summary>Highlights the other occurrences of the selected text (upstream's SelectionHighlighter).</summary>
internal sealed class SelectionHighlighter : IDisposable
{
    public const string Id = "editor.contrib.selectionHighlighter";

    private readonly IMultiCursorEditor _editor;
    private readonly Func<TextModel, bool> _hasDocumentHighlightProvider;
    private readonly RunOnceScheduler _updateSoon;
    private readonly IMultiCursorFindController? _subscribedFindController;
    private bool _isEnabled;
    private bool _isEnabledMultiline;
    private int _maxLength;
    private string[] _decorationIds = [];
    private SelectionHighlighterState? _state;
    private bool _disposed;

    /// <param name="editor">The editor.</param>
    /// <param name="hasDocumentHighlightProvider">Whether a model has a document highlight provider (then the highlights stay out of the overview ruler).</param>
    /// <param name="runAfter">Runs an action once after a delay on the editor's thread.</param>
    public SelectionHighlighter(IMultiCursorEditor editor, Func<TextModel, bool> hasDocumentHighlightProvider, Func<TimeSpan, Action, IDisposable> runAfter)
    {
        _editor = editor;
        _hasDocumentHighlightProvider = hasDocumentHighlightProvider;
        ReadOptions();
        _updateSoon = new RunOnceScheduler(Update, TimeSpan.FromMilliseconds(300), runAfter);

        _editor.DidChangeConfiguration += OnDidChangeConfiguration;
        _editor.DidChangeCursorSelection += OnDidChangeCursorSelection;
        _editor.DidChangeModelContent += OnDidChangeModelContent;
        _subscribedFindController = editor.FindController;
        if (_subscribedFindController is not null)
        {
            _subscribedFindController.GetState().FindReplaceStateChange += OnFindReplaceStateChange;
        }

        _updateSoon.Schedule();
    }

    /// <summary>The ranges highlighted now (tests).</summary>
    internal IReadOnlyList<string> DecorationIds => _decorationIds;

    private void ReadOptions()
    {
        var options = _editor.MultiCursorOptions;
        _isEnabled = options.SelectionHighlight;
        _isEnabledMultiline = options.SelectionHighlightMultiline;
        _maxLength = options.SelectionHighlightMaxLength;
    }

    private void OnDidChangeConfiguration(string option)
    {
        ReadOptions();
        if (option == MultiCursorOptionNames.SelectedTextMatchMode)
        {
            _updateSoon.Schedule(TimeSpan.Zero);
        }
        else if (option == MultiCursorOptionNames.SelectionHighlight && !_isEnabled)
        {
            SetState(null);
        }
    }

    private void OnDidChangeCursorSelection(CursorStateChangedEvent e)
    {
        if (!_isEnabled)
        {
            // Early exit if nothing needs to be done!
            return;
        }

        if (e.Selections[0].IsEmpty)
        {
            if (e.Reason == CursorChangeReason.Explicit)
            {
                if (_state is not null)
                {
                    // no longer valid
                    SetState(null);
                }

                _updateSoon.Schedule();
            }
            else
            {
                SetState(null);
            }
        }
        else
        {
            Update();
        }
    }

    private void OnDidChangeModelContent()
    {
        if (_isEnabled)
        {
            _updateSoon.Schedule();
        }
    }

    private void OnFindReplaceStateChange(FindStateChangedEvent e) => Update();

    private void Update()
    {
        if (_disposed)
        {
            return;
        }

        SetState(CreateState(_state, _isEnabled, _isEnabledMultiline, _maxLength, _editor));
    }

    private static SelectionHighlighterState? CreateState(SelectionHighlighterState? oldState, bool isEnabled, bool isEnabledMultiline, int maxLength, IMultiCursorEditor editor)
    {
        if (!isEnabled || editor.Model is not { } model || editor.ViewModel is null)
        {
            return null;
        }

        if (!isEnabledMultiline)
        {
            var s = editor.GetSelection();
            if (s.StartLineNumber != s.EndLineNumber)
            {
                // multiline forbidden for perf reasons
                return null;
            }
        }

        if (editor.MultiCursorController is not { } multiCursorController)
        {
            return null;
        }

        if (editor.FindController is not { } findController)
        {
            return null;
        }

        var r = multiCursorController.GetSession(findController);
        if (r is null)
        {
            var allSelections = editor.GetSelections();
            if (allSelections.Count > 1)
            {
                var matchCase = findController.GetState().MatchCase;
                if (!MultiCursorSelectionController.ModelRangesContainSameText(model, allSelections.Select(static s => s.Range).ToList(), matchCase))
                {
                    return null;
                }
            }

            r = MultiCursorSession.Create(editor, findController);
        }

        if (r is null)
        {
            return null;
        }

        if (r.CurrentMatch is not null)
        {
            // This is an empty selection
            // Do not interfere with semantic word highlighting in the no selection case
            return null;
        }

        if (r.SearchText.Length > 0 && r.SearchText.All(static c => c is ' ' or '\t'))
        {
            // whitespace only selection
            return null;
        }

        if (maxLength > 0 && r.SearchText.Length > maxLength)
        {
            // very long selection
            return null;
        }

        var findState = findController.GetState();
        var caseSensitive = findState.MatchCase;

        // Return early if the find widget shows the exact same matches
        if (findState.IsRevealed)
        {
            var findStateSearchString = findState.SearchString;
            var mySearchString = r.SearchText;
            if (!caseSensitive)
            {
                findStateSearchString = findStateSearchString.ToLowerInvariant();
                mySearchString = mySearchString.ToLowerInvariant();
            }

            if (findStateSearchString == mySearchString && r.MatchCase == findState.MatchCase && r.WholeWord == findState.WholeWord && !findState.IsRegex)
            {
                return null;
            }
        }

        return new SelectionHighlighterState(model, r.SearchText, r.MatchCase, r.WholeWord ? editor.WordSeparators : null, oldState);
    }

    private void SetState(SelectionHighlighterState? newState)
    {
        _state = newState;

        if (_state is null)
        {
            ClearDecorations();
            return;
        }

        if (_editor.Model is not { } model)
        {
            return;
        }

        if (model.IsTooLargeForTokenization())
        {
            // the file is too large, so searching word under cursor in the whole document would be blocking the UI.
            return;
        }

        var allMatches = _state.FindMatches();

        var selections = _editor.GetSelections().Select(static s => s.Range).ToList();
        selections.Sort(TextRange.CompareRangesUsingStarts);

        // do not overlap with selection (issue #64 and #512)
        var matches = new List<TextRange>();
        for (int i = 0, j = 0, len = allMatches.Count, lenJ = selections.Count; i < len;)
        {
            var match = allMatches[i];

            if (j >= lenJ)
            {
                // finished all editor selections
                matches.Add(match);
                i++;
            }
            else
            {
                var cmp = TextRange.CompareRangesUsingStarts(match, selections[j]);
                if (cmp < 0)
                {
                    // match is before sel
                    if (selections[j].IsEmpty || !TextRange.AreIntersecting(match, selections[j]))
                    {
                        matches.Add(match);
                    }

                    i++;
                }
                else if (cmp > 0)
                {
                    // sel is before match
                    j++;
                }
                else
                {
                    // sel is equal to match
                    i++;
                    j++;
                }
            }
        }

        var occurrenceHighlighting = _editor.MultiCursorOptions.OccurrencesHighlight;
        var hasSemanticHighlights = occurrenceHighlighting && _hasDocumentHighlightProvider(model);
        var options = HighlightDecorations.GetSelectionHighlightDecorationOptions(hasSemanticHighlights);
        var decorations = matches.Select(range => new ModelDeltaDecoration(range, options)).ToList();
        _decorationIds = model.DeltaDecorations(_decorationIds, decorations, _editor.EditorId);
    }

    private void ClearDecorations()
    {
        if (_decorationIds.Length > 0 && _editor.Model is { } model && !model.IsDisposed())
        {
            _decorationIds = model.DeltaDecorations(_decorationIds, [], _editor.EditorId);
        }

        _decorationIds = [];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SetState(null);
        _disposed = true;
        _updateSoon.Dispose();
        _editor.DidChangeConfiguration -= OnDidChangeConfiguration;
        _editor.DidChangeCursorSelection -= OnDidChangeCursorSelection;
        _editor.DidChangeModelContent -= OnDidChangeModelContent;
        if (_subscribedFindController is not null)
        {
            _subscribedFindController.GetState().FindReplaceStateChange -= OnFindReplaceStateChange;
        }
    }
}

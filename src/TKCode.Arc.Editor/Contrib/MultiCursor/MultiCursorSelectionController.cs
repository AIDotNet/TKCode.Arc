// Ported from vscode (src/vs/editor/contrib/multicursor/browser/multicursor.ts: MultiCursorSessionResult,
// isFindWidgetSearch, getSelectionSearchOptions, MultiCursorSession, MultiCursorSelectionController and
// modelRangesContainSameText; commit b57d5c9e), MIT license.
// Differences:
//  - the controller works on IMultiCursorEditor (the control adapts CodeEditor to it) and is created by the editor's
//    contribution, not registered itself;
//  - the find controller is IMultiCursorFindController (the find widget's, or a state-only one);
//  - the aria announcement of added cursors (announceCursorChange) is not ported;
//  - Constants.MAX_SAFE_SMALL_INTEGER is int.MaxValue as the limit of select all.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>The selections a session step produces and what to reveal (upstream's MultiCursorSessionResult).</summary>
internal sealed class MultiCursorSessionResult(IReadOnlyList<TextSelection> selections, TextRange revealRange, ScrollType revealScrollType)
{
    public IReadOnlyList<TextSelection> Selections { get; } = selections;
    public TextRange RevealRange { get; } = revealRange;
    public ScrollType RevealScrollType { get; } = revealScrollType;
}

/// <summary>
/// A run of "add selection to next find match" and its kin: what is searched for and how (upstream's
/// MultiCursorSession).
/// </summary>
internal sealed class MultiCursorSession
{
    private readonly IMultiCursorEditor _editor;

    private MultiCursorSession(
        IMultiCursorEditor editor,
        IMultiCursorFindController findController,
        bool isDisconnectedFromFindController,
        bool usesFindOptions,
        string searchText,
        bool wholeWord,
        bool matchCase,
        TextSelection? currentMatch)
    {
        _editor = editor;
        FindController = findController;
        IsDisconnectedFromFindController = isDisconnectedFromFindController;
        UsesFindOptions = usesFindOptions;
        SearchText = searchText;
        WholeWord = wholeWord;
        MatchCase = matchCase;
        CurrentMatch = currentMatch;
    }

    public IMultiCursorFindController FindController { get; }

    public bool IsDisconnectedFromFindController { get; }

    public bool UsesFindOptions { get; }

    public string SearchText { get; }

    public bool WholeWord { get; }

    public bool MatchCase { get; }

    public TextSelection? CurrentMatch { get; set; }

    /// <summary>Whether the find widget owns what is searched for (upstream's isFindWidgetSearch).</summary>
    internal static bool IsFindWidgetSearch(IMultiCursorEditor editor, IMultiCursorFindController findController)
    {
        // Find widget owns entirely what we search for if:
        //  - focus is not in the editor (i.e. it is in the find widget)
        //  - and the search widget is visible
        //  - and the search string is non-empty
        var findState = findController.GetState();
        return !editor.HasTextFocus && findState.IsRevealed && findState.SearchString.Length > 0;
    }

    /// <summary>The options selected text is matched with (upstream's getSelectionSearchOptions).</summary>
    internal static (bool WholeWord, bool MatchCase, bool UsesFindOptions) GetSelectionSearchOptions(IMultiCursorEditor editor, IMultiCursorFindController findController)
    {
        var selectedTextMatchMode = editor.MultiCursorOptions.SelectedTextMatchMode;
        if (selectedTextMatchMode == SelectedTextMatchMode.FindOptions)
        {
            var findState = findController.GetState();
            return (findState.WholeWord, findState.MatchCase, true);
        }

        return (false, selectedTextMatchMode == SelectedTextMatchMode.CaseSensitive, false);
    }

    public static MultiCursorSession? Create(IMultiCursorEditor editor, IMultiCursorFindController findController)
    {
        if (editor.Model is not { } model)
        {
            return null;
        }

        var findState = findController.GetState();

        if (IsFindWidgetSearch(editor, findController))
        {
            // Find widget owns what is searched for
            return new MultiCursorSession(editor, findController, false, true, findState.SearchString, findState.WholeWord, findState.MatchCase, null);
        }

        var isDisconnectedFromFindController = false;
        bool wholeWord;
        bool matchCase;
        bool usesFindOptions;
        var selections = editor.GetSelections();
        if (selections.Count == 1 && selections[0].IsEmpty)
        {
            isDisconnectedFromFindController = true;
            wholeWord = true;
            matchCase = true;
            usesFindOptions = true;
        }
        else
        {
            (wholeWord, matchCase, usesFindOptions) = GetSelectionSearchOptions(editor, findController);
        }

        // Selection owns what is searched for
        var s = editor.GetSelection();

        string searchText;
        TextSelection? currentMatch = null;

        if (s.IsEmpty)
        {
            // selection is empty => expand to current word
            var word = editor.GetConfiguredWordAtPosition(s.Range.Start);
            if (word is null)
            {
                return null;
            }

            searchText = word.Word;
            currentMatch = new TextSelection(s.StartLineNumber, word.StartColumn, s.StartLineNumber, word.EndColumn);
        }
        else
        {
            searchText = model.GetValueInRange(s.Range).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        return new MultiCursorSession(editor, findController, isDisconnectedFromFindController, usesFindOptions, searchText, wholeWord, matchCase, currentMatch);
    }

    public MultiCursorSessionResult? AddSelectionToNextFindMatch()
    {
        if (_editor.Model is null || GetNextMatch() is not { } nextMatch)
        {
            return null;
        }

        var allSelections = _editor.GetSelections();
        return new MultiCursorSessionResult([.. allSelections, nextMatch], nextMatch.Range, ScrollType.Smooth);
    }

    public MultiCursorSessionResult? MoveSelectionToNextFindMatch()
    {
        if (_editor.Model is null || GetNextMatch() is not { } nextMatch)
        {
            return null;
        }

        var allSelections = _editor.GetSelections();
        return new MultiCursorSessionResult([.. allSelections.Take(allSelections.Count - 1), nextMatch], nextMatch.Range, ScrollType.Smooth);
    }

    private TextSelection? GetNextMatch()
    {
        if (_editor.Model is not { } model)
        {
            return null;
        }

        if (CurrentMatch is { } current)
        {
            CurrentMatch = null;
            return current;
        }

        HighlightFindOptions();

        var allSelections = _editor.GetSelections();
        var lastAddedSelection = allSelections[^1];
        var nextMatch = model.FindNextMatch(SearchText, lastAddedSelection.Range.End, false, MatchCase, WholeWord ? _editor.WordSeparators : null, false);
        if (nextMatch is null)
        {
            return null;
        }

        var range = nextMatch.Range;
        return new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn);
    }

    public MultiCursorSessionResult? AddSelectionToPreviousFindMatch()
    {
        if (_editor.Model is null || GetPreviousMatch() is not { } previousMatch)
        {
            return null;
        }

        var allSelections = _editor.GetSelections();
        return new MultiCursorSessionResult([.. allSelections, previousMatch], previousMatch.Range, ScrollType.Smooth);
    }

    public MultiCursorSessionResult? MoveSelectionToPreviousFindMatch()
    {
        if (_editor.Model is null || GetPreviousMatch() is not { } previousMatch)
        {
            return null;
        }

        var allSelections = _editor.GetSelections();
        return new MultiCursorSessionResult([.. allSelections.Take(allSelections.Count - 1), previousMatch], previousMatch.Range, ScrollType.Smooth);
    }

    private TextSelection? GetPreviousMatch()
    {
        if (_editor.Model is not { } model)
        {
            return null;
        }

        if (CurrentMatch is { } current)
        {
            CurrentMatch = null;
            return current;
        }

        HighlightFindOptions();

        var allSelections = _editor.GetSelections();
        var lastAddedSelection = allSelections[^1];
        var previousMatch = model.FindPreviousMatch(SearchText, lastAddedSelection.Range.Start, false, MatchCase, WholeWord ? _editor.WordSeparators : null, false);
        if (previousMatch is null)
        {
            return null;
        }

        var range = previousMatch.Range;
        return new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn);
    }

    public List<FindMatch> SelectAll(IReadOnlyList<TextRange>? searchScope)
    {
        if (_editor.Model is not { } model)
        {
            return [];
        }

        HighlightFindOptions();

        var wordSeparators = WholeWord ? _editor.WordSeparators : null;
        return searchScope is not null
            ? model.FindMatches(SearchText, searchScope, false, MatchCase, wordSeparators, false, int.MaxValue)
            : model.FindMatches(SearchText, true, false, MatchCase, wordSeparators, false, int.MaxValue);
    }

    private void HighlightFindOptions()
    {
        if (UsesFindOptions)
        {
            FindController.HighlightFindOptions();
        }
    }
}

/// <summary>
/// The editor's multi-cursor controller (upstream's MultiCursorSelectionController): keeps the session of the
/// "add selection to next find match" actions until the selection, the focus or the options change.
/// </summary>
internal sealed class MultiCursorSelectionController : IDisposable
{
    public const string Id = "editor.contrib.multiCursorController";

    private readonly IMultiCursorEditor _editor;
    private readonly List<Action> _sessionDispose = [];
    private bool _ignoreSelectionChange;
    private MultiCursorSession? _session;

    public MultiCursorSelectionController(IMultiCursorEditor editor)
    {
        _editor = editor;
    }

    public void Dispose() => EndSession();

    private void BeginSessionIfNeeded(IMultiCursorFindController findController)
    {
        if (_session is not null)
        {
            return;
        }

        // Create a new session
        var session = MultiCursorSession.Create(_editor, findController);
        if (session is null)
        {
            return;
        }

        _session = session;

        var newState = new NewFindState { SearchString = session.SearchText };
        if (session.IsDisconnectedFromFindController)
        {
            newState = newState with
            {
                WholeWordOverride = FindOptionOverride.True,
                MatchCaseOverride = FindOptionOverride.True,
                IsRegexOverride = FindOptionOverride.False,
            };
        }

        findController.GetState().Change(newState, false);

        void OnSelection(CursorStateChangedEvent e)
        {
            if (!_ignoreSelectionChange)
            {
                EndSession();
            }
        }

        void OnBlur() => EndSession();

        void OnFocus() => EndSession();

        void OnConfiguration(string option)
        {
            if (option == MultiCursorOptionNames.SelectedTextMatchMode)
            {
                EndSession();
            }
        }

        void OnFindState(FindStateChangedEvent e)
        {
            if (session.UsesFindOptions && (e.MatchCase || e.WholeWord))
            {
                EndSession();
            }
        }

        _editor.DidChangeCursorSelection += OnSelection;
        _sessionDispose.Add(() => _editor.DidChangeCursorSelection -= OnSelection);
        _editor.DidBlurEditorText += OnBlur;
        _sessionDispose.Add(() => _editor.DidBlurEditorText -= OnBlur);
        if (_editor.MultiCursorOptions.SelectedTextMatchMode != SelectedTextMatchMode.FindOptions)
        {
            _editor.DidFocusEditorText += OnFocus;
            _sessionDispose.Add(() => _editor.DidFocusEditorText -= OnFocus);
        }

        _editor.DidChangeConfiguration += OnConfiguration;
        _sessionDispose.Add(() => _editor.DidChangeConfiguration -= OnConfiguration);
        var state = findController.GetState();
        state.FindReplaceStateChange += OnFindState;
        _sessionDispose.Add(() => state.FindReplaceStateChange -= OnFindState);
    }

    private void EndSession()
    {
        foreach (var dispose in _sessionDispose)
        {
            dispose();
        }

        _sessionDispose.Clear();
        if (_session is { IsDisconnectedFromFindController: true } session)
        {
            session.FindController.GetState().Change(
                new NewFindState
                {
                    WholeWordOverride = FindOptionOverride.NotSet,
                    MatchCaseOverride = FindOptionOverride.NotSet,
                    IsRegexOverride = FindOptionOverride.NotSet,
                },
                false);
        }

        _session = null;
    }

    private void SetSelections(IReadOnlyList<TextSelection> selections)
    {
        _ignoreSelectionChange = true;
        try
        {
            _editor.SetSelections(selections);
        }
        finally
        {
            _ignoreSelectionChange = false;
        }
    }

    private TextSelection ExpandEmptyToWord(TextSelection selection)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var word = _editor.GetConfiguredWordAtPosition(selection.Range.Start);
        if (word is null)
        {
            return selection;
        }

        return new TextSelection(selection.StartLineNumber, word.StartColumn, selection.StartLineNumber, word.EndColumn);
    }

    private void ApplySessionResult(MultiCursorSessionResult? result)
    {
        if (result is null)
        {
            return;
        }

        SetSelections(result.Selections);
        _editor.RevealRangeInCenterIfOutsideViewport(result.RevealRange, result.RevealScrollType);
    }

    public MultiCursorSession? GetSession(IMultiCursorFindController findController) => _session;

    public void AddSelectionToNextFindMatch(IMultiCursorFindController findController)
    {
        if (_editor.Model is not { } model)
        {
            return;
        }

        if (_session is null)
        {
            // If there are multiple cursors, handle the case where they do not all select the same text.
            var allSelections = _editor.GetSelections();
            if (allSelections.Count > 1)
            {
                var matchCase = MultiCursorSession.IsFindWidgetSearch(_editor, findController)
                    ? findController.GetState().MatchCase
                    : MultiCursorSession.GetSelectionSearchOptions(_editor, findController).MatchCase;
                var selectionsContainSameText = ModelRangesContainSameText(model, allSelections.Select(static s => s.Range).ToList(), matchCase);
                if (!selectionsContainSameText)
                {
                    var resultingSelections = new TextSelection[allSelections.Count];
                    for (var i = 0; i < allSelections.Count; i++)
                    {
                        resultingSelections[i] = ExpandEmptyToWord(allSelections[i]);
                    }

                    _editor.SetSelections(resultingSelections);
                    return;
                }
            }
        }

        BeginSessionIfNeeded(findController);
        if (_session is { } session)
        {
            ApplySessionResult(session.AddSelectionToNextFindMatch());
        }
    }

    public void AddSelectionToPreviousFindMatch(IMultiCursorFindController findController)
    {
        BeginSessionIfNeeded(findController);
        if (_session is { } session)
        {
            ApplySessionResult(session.AddSelectionToPreviousFindMatch());
        }
    }

    public void MoveSelectionToNextFindMatch(IMultiCursorFindController findController)
    {
        BeginSessionIfNeeded(findController);
        if (_session is { } session)
        {
            ApplySessionResult(session.MoveSelectionToNextFindMatch());
        }
    }

    public void MoveSelectionToPreviousFindMatch(IMultiCursorFindController findController)
    {
        BeginSessionIfNeeded(findController);
        if (_session is { } session)
        {
            ApplySessionResult(session.MoveSelectionToPreviousFindMatch());
        }
    }

    public void SelectAll(IMultiCursorFindController findController)
    {
        if (_editor.Model is not { } model)
        {
            return;
        }

        List<FindMatch> matches;

        var findState = findController.GetState();

        // Special case: find widget owns entirely what we search for if:
        // - focus is not in the editor (i.e. it is in the find widget)
        // - and the search widget is visible
        // - and the search string is non-empty
        // - and we're searching for a regex
        var allowFindRegex = _editor.MultiCursorOptions.SelectedTextMatchMode == SelectedTextMatchMode.FindOptions || !_editor.HasTextFocus;
        if (allowFindRegex && findState.IsRevealed && findState.SearchString.Length > 0 && findState.IsRegex)
        {
            var wordSeparators = findState.WholeWord ? _editor.WordSeparators : null;
            matches = findState.SearchScope is { } scope
                ? model.FindMatches(findState.SearchString, scope, findState.IsRegex, findState.MatchCase, wordSeparators, false, int.MaxValue)
                : model.FindMatches(findState.SearchString, true, findState.IsRegex, findState.MatchCase, wordSeparators, false, int.MaxValue);
        }
        else
        {
            BeginSessionIfNeeded(findController);
            if (_session is not { } session)
            {
                return;
            }

            matches = session.SelectAll(findState.SearchScope);
        }

        if (matches.Count > 0)
        {
            var editorSelection = _editor.GetSelection();

            // Have the primary cursor remain the one where the action was invoked
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Range.IntersectRanges(editorSelection.Range) is not null)
                {
                    // bingo!
                    matches[i] = matches[0];
                    matches[0] = match;
                    break;
                }
            }

            SetSelections(matches.Select(static m => new TextSelection(m.Range.StartLineNumber, m.Range.StartColumn, m.Range.EndLineNumber, m.Range.EndColumn)).ToList());
        }
    }

    public void SelectAllUsingSelections(IReadOnlyList<TextSelection> selections)
    {
        if (selections.Count > 0)
        {
            SetSelections(selections);
        }
    }

    /// <summary>Whether every range holds the same text as the first (upstream's modelRangesContainSameText).</summary>
    internal static bool ModelRangesContainSameText(TextModel model, IReadOnlyList<TextRange> ranges, bool matchCase)
    {
        var selectedText = GetValueInRange(model, ranges[0], !matchCase);
        for (var i = 1; i < ranges.Count; i++)
        {
            var range = ranges[i];
            if (range.IsEmpty)
            {
                return false;
            }

            var thisSelectedText = GetValueInRange(model, range, !matchCase);
            if (selectedText != thisSelectedText)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetValueInRange(TextModel model, TextRange range, bool toLowerCase)
    {
        var text = model.GetValueInRange(range);
        return toLowerCase ? text.ToLowerInvariant() : text;
    }
}

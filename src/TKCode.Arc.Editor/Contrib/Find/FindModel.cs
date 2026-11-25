// Ported from vscode (src/vs/editor/contrib/find/browser/findModel.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the model runs on an IFindEditor (the part of ICodeEditor find uses), not on the editor widget;
//  - the context keys and keybindings of the file live with the editor commands (Controls' FindController); FIND_IDS is
//    FindIds;
//  - the RunOnceScheduler (100 ms after an edit) and the TimeoutTimer (240 ms for models too large to search while
//    typing) run on the editor's IdleScheduler; a scheduler that never runs anything (tests) means no delayed search;
//  - _largeReplaceAll replaces with a .NET regular expression translated from the search's JavaScript one
//    (JsRegex, multiline as upstream); its match arguments are the match and its groups only.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>The ids of the find commands and actions (upstream's FIND_IDS).</summary>
internal static class FindIds
{
    public const string StartFindAction = "actions.find";
    public const string StartFindWithSelection = "actions.findWithSelection";
    public const string StartFindWithArgs = "editor.actions.findWithArgs";
    public const string NextMatchFindAction = "editor.action.nextMatchFindAction";
    public const string PreviousMatchFindAction = "editor.action.previousMatchFindAction";
    public const string GoToMatchFindAction = "editor.action.goToMatchFindAction";
    public const string NextSelectionMatchFindAction = "editor.action.nextSelectionMatchFindAction";
    public const string PreviousSelectionMatchFindAction = "editor.action.previousSelectionMatchFindAction";
    public const string StartFindReplaceAction = "editor.action.startFindReplaceAction";
    public const string CloseFindWidgetCommand = "closeFindWidget";
    public const string ToggleCaseSensitiveCommand = "toggleFindCaseSensitive";
    public const string ToggleWholeWordCommand = "toggleFindWholeWord";
    public const string ToggleRegexCommand = "toggleFindRegex";
    public const string ToggleSearchScopeCommand = "toggleFindInSelection";
    public const string TogglePreserveCaseCommand = "togglePreserveCase";
    public const string ReplaceOneAction = "editor.action.replaceOne";
    public const string ReplaceAllAction = "editor.action.replaceAll";
    public const string SelectAllMatchesAction = "editor.action.selectAllMatches";
}

/// <summary>
/// Searches the editor's model as the find state changes, decorates the matches and moves between them, replaces and
/// selects them (upstream's FindModelBoundToEditorModel).
/// </summary>
internal sealed class FindModelBoundToEditorModel : IDisposable
{
    /// <summary>How many matches are decorated at most (upstream's MATCHES_LIMIT).</summary>
    public const int MatchesLimit = 19999;

    private const int ResearchDelay = 240;

    // upstream's Constants.MAX_SAFE_SMALL_INTEGER
    private const int MaxSafeSmallInteger = 1 << 30;

    private readonly IFindEditor _editor;
    private readonly FindReplaceState _state;
    private readonly FindDecorations _decorations;
    private readonly Action<FindCursorPositionChangedEvent> _onCursorPositionChanged;
    private readonly Action<ModelContentChangedEvent> _onModelContentChanged;
    private readonly Action<FindReplaceStateChangedEvent> _onStateChanged;
    private IDisposable? _updateDecorationsScheduled;
    private IDisposable? _startSearchingTimer;
    private bool _ignoreModelContentChanged;
    private bool _isDisposed;

    public FindModelBoundToEditorModel(IFindEditor editor, FindReplaceState state)
    {
        _editor = editor;
        _state = state;

        _decorations = new FindDecorations(editor);

        _onCursorPositionChanged = e =>
        {
            if (e.Reason is CursorChangeReason.Explicit or CursorChangeReason.Undo or CursorChangeReason.Redo)
            {
                _decorations.SetStartPosition(_editor.GetPosition());
            }
        };
        _editor.DidChangeCursorPosition += _onCursorPositionChanged;

        _onModelContentChanged = e =>
        {
            if (_ignoreModelContentChanged)
            {
                return;
            }
            if (e.IsFlush)
            {
                // a model.setValue() was called
                _decorations.Reset();
            }

            _decorations.SetStartPosition(_editor.GetPosition());
            ScheduleUpdateDecorations();
        };
        _editor.DidChangeModelContent += _onModelContentChanged;

        _onStateChanged = OnStateChanged;
        _state.FindReplaceStateChange += _onStateChanged;

        Research(false, _state.SearchScope, newFindScopeIsSet: true);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _startSearchingTimer?.Dispose();
        _startSearchingTimer = null;
        _updateDecorationsScheduled?.Dispose();
        _updateDecorationsScheduled = null;
        _editor.DidChangeCursorPosition -= _onCursorPositionChanged;
        _editor.DidChangeModelContent -= _onModelContentChanged;
        _state.FindReplaceStateChange -= _onStateChanged;
        if (_editor.GetModel() is { } model && !model.IsDisposed())
        {
            _decorations.Dispose();
        }
    }

    // upstream's _updateDecorationsScheduler: a RunOnceScheduler of 100 ms
    private void ScheduleUpdateDecorations()
    {
        _updateDecorationsScheduled?.Dispose();
        _updateDecorationsScheduled = _editor.Scheduler.RunAfter(TimeSpan.FromMilliseconds(100), () =>
        {
            _updateDecorationsScheduled = null;
            if (_isDisposed || _editor.GetModel() is null)
            {
                return;
            }

            Research(false);
        });
    }

    private void OnStateChanged(FindReplaceStateChangedEvent e)
    {
        if (_isDisposed)
        {
            // The find model is disposed during a find state changed event
            return;
        }
        if (_editor.GetModel() is not { } model)
        {
            // The find model will be disposed momentarily
            return;
        }
        if (e.SearchString || e.IsReplaceRevealed || e.IsRegex || e.WholeWord || e.MatchCase || e.SearchScope)
        {
            if (model.IsTooLargeForSyncing())
            {
                _startSearchingTimer?.Dispose();
                _startSearchingTimer = _editor.Scheduler.RunAfter(TimeSpan.FromMilliseconds(ResearchDelay), () =>
                {
                    _startSearchingTimer = null;
                    if (_isDisposed)
                    {
                        return;
                    }
                    if (e.SearchScope)
                    {
                        Research(e.MoveCursor, _state.SearchScope, newFindScopeIsSet: true);
                    }
                    else
                    {
                        Research(e.MoveCursor);
                    }
                });
            }
            else
            {
                if (e.SearchScope)
                {
                    Research(e.MoveCursor, _state.SearchScope, newFindScopeIsSet: true);
                }
                else
                {
                    Research(e.MoveCursor);
                }
            }
        }
    }

    private static TextRange GetSearchRange(TextModel model, TextRange? findScope) =>
        // If we have set now or before a find scope, use it for computing the search range
        findScope ?? model.GetFullModelRange();

    // newFindScopeIsSet false is upstream's newFindScope === undefined: keep the current scopes
    private void Research(bool moveCursor, IReadOnlyList<TextRange>? newFindScope = null, bool newFindScopeIsSet = false)
    {
        var model = _editor.GetModel()!;
        IReadOnlyList<TextRange>? findScopes = newFindScopeIsSet ? newFindScope : _decorations.GetFindScopes();
        if (findScopes is not null)
        {
            findScopes = findScopes.Select(findScope =>
            {
                if (findScope.StartLineNumber != findScope.EndLineNumber)
                {
                    var endLineNumber = findScope.EndLineNumber;

                    if (findScope.EndColumn == 1)
                    {
                        endLineNumber--;
                    }

                    return new TextRange(findScope.StartLineNumber, 1, endLineNumber, model.GetLineMaxColumn(endLineNumber));
                }

                return findScope;
            }).ToList();
        }

        var findMatches = FindMatches(findScopes, false, MatchesLimit);
        _decorations.Set(findMatches, findScopes);

        var editorSelection = _editor.GetSelection();
        var currentMatchesPosition = _decorations.GetCurrentMatchesPosition(editorSelection);
        if (currentMatchesPosition == 0 && findMatches.Count > 0)
        {
            // current selection is not on top of a match
            // try to find its nearest result from the top of the document
            var matchAfterSelection = FindFirstIdxMonotonousOrArrLen(findMatches, range => TextRange.CompareRangesUsingStarts(range.Range, editorSelection) >= 0);
            currentMatchesPosition = matchAfterSelection > 0 ? matchAfterSelection - 1 + 1 /* match position is one based */ : currentMatchesPosition;
        }

        _state.ChangeMatchInfo(currentMatchesPosition, _decorations.GetCount(), null);

        if (moveCursor && _editor.FindOptions.CursorMoveOnType)
        {
            MoveToNextMatchCore(_decorations.GetStartPosition());
        }
    }

    // upstream's findFirstIdxMonotonousOrArrLen: the first index where the monotonous predicate holds, or the length
    private static int FindFirstIdxMonotonousOrArrLen(IReadOnlyList<FindMatch> array, Func<FindMatch, bool> predicate)
    {
        int low = 0, high = array.Count;
        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            if (predicate(array[mid]))
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    private bool HasMatches() => _state.MatchesCount > 0;

    private bool CannotFind()
    {
        if (!HasMatches())
        {
            if (_decorations.GetFindScope() is { } findScope)
            {
                // Reveal the selection so user is reminded that 'selection find' is on.
                _editor.RevealRangeInCenterIfOutsideViewport(findScope);
            }

            return true;
        }

        return false;
    }

    private void SetCurrentFindMatch(TextRange match)
    {
        var matchesPosition = _decorations.SetCurrentFindMatch(match);
        _state.ChangeMatchInfo(matchesPosition, _decorations.GetCount(), match);

        _editor.SetSelection(TextSelection.FromRange(match));
        _editor.RevealRangeInCenterIfOutsideViewport(match);
    }

    private bool IsUsingLineStops() =>
        _state.IsRegex && (_state.SearchString.Contains('^') || _state.SearchString.Contains('$'));

    private TextPosition PrevSearchPosition(TextPosition before)
    {
        var isUsingLineStops = IsUsingLineStops();
        var (lineNumber, column) = before;
        var model = _editor.GetModel()!;

        if (isUsingLineStops || column == 1)
        {
            if (lineNumber == 1)
            {
                lineNumber = model.GetLineCount();
            }
            else
            {
                lineNumber--;
            }

            column = model.GetLineMaxColumn(lineNumber);
        }
        else
        {
            column--;
        }

        return new TextPosition(lineNumber, column);
    }

    private void MoveToPrevMatchCore(TextPosition before, bool isRecursed = false)
    {
        if (!_state.CanNavigateBack())
        {
            // we are beyond the first matched find result
            // instead of doing nothing, we should refocus the first item
            if (_decorations.MatchAfterPosition(before) is { } nextMatchRange)
            {
                SetCurrentFindMatch(nextMatchRange);
            }

            return;
        }
        if (_decorations.GetCount() < MatchesLimit)
        {
            var prevMatchRange = _decorations.MatchBeforePosition(before);

            if (prevMatchRange is { IsEmpty: true } empty && empty.Start == before)
            {
                before = PrevSearchPosition(before);
                prevMatchRange = _decorations.MatchBeforePosition(before);
            }

            if (prevMatchRange is { } range)
            {
                SetCurrentFindMatch(range);
            }

            return;
        }

        if (CannotFind())
        {
            return;
        }

        var findScope = _decorations.GetFindScope();
        var model = _editor.GetModel()!;
        var searchRange = GetSearchRange(model, findScope);

        // ...(----)...|...
        if (searchRange.End.IsBefore(before))
        {
            before = searchRange.End;
        }

        // ...|...(----)...
        if (before.IsBefore(searchRange.Start))
        {
            before = searchRange.End;
        }

        var position = before;

        var prevMatch = model.FindPreviousMatch(_state.SearchString, position, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull(), false);

        if (prevMatch is not null && prevMatch.Range.IsEmpty && prevMatch.Range.Start == position)
        {
            // Looks like we're stuck at this position, unacceptable!
            position = PrevSearchPosition(position);
            prevMatch = model.FindPreviousMatch(_state.SearchString, position, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull(), false);
        }

        if (prevMatch is null)
        {
            // there is precisely one match and selection is on top of it
            return;
        }

        if (!isRecursed && !searchRange.ContainsRange(prevMatch.Range))
        {
            MoveToPrevMatchCore(prevMatch.Range.Start, true);
            return;
        }

        SetCurrentFindMatch(prevMatch.Range);
    }

    public void MoveToPrevMatch() => MoveToPrevMatchCore(_editor.GetSelection().Range.Start);

    private TextPosition NextSearchPosition(TextPosition after)
    {
        var isUsingLineStops = IsUsingLineStops();
        var (lineNumber, column) = after;
        var model = _editor.GetModel()!;

        if (isUsingLineStops || column == model.GetLineMaxColumn(lineNumber))
        {
            if (lineNumber == model.GetLineCount())
            {
                lineNumber = 1;
            }
            else
            {
                lineNumber++;
            }

            column = 1;
        }
        else
        {
            column++;
        }

        return new TextPosition(lineNumber, column);
    }

    private void MoveToNextMatchCore(TextPosition after)
    {
        if (!_state.CanNavigateForward())
        {
            // we are beyond the last matched find result
            // instead of doing nothing, we should refocus the last item
            if (_decorations.MatchBeforePosition(after) is { } prevMatchRange)
            {
                SetCurrentFindMatch(prevMatchRange);
            }

            return;
        }
        if (_decorations.GetCount() < MatchesLimit)
        {
            var nextMatchRange = _decorations.MatchAfterPosition(after);

            if (nextMatchRange is { IsEmpty: true } empty && empty.Start == after)
            {
                // Looks like we're stuck at this position, unacceptable!
                after = NextSearchPosition(after);
                nextMatchRange = _decorations.MatchAfterPosition(after);
            }
            if (nextMatchRange is { } range)
            {
                SetCurrentFindMatch(range);
            }

            return;
        }

        if (GetNextMatch(after, false, true) is { } nextMatch)
        {
            SetCurrentFindMatch(nextMatch.Range);
        }
    }

    private FindMatch? GetNextMatch(TextPosition after, bool captureMatches, bool forceMove, bool isRecursed = false)
    {
        if (CannotFind())
        {
            return null;
        }

        var findScope = _decorations.GetFindScope();
        var model = _editor.GetModel()!;
        var searchRange = GetSearchRange(model, findScope);

        // ...(----)...|...
        if (searchRange.End.IsBefore(after))
        {
            after = searchRange.Start;
        }

        // ...|...(----)...
        if (after.IsBefore(searchRange.Start))
        {
            after = searchRange.Start;
        }

        var position = after;

        var nextMatch = model.FindNextMatch(_state.SearchString, position, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull(), captureMatches);

        if (forceMove && nextMatch is not null && nextMatch.Range.IsEmpty && nextMatch.Range.Start == position)
        {
            // Looks like we're stuck at this position, unacceptable!
            position = NextSearchPosition(position);
            nextMatch = model.FindNextMatch(_state.SearchString, position, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull(), captureMatches);
        }

        if (nextMatch is null)
        {
            // there is precisely one match and selection is on top of it
            return null;
        }

        if (!isRecursed && !searchRange.ContainsRange(nextMatch.Range))
        {
            return GetNextMatch(nextMatch.Range.End, captureMatches, forceMove, true);
        }

        return nextMatch;
    }

    public void MoveToNextMatch() => MoveToNextMatchCore(_editor.GetSelection().Range.End);

    public void MoveToMatch(int index)
    {
        if (_decorations.GetDecorationRangeAt(index) is { } decorationRange)
        {
            SetCurrentFindMatch(decorationRange);
        }
    }

    private ReplacePattern GetReplacePattern()
    {
        if (_state.IsRegex)
        {
            return ReplacePatternParser.ParseReplaceString(_state.ReplaceString);
        }

        return ReplacePattern.FromStaticValue(_state.ReplaceString);
    }

    public void Replace()
    {
        if (!HasMatches())
        {
            return;
        }

        var replacePattern = GetReplacePattern();
        var selection = _editor.GetSelection();
        var nextMatch = GetNextMatch(selection.Range.Start, true, false);
        if (nextMatch is not null)
        {
            if (selection.Range == nextMatch.Range)
            {
                // selection sits on a find match => replace it!
                var replaceString = replacePattern.BuildReplaceString(nextMatch.Matches, _state.PreserveCase);

                var command = new ReplaceCommand(selection, replaceString);

                ExecuteEditorCommand("replace", command);

                _decorations.SetStartPosition(new TextPosition(selection.StartLineNumber, selection.StartColumn + replaceString.Length));
                Research(true);
            }
            else
            {
                _decorations.SetStartPosition(_editor.GetPosition());
                SetCurrentFindMatch(nextMatch.Range);
            }
        }
    }

    private List<FindMatch> FindMatches(IReadOnlyList<TextRange>? findScopes, bool captureMatches, int limitResultCount)
    {
        var model = _editor.GetModel()!;
        IReadOnlyList<TextRange> searchRanges = findScopes is null
            ? [GetSearchRange(model, null)]
            : findScopes.Select(scope => GetSearchRange(model, scope)).ToList();

        return model.FindMatches(_state.SearchString, searchRanges, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull(), captureMatches, limitResultCount);
    }

    private string? WordSeparatorsOrNull() => _state.WholeWord ? _editor.WordSeparators : null;

    public void ReplaceAll()
    {
        if (!HasMatches())
        {
            return;
        }

        var findScopes = _decorations.GetFindScopes();

        if (findScopes is null && _state.MatchesCount >= MatchesLimit)
        {
            // Doing a replace on the entire file that is over ${MATCHES_LIMIT} matches
            LargeReplaceAll();
        }
        else
        {
            RegularReplaceAll(findScopes);
        }

        Research(false);
    }

    private void LargeReplaceAll()
    {
        var searchParams = new SearchParams(_state.SearchString, _state.IsRegex, _state.MatchCase, WordSeparatorsOrNull());
        var searchData = searchParams.ParseSearchRequest();
        if (searchData is null)
        {
            return;
        }

        // upstream: a multiline ('mu') copy of the search regex
        Regex searchRegex;
        try
        {
            searchRegex = JsRegex.Create(searchData.Regex.Source, isRegex: true, searchData.Regex.MatchCase, multiline: true).Regex;
        }
        catch (ArgumentException)
        {
            return;
        }

        var model = _editor.GetModel()!;
        var modelText = model.GetValue(EndOfLinePreference.LF);
        var fullModelRange = model.GetFullModelRange();

        var replacePattern = GetReplacePattern();
        string resultText;
        var preserveCase = _state.PreserveCase;

        if (replacePattern.HasReplacementPatterns || preserveCase)
        {
            resultText = searchRegex.Replace(modelText, match =>
            {
                var groups = new string?[match.Groups.Count];
                for (var i = 0; i < groups.Length; i++)
                {
                    groups[i] = match.Groups[i].Success ? match.Groups[i].Value : null;
                }

                return replacePattern.BuildReplaceString(groups, preserveCase);
            });
        }
        else
        {
            var replaceString = replacePattern.BuildReplaceString(null, preserveCase);
            resultText = searchRegex.Replace(modelText, _ => replaceString);
        }

        var command = new ReplaceCommandThatPreservesSelection(fullModelRange, resultText, _editor.GetSelection());
        ExecuteEditorCommand("replaceAll", command);
    }

    private void RegularReplaceAll(IReadOnlyList<TextRange>? findScopes)
    {
        var replacePattern = GetReplacePattern();
        // Get all the ranges (even more than the highlighted ones)
        var matches = FindMatches(findScopes, replacePattern.HasReplacementPatterns || _state.PreserveCase, MaxSafeSmallInteger);

        var replaceStrings = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            replaceStrings[i] = replacePattern.BuildReplaceString(matches[i].Matches, _state.PreserveCase);
        }

        var command = new ReplaceAllCommand(_editor.GetSelection(), matches.Select(m => m.Range).ToList(), replaceStrings);
        ExecuteEditorCommand("replaceAll", command);
    }

    public void SelectAllMatches()
    {
        if (!HasMatches())
        {
            return;
        }

        var findScopes = _decorations.GetFindScopes();

        // Get all the ranges (even more than the highlighted ones)
        var matches = FindMatches(findScopes, false, MaxSafeSmallInteger);
        var selections = matches.Select(m => new TextSelection(m.Range.StartLineNumber, m.Range.StartColumn, m.Range.EndLineNumber, m.Range.EndColumn)).ToList();

        // If one of the ranges is the editor selection, then maintain it as primary
        var editorSelection = _editor.GetSelection();
        for (var i = 0; i < selections.Count; i++)
        {
            var sel = selections[i];
            if (sel.Range == editorSelection.Range)
            {
                selections.RemoveAt(i);
                selections.Insert(0, editorSelection);
                break;
            }
        }

        _editor.SetSelections(selections);
    }

    private void ExecuteEditorCommand(string source, ICommand command)
    {
        try
        {
            _ignoreModelContentChanged = true;
            _editor.PushUndoStop();
            _editor.ExecuteCommand(source, command);
            _editor.PushUndoStop();
        }
        finally
        {
            _ignoreModelContentChanged = false;
        }
    }
}

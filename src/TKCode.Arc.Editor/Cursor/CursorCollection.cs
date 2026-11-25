// Ported from vscode (src/vs/editor/common/cursor/cursorCollection.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the single cursors are OneCursor (upstream's Cursor, see OneCursor.cs);
//  - setStates takes a read-only list (null leaves the cursors as they are); the getters return new lists;
//  - normalize sorts with a stable sort, as Array.prototype.sort is.

namespace TKCode.Arc.Editor.Cursor;

/// <summary>The cursors of an editor: the primary cursor first, then the secondary cursors.</summary>
internal sealed class CursorCollection
{
    private CursorContext _context;

    /// <summary>
    /// <c>_cursors[0]</c> is the primary cursor, thus <c>_cursors.Count &gt;= 1</c> is always true.
    /// <c>_cursors.Skip(1)</c> are secondary cursors.
    /// </summary>
    private readonly List<OneCursor> _cursors;

    // An index which identifies the last cursor that was added / moved (think Ctrl+drag)
    // This index refers to `cursors.slice(1)`, i.e. after removing the primary cursor.
    private int _lastAddedCursorIndex;

    public CursorCollection(CursorContext context)
    {
        _context = context;
        _cursors = [new OneCursor(context)];
        _lastAddedCursorIndex = 0;
    }

    public void Dispose()
    {
        foreach (var cursor in _cursors)
        {
            cursor.Dispose(_context);
        }
    }

    public void StartTrackingSelections()
    {
        foreach (var cursor in _cursors)
        {
            cursor.StartTrackingSelection(_context);
        }
    }

    public void StopTrackingSelections()
    {
        foreach (var cursor in _cursors)
        {
            cursor.StopTrackingSelection(_context);
        }
    }

    public void UpdateContext(CursorContext context) => _context = context;

    public void EnsureValidState()
    {
        foreach (var cursor in _cursors)
        {
            cursor.EnsureValidState(_context);
        }
    }

    public List<TextSelection> ReadSelectionFromMarkers() => _cursors.Select(c => c.ReadSelectionFromMarkers(_context)).ToList();

    public List<CursorState> GetAll() => _cursors.Select(c => c.AsCursorState()).ToList();

    public List<TextPosition> GetViewPositions() => _cursors.Select(c => c.ViewState.Position).ToList();

    public TextPosition GetTopMostViewPosition() =>
        ArraysFind.FindFirstMin(_cursors, Arrays.CompareBy<OneCursor, TextPosition>(c => c.ViewState.Position, ComparePositions))!.ViewState.Position;

    public TextPosition GetBottomMostViewPosition() =>
        ArraysFind.FindLastMax(_cursors, Arrays.CompareBy<OneCursor, TextPosition>(c => c.ViewState.Position, ComparePositions))!.ViewState.Position;

    public List<TextSelection> GetSelections() => _cursors.Select(c => c.ModelState.Selection).ToList();

    public List<TextSelection> GetViewSelections() => _cursors.Select(c => c.ViewState.Selection).ToList();

    public void SetSelections(IReadOnlyList<TextSelection> selections) => SetStates(CursorState.FromModelSelections(selections));

    public CursorState GetPrimaryCursor() => _cursors[0].AsCursorState();

    public void SetStates(IReadOnlyList<PartialCursorState>? states)
    {
        if (states is null)
        {
            return;
        }
        _cursors[0].SetState(_context, states[0].ModelState, states[0].ViewState);
        SetSecondaryStates(states.Skip(1).ToList());
    }

    /// <summary>Creates or disposes secondary cursors as necessary to match the number of <paramref name="secondaryStates"/>.</summary>
    private void SetSecondaryStates(IReadOnlyList<PartialCursorState> secondaryStates)
    {
        var secondaryCursorsLength = _cursors.Count - 1;
        var secondaryStatesLength = secondaryStates.Count;

        if (secondaryCursorsLength < secondaryStatesLength)
        {
            var createCnt = secondaryStatesLength - secondaryCursorsLength;
            for (var i = 0; i < createCnt; i++)
            {
                AddSecondaryCursor();
            }
        }
        else if (secondaryCursorsLength > secondaryStatesLength)
        {
            var removeCnt = secondaryCursorsLength - secondaryStatesLength;
            for (var i = 0; i < removeCnt; i++)
            {
                RemoveSecondaryCursor(_cursors.Count - 2);
            }
        }

        for (var i = 0; i < secondaryStatesLength; i++)
        {
            _cursors[i + 1].SetState(_context, secondaryStates[i].ModelState, secondaryStates[i].ViewState);
        }
    }

    public void KillSecondaryCursors() => SetSecondaryStates([]);

    private void AddSecondaryCursor()
    {
        _cursors.Add(new OneCursor(_context));
        _lastAddedCursorIndex = _cursors.Count - 1;
    }

    public int GetLastAddedCursorIndex()
    {
        if (_cursors.Count == 1 || _lastAddedCursorIndex == 0)
        {
            return 0;
        }
        return _lastAddedCursorIndex;
    }

    private void RemoveSecondaryCursor(int removeIndex)
    {
        if (_lastAddedCursorIndex >= removeIndex + 1)
        {
            _lastAddedCursorIndex--;
        }
        _cursors[removeIndex + 1].Dispose(_context);
        _cursors.RemoveAt(removeIndex + 1);
    }

    public void Normalize()
    {
        if (_cursors.Count == 1)
        {
            return;
        }
        var cursors = _cursors.ToList();

        var sortedCursors = new List<SortedCursor>(cursors.Count);
        for (int i = 0, len = cursors.Count; i < len; i++)
        {
            sortedCursors.Add(new SortedCursor(i, cursors[i].ModelState.Selection));
        }

        // A stable sort, as Array.prototype.sort.
        sortedCursors = sortedCursors
            .OrderBy(s => s.Selection, Comparer<TextSelection>.Create(static (a, b) => TextRange.CompareRangesUsingStarts(a, b)))
            .ToList();

        for (var sortedCursorIndex = 0; sortedCursorIndex < sortedCursors.Count - 1; sortedCursorIndex++)
        {
            var current = sortedCursors[sortedCursorIndex];
            var next = sortedCursors[sortedCursorIndex + 1];

            var currentSelection = current.Selection;
            var nextSelection = next.Selection;

            if (!_context.CursorConfig.MultiCursorMergeOverlapping)
            {
                continue;
            }

            bool shouldMergeCursors;
            if (nextSelection.IsEmpty || currentSelection.IsEmpty)
            {
                // Merge touching cursors if one of them is collapsed
                shouldMergeCursors = nextSelection.Range.Start.IsBeforeOrEqual(currentSelection.Range.End);
            }
            else
            {
                // Merge only overlapping cursors (i.e. allow touching ranges)
                shouldMergeCursors = nextSelection.Range.Start.IsBefore(currentSelection.Range.End);
            }

            if (shouldMergeCursors)
            {
                var winnerSortedCursorIndex = current.Index < next.Index ? sortedCursorIndex : sortedCursorIndex + 1;
                var looserSortedCursorIndex = current.Index < next.Index ? sortedCursorIndex + 1 : sortedCursorIndex;

                var looserIndex = sortedCursors[looserSortedCursorIndex].Index;
                var winnerIndex = sortedCursors[winnerSortedCursorIndex].Index;

                var looserSelection = sortedCursors[looserSortedCursorIndex].Selection;
                var winnerSelection = sortedCursors[winnerSortedCursorIndex].Selection;

                if (looserSelection != winnerSelection)
                {
                    var resultingRange = looserSelection.Range.PlusRange(winnerSelection);
                    var looserSelectionIsLTR = looserSelection.SelectionStartLineNumber == looserSelection.StartLineNumber && looserSelection.SelectionStartColumn == looserSelection.StartColumn;
                    var winnerSelectionIsLTR = winnerSelection.SelectionStartLineNumber == winnerSelection.StartLineNumber && winnerSelection.SelectionStartColumn == winnerSelection.StartColumn;

                    // Give more importance to the last added cursor (think Ctrl-dragging + hitting another cursor)
                    bool resultingSelectionIsLTR;
                    if (looserIndex == _lastAddedCursorIndex)
                    {
                        resultingSelectionIsLTR = looserSelectionIsLTR;
                        _lastAddedCursorIndex = winnerIndex;
                    }
                    else
                    {
                        // Winner takes it all
                        resultingSelectionIsLTR = winnerSelectionIsLTR;
                    }

                    TextSelection resultingSelection;
                    if (resultingSelectionIsLTR)
                    {
                        resultingSelection = new TextSelection(resultingRange.StartLineNumber, resultingRange.StartColumn, resultingRange.EndLineNumber, resultingRange.EndColumn);
                    }
                    else
                    {
                        resultingSelection = new TextSelection(resultingRange.EndLineNumber, resultingRange.EndColumn, resultingRange.StartLineNumber, resultingRange.StartColumn);
                    }

                    sortedCursors[winnerSortedCursorIndex].Selection = resultingSelection;
                    var resultingState = CursorState.FromModelSelection(resultingSelection);
                    cursors[winnerIndex].SetState(_context, resultingState.ModelState, resultingState.ViewState);
                }

                foreach (var sortedCursor in sortedCursors)
                {
                    if (sortedCursor.Index > looserIndex)
                    {
                        sortedCursor.Index--;
                    }
                }

                cursors.RemoveAt(looserIndex);
                sortedCursors.RemoveAt(looserSortedCursorIndex);
                RemoveSecondaryCursor(looserIndex - 1);

                sortedCursorIndex--;
            }
        }
    }

    private static int ComparePositions(TextPosition a, TextPosition b) => a.CompareTo(b);

    private sealed class SortedCursor
    {
        public SortedCursor(int index, TextSelection selection)
        {
            Index = index;
            Selection = selection;
        }

        public int Index { get; set; }

        public TextSelection Selection { get; set; }
    }
}

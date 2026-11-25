// Ported from vscode (src/vs/editor/common/cursor/cursor.ts: CursorModelState, AutoClosedAction, CompositionLineState and
// CompositionState; src/vs/editor/common/editorCommon.ts: ICursorState; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - ICursorState is the record SavedCursorState;
//  - CompositionOutcome is the record in CursorTypeOperations.cs; CompositionLineState is a record;
//  - AutoClosedAction.isValid sorts with a stable sort (Array.prototype.sort is stable) and, as upstream, sorts the
//    list of selections it is given;
//  - strings.commonPrefixLength and commonSuffixLength are private helpers of CompositionState.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>A cursor as saved in a view state (upstream's editorCommon.ICursorState).</summary>
/// <param name="InSelectionMode">Whether the cursor has a selection.</param>
/// <param name="SelectionStart">Where the selection starts; a line or column of 0 counts as missing.</param>
/// <param name="Position">Where the cursor is; a line or column of 0 counts as missing.</param>
internal sealed record SavedCursorState(bool InSelectionMode, TextPosition SelectionStart, TextPosition Position);

/// <summary>A snapshot of the cursor and the model state.</summary>
internal sealed class CursorModelState
{
    public CursorModelState(int modelVersionId, IReadOnlyList<CursorState> cursorState)
    {
        ModelVersionId = modelVersionId;
        CursorState = cursorState;
    }

    public int ModelVersionId { get; }

    public IReadOnlyList<CursorState> CursorState { get; }

    public static CursorModelState From(TextModel model, CursorsController cursor) =>
        new(model.GetVersionId(), cursor.GetCursorStates());

    public bool Equals(CursorModelState? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ModelVersionId != other.ModelVersionId)
        {
            return false;
        }
        if (CursorState.Count != other.CursorState.Count)
        {
            return false;
        }
        for (int i = 0, len = CursorState.Count; i < len; i++)
        {
            if (!CursorState[i].Equals(other.CursorState[i]))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>Closing characters the editor inserted, tracked while the cursors stay between them and their opening.</summary>
internal sealed class AutoClosedAction : IDisposable
{
    private readonly TextModel _model;

    private string[] _autoClosedCharactersDecorations;
    private string[] _autoClosedEnclosingDecorations;

    public AutoClosedAction(TextModel model, string[] autoClosedCharactersDecorations, string[] autoClosedEnclosingDecorations)
    {
        _model = model;
        _autoClosedCharactersDecorations = autoClosedCharactersDecorations;
        _autoClosedEnclosingDecorations = autoClosedEnclosingDecorations;
    }

    public static List<TextRange> GetAllAutoClosedCharacters(IReadOnlyList<AutoClosedAction> autoClosedActions)
    {
        var autoClosedCharacters = new List<TextRange>();
        foreach (var autoClosedAction in autoClosedActions)
        {
            autoClosedCharacters.AddRange(autoClosedAction.GetAutoClosedCharactersRanges());
        }
        return autoClosedCharacters;
    }

    public void Dispose()
    {
        _model.ChangeDecorations(accessor =>
        {
            _autoClosedCharactersDecorations = accessor.DeltaDecorations(_autoClosedCharactersDecorations, []);
            _autoClosedEnclosingDecorations = accessor.DeltaDecorations(_autoClosedEnclosingDecorations, []);
            return true;
        });
    }

    public List<TextRange> GetAutoClosedCharactersRanges()
    {
        var result = new List<TextRange>();
        for (var i = 0; i < _autoClosedCharactersDecorations.Length; i++)
        {
            if (_model.GetDecorationRange(_autoClosedCharactersDecorations[i]) is { } decorationRange)
            {
                result.Add(decorationRange);
            }
        }
        return result;
    }

    public bool IsValid(List<TextRange> selections)
    {
        var enclosingRanges = new List<TextRange>();
        for (var i = 0; i < _autoClosedEnclosingDecorations.Length; i++)
        {
            if (_model.GetDecorationRange(_autoClosedEnclosingDecorations[i]) is { } decorationRange)
            {
                enclosingRanges.Add(decorationRange);
                if (decorationRange.StartLineNumber != decorationRange.EndLineNumber)
                {
                    // Stop tracking if the range becomes multiline...
                    return false;
                }
            }
        }
        StableSort(enclosingRanges);

        StableSort(selections);

        for (var i = 0; i < selections.Count; i++)
        {
            if (i >= enclosingRanges.Count)
            {
                return false;
            }
            if (!enclosingRanges[i].StrictContainsRange(selections[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void StableSort(List<TextRange> ranges)
    {
        var sorted = ranges
            .Select((range, index) => (range, index))
            .OrderBy(x => x.range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .ThenBy(x => x.index)
            .Select(x => x.range)
            .ToList();
        ranges.Clear();
        ranges.AddRange(sorted);
    }
}

/// <summary>The text of a cursor's line and the cursor's offsets in it.</summary>
internal sealed record CompositionLineState(string Text, int LineNumber, int StartSelectionOffset, int EndSelectionOffset);

/// <summary>The lines of the cursors when a composition started, to deduce what it did when it ends.</summary>
internal sealed class CompositionState
{
    private readonly List<CompositionLineState>? _original;

    public CompositionState(TextModel textModel, IReadOnlyList<TextSelection> selections)
    {
        _original = Capture(textModel, selections);
    }

    private static List<CompositionLineState>? Capture(TextModel textModel, IReadOnlyList<TextSelection> selections)
    {
        var result = new List<CompositionLineState>();
        foreach (var selection in selections)
        {
            if (selection.StartLineNumber != selection.EndLineNumber)
            {
                return null;
            }
            var lineNumber = selection.StartLineNumber;
            result.Add(new CompositionLineState(
                textModel.GetLineContent(lineNumber),
                lineNumber,
                selection.StartColumn - 1,
                selection.EndColumn - 1));
        }
        return result;
    }

    /// <summary>
    /// Returns the inserted text during this composition.
    /// If the composition resulted in existing text being changed (i.e. not a pure insertion) it returns null.
    /// </summary>
    public List<CompositionOutcome>? DeduceOutcome(TextModel textModel, IReadOnlyList<TextSelection> selections)
    {
        if (_original is null)
        {
            return null;
        }
        var current = Capture(textModel, selections);
        if (current is null)
        {
            return null;
        }
        if (_original.Count != current.Count)
        {
            return null;
        }
        var result = new List<CompositionOutcome>();
        for (int i = 0, len = _original.Count; i < len; i++)
        {
            result.Add(DeduceOutcome(_original[i], current[i]));
        }
        return result;
    }

    private static CompositionOutcome DeduceOutcome(CompositionLineState original, CompositionLineState current)
    {
        var commonPrefix = Math.Min(
            original.StartSelectionOffset,
            Math.Min(current.StartSelectionOffset, CommonPrefixLength(original.Text, current.Text)));
        var commonSuffix = Math.Min(
            original.Text.Length - original.EndSelectionOffset,
            Math.Min(current.Text.Length - current.EndSelectionOffset, CommonSuffixLength(original.Text, current.Text)));
        var deletedText = Substring(original.Text, commonPrefix, original.Text.Length - commonSuffix);
        var insertedTextStartOffset = commonPrefix;
        var insertedTextEndOffset = current.Text.Length - commonSuffix;
        var insertedText = Substring(current.Text, insertedTextStartOffset, insertedTextEndOffset);
        var insertedTextRange = new TextRange(current.LineNumber, insertedTextStartOffset + 1, current.LineNumber, insertedTextEndOffset + 1);
        return new CompositionOutcome(
            deletedText,
            original.StartSelectionOffset - commonPrefix,
            original.EndSelectionOffset - commonPrefix,
            insertedText,
            current.StartSelectionOffset - commonPrefix,
            current.EndSelectionOffset - commonPrefix,
            insertedTextRange);
    }

    // String.prototype.substring(start, end): the two ends are swapped when start > end.
    private static string Substring(string str, int start, int end)
    {
        start = Math.Clamp(start, 0, str.Length);
        end = Math.Clamp(end, 0, str.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }
        return str.Substring(start, end - start);
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }
        return len;
    }

    private static int CommonSuffixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        var aLastIndex = a.Length - 1;
        var bLastIndex = b.Length - 1;
        for (var i = 0; i < len; i++)
        {
            if (a[aLastIndex - i] != b[bLastIndex - i])
            {
                return i;
            }
        }
        return len;
    }
}

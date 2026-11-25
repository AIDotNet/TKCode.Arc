// Ported from vscode (src/vs/base/common/diff/diff.ts and diffChange.ts, commit b57d5c9e), MIT license.
// Differences:
//  - a sequence is IDiffSequence: its elements are an int[] (characters, code points) or a string[] (lines; compared
//    by hash, then by value, as upstream's _getElements does with stringHash);
//  - the Levenshtein distance helpers at the end of diff.ts are not ported (nothing here calls them);
//  - Debug.Assert throws InvalidOperationException; the out-parameter arrays (midOriginalArr, quitEarlyArr) are ref
//    locals.

namespace TKCode.Arc.Editor.Diff;

/// <summary>A sequence to diff (upstream's ISequence).</summary>
internal interface IDiffSequence
{
    /// <summary>The elements as numbers (characters, code points); null when the sequence has string elements.</summary>
    int[]? GetIntElements() => null;

    /// <summary>The elements as strings (lines); null when the sequence has number elements.</summary>
    string[]? GetStringElements() => null;

    /// <summary>The element compared when prettifying (upstream's optional getStrictElement); null when not supported.</summary>
    string? GetStrictElement(int index) => null;
}

/// <summary>A difference between two sequences (upstream's DiffChange / IDiffChange).</summary>
internal sealed class DiffChange(int originalStart, int originalLength, int modifiedStart, int modifiedLength)
{
    /// <summary>The first element of the original sequence the change affects.</summary>
    public int OriginalStart { get; set; } = originalStart;

    /// <summary>How many elements of the original sequence the change affects.</summary>
    public int OriginalLength { get; set; } = originalLength;

    /// <summary>The first element of the modified sequence the change affects.</summary>
    public int ModifiedStart { get; set; } = modifiedStart;

    /// <summary>How many elements of the modified sequence the change affects (adds).</summary>
    public int ModifiedLength { get; set; } = modifiedLength;

    /// <summary>The end (exclusive) of the change in the original sequence.</summary>
    public int GetOriginalEnd() => OriginalStart + OriginalLength;

    /// <summary>The end (exclusive) of the change in the modified sequence.</summary>
    public int GetModifiedEnd() => ModifiedStart + ModifiedLength;

    public override string ToString() => $"({OriginalStart},{OriginalLength}) -> ({ModifiedStart},{ModifiedLength})";
}

/// <summary>The result of <see cref="LcsDiff.ComputeDiff(bool)"/> (upstream's IDiffResult).</summary>
internal sealed record DiffResult(bool QuitEarly, IReadOnlyList<DiffChange> Changes);

/// <summary>The characters of a string, by UTF-16 code unit (upstream's StringDiffSequence).</summary>
internal sealed class StringDiffSequence(string source) : IDiffSequence
{
    public int[] GetIntElements()
    {
        var characters = new int[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            characters[i] = source[i];
        }

        return characters;
    }
}

/// <summary>The code points of a string, with the UTF-16 offset of each (upstream's CodePointDiffSequence).</summary>
internal sealed class CodePointDiffSequence : IDiffSequence
{
    private readonly int[] _elements;
    private readonly int[] _offsets;

    public CodePointDiffSequence(string source)
    {
        var elementCount = 0;
        for (var offset = 0; offset < source.Length;)
        {
            var charCode = source[offset];
            offset += Strings.IsHighSurrogate(charCode) && offset + 1 < source.Length && Strings.IsLowSurrogate(source[offset + 1]) ? 2 : 1;
            elementCount++;
        }

        _elements = new int[elementCount];
        _offsets = new int[elementCount + 1];
        var elementIndex = 0;
        for (var offset = 0; offset < source.Length; elementIndex++)
        {
            _offsets[elementIndex] = offset;
            var charCode = source[offset];
            if (Strings.IsHighSurrogate(charCode) && offset + 1 < source.Length)
            {
                var nextCharCode = source[offset + 1];
                if (Strings.IsLowSurrogate(nextCharCode))
                {
                    _elements[elementIndex] = Strings.ComputeCodePoint(charCode, nextCharCode);
                    offset += 2;
                    continue;
                }
            }

            _elements[elementIndex] = charCode;
            offset++;
        }

        _offsets[elementCount] = source.Length;
    }

    public int[] GetIntElements() => _elements;

    public int GetOffset(int elementIndex) => _offsets[elementIndex];
}

/// <summary>Lines (or other strings) to diff, compared by value.</summary>
internal sealed class StringArrayDiffSequence(string[] elements) : IDiffSequence
{
    public string[] GetStringElements() => elements;
}

/// <summary>Decides whether the diff continues (upstream's IContinueProcessingPredicate).</summary>
internal delegate bool ContinueProcessingPredicate(int furthestOriginalIndex, int matchLengthOfLongest);

/// <summary>The diff of two strings (upstream's stringDiff).</summary>
internal static class StringDiff
{
    /// <summary>The changes that turn <paramref name="original"/> into <paramref name="modified"/>, in UTF-16 offsets; surrogate pairs are never split.</summary>
    public static IReadOnlyList<DiffChange> Compute(string original, string modified, bool pretty)
    {
        if (!ContainsSurrogatePair(original) && !ContainsSurrogatePair(modified))
        {
            return new LcsDiff(new StringDiffSequence(original), new StringDiffSequence(modified)).ComputeDiff(pretty).Changes;
        }

        var originalSequence = new CodePointDiffSequence(original);
        var modifiedSequence = new CodePointDiffSequence(modified);
        var changes = new LcsDiff(originalSequence, modifiedSequence).ComputeDiff(pretty).Changes;
        var result = new List<DiffChange>(changes.Count);
        foreach (var change in changes)
        {
            var originalStart = originalSequence.GetOffset(change.OriginalStart);
            var originalEnd = originalSequence.GetOffset(change.OriginalStart + change.OriginalLength);
            var modifiedStart = modifiedSequence.GetOffset(change.ModifiedStart);
            var modifiedEnd = modifiedSequence.GetOffset(change.ModifiedStart + change.ModifiedLength);
            result.Add(new DiffChange(originalStart, originalEnd - originalStart, modifiedStart, modifiedEnd - modifiedStart));
        }

        return result;
    }

    private static bool ContainsSurrogatePair(string value)
    {
        for (var i = 0; i + 1 < value.Length; i++)
        {
            if (Strings.IsHighSurrogate(value[i]) && Strings.IsLowSurrogate(value[i + 1]))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Collects changes while walking the trace (upstream's DiffChangeHelper).</summary>
internal sealed class DiffChangeHelper
{
    private readonly List<DiffChange> _changes = [];
    private int _originalStart = int.MaxValue;
    private int _modifiedStart = int.MaxValue;
    private int _originalCount;
    private int _modifiedCount;

    /// <summary>Marks the start of the next change in the set of differences.</summary>
    public void MarkNextChange()
    {
        // Only add to the list if there is something to add
        if (_originalCount > 0 || _modifiedCount > 0)
        {
            _changes.Add(new DiffChange(_originalStart, _originalCount, _modifiedStart, _modifiedCount));
        }

        // Reset for the next change
        _originalCount = 0;
        _modifiedCount = 0;
        _originalStart = int.MaxValue;
        _modifiedStart = int.MaxValue;
    }

    /// <summary>Adds the original element at the given position to the current change.</summary>
    public void AddOriginalElement(int originalIndex, int modifiedIndex)
    {
        _originalStart = Math.Min(_originalStart, originalIndex);
        _modifiedStart = Math.Min(_modifiedStart, modifiedIndex);
        _originalCount++;
    }

    /// <summary>Adds the modified element at the given position to the current change.</summary>
    public void AddModifiedElement(int originalIndex, int modifiedIndex)
    {
        _originalStart = Math.Min(_originalStart, originalIndex);
        _modifiedStart = Math.Min(_modifiedStart, modifiedIndex);
        _modifiedCount++;
    }

    /// <summary>The changes in order.</summary>
    public List<DiffChange> GetChanges()
    {
        if (_originalCount > 0 || _modifiedCount > 0)
        {
            MarkNextChange();
        }

        return _changes;
    }

    /// <summary>The changes in reverse order.</summary>
    public List<DiffChange> GetReverseChanges()
    {
        if (_originalCount > 0 || _modifiedCount > 0)
        {
            MarkNextChange();
        }

        _changes.Reverse();
        return _changes;
    }
}

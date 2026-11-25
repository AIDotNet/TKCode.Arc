// Ported from vscode (src/vs/editor/common/core/ranges/lineRange.ts: LineRange and the union of LineRangeSet, commit
// b57d5c9e), MIT license.
// Differences from upstream: only the members the tokenization part needs; a readonly record struct, so equality is by
// value; JoinMany merges the sorted ranges directly instead of through LineRangeSet (same result: touching and
// overlapping ranges become one); the invalid range error is an ArgumentException.

namespace TKCode.Arc.Editor;

/// <summary>A range of lines (1-based), the end exclusive.</summary>
internal readonly record struct LineRange
{
    public LineRange(int startLineNumber, int endLineNumberExclusive)
    {
        if (startLineNumber > endLineNumberExclusive)
        {
            throw new ArgumentException(
                $"startLineNumber {startLineNumber} cannot be after endLineNumberExclusive {endLineNumberExclusive}");
        }

        StartLineNumber = startLineNumber;
        EndLineNumberExclusive = endLineNumberExclusive;
    }

    public int StartLineNumber { get; }

    public int EndLineNumberExclusive { get; }

    public bool IsEmpty => StartLineNumber == EndLineNumberExclusive;

    /// <summary>The number of lines this line range spans.</summary>
    public int Length => EndLineNumberExclusive - StartLineNumber;

    /// <summary>The union of the line ranges of several lists, as sorted disjoint ranges.</summary>
    public static IReadOnlyList<LineRange> JoinMany(IEnumerable<IReadOnlyList<LineRange>> lineRanges)
    {
        var all = new List<LineRange>();
        foreach (var ranges in lineRanges)
        {
            all.AddRange(ranges);
        }

        if (all.Count == 0)
        {
            return [];
        }

        all.Sort(static (a, b) => a.StartLineNumber.CompareTo(b.StartLineNumber));
        var result = new List<LineRange>();
        var current = all[0];
        for (var i = 1; i < all.Count; i++)
        {
            var next = all[i];
            if (next.StartLineNumber <= current.EndLineNumberExclusive)
            {
                current = new LineRange(current.StartLineNumber, Math.Max(current.EndLineNumberExclusive, next.EndLineNumberExclusive));
            }
            else
            {
                result.Add(current);
                current = next;
            }
        }

        result.Add(current);
        return result;
    }

    public bool Contains(int lineNumber) => StartLineNumber <= lineNumber && lineNumber < EndLineNumberExclusive;

    public override string ToString() => $"[{StartLineNumber},{EndLineNumberExclusive})";
}

// Ported from vscode (src/vs/editor/common/core/ranges/offsetRange.ts, commit b57d5c9e), MIT license.
// Differences from upstream: only the members the tokenization part needs (the range priority queue of
// model/textModelTokens.ts); a readonly record struct, so equality is by value; the invalid range error is an
// ArgumentException.

namespace TKCode.Arc.Editor;

/// <summary>A range of offsets (0-based), the end exclusive.</summary>
internal readonly record struct OffsetRange
{
    public OffsetRange(int start, int endExclusive)
    {
        if (start > endExclusive)
        {
            throw new ArgumentException($"Invalid range: [{start}, {endExclusive})");
        }

        Start = start;
        EndExclusive = endExclusive;
    }

    public int Start { get; }

    public int EndExclusive { get; }

    public bool IsEmpty => Start == EndExclusive;

    public int Length => EndExclusive - Start;

    /// <summary>Adds a range to sorted, disjoint ranges, merging it with the ranges it touches or overlaps.</summary>
    public static void AddRange(OffsetRange range, List<OffsetRange> sortedRanges)
    {
        var i = 0;
        while (i < sortedRanges.Count && sortedRanges[i].EndExclusive < range.Start)
        {
            i++;
        }

        var j = i;
        while (j < sortedRanges.Count && sortedRanges[j].Start <= range.EndExclusive)
        {
            j++;
        }

        if (i == j)
        {
            sortedRanges.Insert(i, range);
        }
        else
        {
            var start = Math.Min(range.Start, sortedRanges[i].Start);
            var end = Math.Max(range.EndExclusive, sortedRanges[j - 1].EndExclusive);
            sortedRanges.RemoveRange(i, j - i);
            sortedRanges.Insert(i, new OffsetRange(start, end));
        }
    }

    public OffsetRange Delta(int offset) => new(Start + offset, EndExclusive + offset);

    public bool Contains(int offset) => Start <= offset && offset < EndExclusive;

    public override string ToString() => $"[{Start}, {EndExclusive})";
}

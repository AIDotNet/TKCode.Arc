// Ported from vscode (src/vs/editor/common/core/ranges/lineRange.ts: the LineRange members and LineRangeSet;
// src/vs/editor/common/core/ranges/offsetRange.ts: the OffsetRange members; src/vs/base/common/arraysFind.ts: the
// monotonous searches and MonotonousArray; src/vs/base/common/arrays.ts: groupAdjacentBy; commit b57d5c9e), MIT license.
// Differences:
// - The LineRange and OffsetRange members the diff needs are extension methods here (the shared record structs keep
//   only what other parts need), with upstream's names; "undefined" results are nullable structs.
// - toInclusiveRange uses int.MaxValue as the end column (upstream: Number.MAX_SAFE_INTEGER).
// - LineRangeSet lives in the diff namespace; only the members the diff and the diff editor use.

namespace TKCode.Arc.Editor.Diff;

/// <summary>The <see cref="LineRange"/> members of upstream's lineRange.ts that the diff uses.</summary>
internal static class LineRangeDiffExtensions
{
    extension(LineRange range)
    {
        public static LineRange OfLength(int startLineNumber, int length) => new(startLineNumber, startLineNumber + length);

        /// <summary>The lines a range touches, its end line included.</summary>
        public static LineRange FromRangeInclusive(TextRange textRange) => new(textRange.StartLineNumber, textRange.EndLineNumber + 1);

        /// <summary>The smallest line range that contains all of <paramref name="lineRanges"/> (at least one).</summary>
        public static LineRange Join(IReadOnlyList<LineRange> lineRanges)
        {
            if (lineRanges.Count == 0)
            {
                throw new InvalidOperationException("lineRanges cannot be empty");
            }

            var startLineNumber = lineRanges[0].StartLineNumber;
            var endLineNumberExclusive = lineRanges[0].EndLineNumberExclusive;
            for (var i = 1; i < lineRanges.Count; i++)
            {
                startLineNumber = Math.Min(startLineNumber, lineRanges[i].StartLineNumber);
                endLineNumberExclusive = Math.Max(endLineNumberExclusive, lineRanges[i].EndLineNumberExclusive);
            }

            return new LineRange(startLineNumber, endLineNumberExclusive);
        }

        public LineRange Join(LineRange other) =>
            new(Math.Min(range.StartLineNumber, other.StartLineNumber), Math.Max(range.EndLineNumberExclusive, other.EndLineNumberExclusive));

        public LineRange Delta(int offset) =>
            new(range.StartLineNumber + offset, range.EndLineNumberExclusive + offset);

        public LineRange DeltaLength(int offset) =>
            new(range.StartLineNumber, range.EndLineNumberExclusive + offset);

        /// <summary>The intersection, or null when the ranges are apart (touching ranges give an empty range).</summary>
        public LineRange? Intersect(LineRange other)
        {
            var startLineNumber = Math.Max(range.StartLineNumber, other.StartLineNumber);
            var endLineNumberExclusive = Math.Min(range.EndLineNumberExclusive, other.EndLineNumberExclusive);
            return startLineNumber <= endLineNumberExclusive ? new LineRange(startLineNumber, endLineNumberExclusive) : null;
        }

        public bool IntersectsStrict(LineRange other) =>
            range.StartLineNumber < other.EndLineNumberExclusive && other.StartLineNumber < range.EndLineNumberExclusive;

        public bool IntersectsOrTouches(LineRange other) =>
            range.StartLineNumber <= other.EndLineNumberExclusive && other.StartLineNumber <= range.EndLineNumberExclusive;

        public bool ContainsRange(LineRange other) =>
            range.StartLineNumber <= other.StartLineNumber && other.EndLineNumberExclusive <= range.EndLineNumberExclusive;

        /// <summary>The range from the first column of the first line to the end of the last line, or null when empty.</summary>
        public TextRange? ToInclusiveRange() =>
            range.IsEmpty ? null : new TextRange(range.StartLineNumber, 1, range.EndLineNumberExclusive - 1, int.MaxValue);

        /// <summary>The range from the start of the first line to the start of the end line (which need not exist).</summary>
        public TextRange ToExclusiveRange() =>
            new(range.StartLineNumber, 1, range.EndLineNumberExclusive, 1);

        /// <summary>The 0-based offset range of the lines (subtracts 1).</summary>
        public OffsetRange ToOffsetRange() => new(range.StartLineNumber - 1, range.EndLineNumberExclusive - 1);

        public int DistanceToRange(LineRange other)
        {
            if (range.EndLineNumberExclusive <= other.StartLineNumber)
            {
                return other.StartLineNumber - range.EndLineNumberExclusive;
            }

            if (other.EndLineNumberExclusive <= range.StartLineNumber)
            {
                return range.StartLineNumber - other.EndLineNumberExclusive;
            }

            return 0;
        }
    }
}

/// <summary>The <see cref="OffsetRange"/> members of upstream's offsetRange.ts that the diff uses.</summary>
internal static class OffsetRangeDiffExtensions
{
    extension(OffsetRange range)
    {
        public static OffsetRange OfLength(int length) => new(0, length);

        public static OffsetRange OfStartAndLength(int start, int length) => new(start, start + length);

        public OffsetRange DeltaStart(int offset) => new(range.Start + offset, range.EndExclusive);

        public OffsetRange DeltaEnd(int offset) => new(range.Start, range.EndExclusive + offset);

        public OffsetRange Join(OffsetRange other) =>
            new(Math.Min(range.Start, other.Start), Math.Max(range.EndExclusive, other.EndExclusive));

        /// <summary>The intersection, or null when the ranges are apart (touching ranges give an empty range).</summary>
        public OffsetRange? Intersect(OffsetRange other)
        {
            var start = Math.Max(range.Start, other.Start);
            var end = Math.Min(range.EndExclusive, other.EndExclusive);
            return start <= end ? new OffsetRange(start, end) : null;
        }

        public bool Intersects(OffsetRange other) =>
            Math.Max(range.Start, other.Start) < Math.Min(range.EndExclusive, other.EndExclusive);

        public bool IntersectsOrTouches(OffsetRange other) =>
            Math.Max(range.Start, other.Start) <= Math.Min(range.EndExclusive, other.EndExclusive);

        public bool ContainsRange(OffsetRange other) =>
            range.Start <= other.Start && other.EndExclusive <= range.EndExclusive;

        /// <summary>The items of <paramref name="array"/> in the range.</summary>
        public T[] Slice<T>(IReadOnlyList<T> array)
        {
            var result = new T[range.Length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = array[range.Start + i];
            }

            return result;
        }
    }
}

/// <summary>Binary searches over sorted lists with monotonous predicates (upstream's arraysFind.ts).</summary>
internal static class DiffArrays
{
    /// <summary>The index of the last item the predicate holds for, or startIdx - 1 (predicate: true ..., false ...).</summary>
    public static int FindLastIdxMonotonous<T>(IReadOnlyList<T> array, Func<T, bool> predicate, int startIdx = 0, int endIdxEx = -1)
    {
        var i = startIdx;
        var j = endIdxEx < 0 ? array.Count : endIdxEx;
        while (i < j)
        {
            var k = (i + j) / 2;
            if (predicate(array[k]))
            {
                i = k + 1;
            }
            else
            {
                j = k;
            }
        }

        return i - 1;
    }

    /// <summary>The index of the first item the predicate holds for, or the length (predicate: false ..., true ...).</summary>
    public static int FindFirstIdxMonotonousOrArrLen<T>(IReadOnlyList<T> array, Func<T, bool> predicate, int startIdx = 0, int endIdxEx = -1)
    {
        var i = startIdx;
        var j = endIdxEx < 0 ? array.Count : endIdxEx;
        while (i < j)
        {
            var k = (i + j) / 2;
            if (predicate(array[k]))
            {
                j = k;
            }
            else
            {
                i = k + 1;
            }
        }

        return i;
    }

    /// <summary>Whether the last item the predicate holds for was found, and that item.</summary>
    public static bool TryFindLastMonotonous<T>(IReadOnlyList<T> array, Func<T, bool> predicate, out T item)
    {
        var idx = FindLastIdxMonotonous(array, predicate);
        item = idx == -1 ? default! : array[idx];
        return idx != -1;
    }

    /// <summary>The last item the predicate holds for, or null.</summary>
    public static T? FindLastMonotonous<T>(IReadOnlyList<T> array, Func<T, bool> predicate)
        where T : class
    {
        var idx = FindLastIdxMonotonous(array, predicate);
        return idx == -1 ? null : array[idx];
    }

    /// <summary>Groups adjacent items for which <paramref name="shouldBeGrouped"/> holds.</summary>
    public static IEnumerable<List<T>> GroupAdjacentBy<T>(IEnumerable<T> items, Func<T, T, bool> shouldBeGrouped)
    {
        List<T>? currentGroup = null;
        var hasLast = false;
        T last = default!;
        foreach (var item in items)
        {
            if (hasLast && shouldBeGrouped(last, item))
            {
                currentGroup!.Add(item);
            }
            else
            {
                if (currentGroup is not null)
                {
                    yield return currentGroup;
                }

                currentGroup = [item];
            }

            last = item;
            hasLast = true;
        }

        if (currentGroup is not null)
        {
            yield return currentGroup;
        }
    }

    /// <summary>A stable sort (List.Sort is not stable, JavaScript's sort is).</summary>
    public static void StableSort<T>(List<T> list, Comparison<T> comparison)
    {
        var indexed = new (T Item, int Index)[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            indexed[i] = (list[i], i);
        }

        Array.Sort(indexed, (a, b) =>
        {
            var result = comparison(a.Item, b.Item);
            return result != 0 ? result : a.Index.CompareTo(b.Index);
        });
        for (var i = 0; i < indexed.Length; i++)
        {
            list[i] = indexed[i].Item;
        }
    }
}

/// <summary>
/// A sorted list queried with monotonous predicates that get weaker and weaker (upstream's MonotonousArray): each
/// search starts where the previous one ended.
/// </summary>
internal sealed class MonotonousArray<T>(IReadOnlyList<T> array)
    where T : class
{
    private int _findLastMonotonousLastIdx;

    public T? FindLastMonotonous(Func<T, bool> predicate)
    {
        var idx = DiffArrays.FindLastIdxMonotonous(array, predicate, _findLastMonotonousLastIdx);
        _findLastMonotonousLastIdx = idx + 1;
        return idx == -1 ? null : array[idx];
    }
}

/// <summary>A set of lines as sorted, disjoint, non-touching line ranges (upstream's LineRangeSet).</summary>
internal sealed class LineRangeSet
{
    private readonly List<LineRange> _normalizedRanges;

    public LineRangeSet()
    {
        _normalizedRanges = [];
    }

    public LineRangeSet(IEnumerable<LineRange> normalizedRanges)
    {
        _normalizedRanges = [.. normalizedRanges];
    }

    public IReadOnlyList<LineRange> Ranges => _normalizedRanges;

    public void AddRange(LineRange range)
    {
        if (range.Length == 0)
        {
            return;
        }

        // idx of first element that touches range or that is after range
        var joinRangeStartIdx = DiffArrays.FindFirstIdxMonotonousOrArrLen(_normalizedRanges, r => r.EndLineNumberExclusive >= range.StartLineNumber);
        // idx of element after { last element that touches range or that is before range }
        var joinRangeEndIdxExclusive = DiffArrays.FindLastIdxMonotonous(_normalizedRanges, r => r.StartLineNumber <= range.EndLineNumberExclusive) + 1;

        if (joinRangeStartIdx == joinRangeEndIdxExclusive)
        {
            _normalizedRanges.Insert(joinRangeStartIdx, range);
        }
        else if (joinRangeStartIdx == joinRangeEndIdxExclusive - 1)
        {
            _normalizedRanges[joinRangeStartIdx] = _normalizedRanges[joinRangeStartIdx].Join(range);
        }
        else
        {
            var joinRange = _normalizedRanges[joinRangeStartIdx].Join(_normalizedRanges[joinRangeEndIdxExclusive - 1]).Join(range);
            _normalizedRanges.RemoveRange(joinRangeStartIdx, joinRangeEndIdxExclusive - joinRangeStartIdx);
            _normalizedRanges.Insert(joinRangeStartIdx, joinRange);
        }
    }

    public bool Contains(int lineNumber) =>
        DiffArrays.TryFindLastMonotonous(_normalizedRanges, r => r.StartLineNumber <= lineNumber, out var range)
        && range.EndLineNumberExclusive > lineNumber;

    public bool Intersects(LineRange range) =>
        DiffArrays.TryFindLastMonotonous(_normalizedRanges, r => r.StartLineNumber < range.EndLineNumberExclusive, out var found)
        && found.EndLineNumberExclusive > range.StartLineNumber;

    /// <summary>The lines of <paramref name="range"/> that are not in this set.</summary>
    public LineRangeSet SubtractFrom(LineRange range)
    {
        var joinRangeStartIdx = DiffArrays.FindFirstIdxMonotonousOrArrLen(_normalizedRanges, r => r.EndLineNumberExclusive >= range.StartLineNumber);
        var joinRangeEndIdxExclusive = DiffArrays.FindLastIdxMonotonous(_normalizedRanges, r => r.StartLineNumber <= range.EndLineNumberExclusive) + 1;

        if (joinRangeStartIdx == joinRangeEndIdxExclusive)
        {
            return new LineRangeSet([range]);
        }

        var result = new List<LineRange>();
        var startLineNumber = range.StartLineNumber;
        for (var i = joinRangeStartIdx; i < joinRangeEndIdxExclusive; i++)
        {
            var r = _normalizedRanges[i];
            if (r.StartLineNumber > startLineNumber)
            {
                result.Add(new LineRange(startLineNumber, r.StartLineNumber));
            }

            startLineNumber = r.EndLineNumberExclusive;
        }

        if (startLineNumber < range.EndLineNumberExclusive)
        {
            result.Add(new LineRange(startLineNumber, range.EndLineNumberExclusive));
        }

        return new LineRangeSet(result);
    }

    public LineRangeSet GetIntersection(LineRangeSet other)
    {
        var result = new List<LineRange>();
        var i1 = 0;
        var i2 = 0;
        while (i1 < _normalizedRanges.Count && i2 < other._normalizedRanges.Count)
        {
            var r1 = _normalizedRanges[i1];
            var r2 = other._normalizedRanges[i2];
            if (r1.Intersect(r2) is { IsEmpty: false } i)
            {
                result.Add(i);
            }

            if (r1.EndLineNumberExclusive < r2.EndLineNumberExclusive)
            {
                i1++;
            }
            else
            {
                i2++;
            }
        }

        return new LineRangeSet(result);
    }

    public LineRangeSet GetWithDelta(int value) => new(_normalizedRanges.Select(r => r.Delta(value)));

    public override string ToString() => string.Join(", ", _normalizedRanges);
}

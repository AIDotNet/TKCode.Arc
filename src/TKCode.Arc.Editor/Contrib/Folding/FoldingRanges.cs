// Ported from vscode (src/vs/editor/contrib/folding/browser/foldingRanges.ts, commit b57d5c9e), MIT license.
// Differences:
//  - FoldRange is a class with settable IsCollapsed and Source, because sanitizeAndMerge changes the entries it takes
//    (as upstream changes the objects of its input arrays);
//  - sanitizeAndMerge takes either list through a small reader (upstream: a function of index chosen by
//    Array.isArray); the selection is upstream's SelectedLines (folding.ts), here ISelectedLines;
//  - the start and end indexes are uint arrays (upstream's Uint32Array), the parent index is packed into their top
//    bits exactly as upstream.

using System.Text;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>A range of lines (upstream's ILineRange).</summary>
internal interface ILineRange
{
    int StartLineNumber { get; }

    int EndLineNumber { get; }
}

/// <summary>Where a folding range comes from (upstream's FoldSource).</summary>
internal enum FoldSource
{
    /// <summary>A range provider (indentation or a language's folding range provider).</summary>
    Provider = 0,

    /// <summary>The user made it (Create Folding Range from Selection).</summary>
    UserDefined = 1,

    /// <summary>A collapsed range that the provider no longer reports, kept collapsed.</summary>
    Recovered = 2,
}

/// <summary>A folding range as data (upstream's FoldRange).</summary>
internal sealed class FoldRange : ILineRange
{
    public FoldRange(int startLineNumber, int endLineNumber, string? type, bool isCollapsed, FoldSource source)
    {
        StartLineNumber = startLineNumber;
        EndLineNumber = endLineNumber;
        Type = type;
        IsCollapsed = isCollapsed;
        Source = source;
    }

    public int StartLineNumber { get; }

    public int EndLineNumber { get; }

    /// <summary>The kind (<c>comment</c>, <c>imports</c>, <c>region</c>, ...), or null.</summary>
    public string? Type { get; }

    public bool IsCollapsed { get; set; }

    public FoldSource Source { get; set; }

    public override string ToString() => $"[{StartLineNumber}/{EndLineNumber} {(IsCollapsed ? "+" : "-")} {Source} {Type}]";
}

/// <summary>Whether a selection starts in a range of lines (upstream's SelectedLines in folding.ts).</summary>
internal interface ISelectedLines
{
    bool StartsInside(int startLine, int endLine);
}

/// <summary>Upstream's toSelectedLines (folding.ts).</summary>
internal static class SelectedLines
{
    /// <summary>The selected lines of <paramref name="selections"/>: whether one of them starts in a range of lines.</summary>
    public static ISelectedLines ToSelectedLines(IReadOnlyList<TextSelection>? selections) =>
        selections is null || selections.Count == 0 ? NoSelection.Instance : new Selections(selections);

    private sealed class NoSelection : ISelectedLines
    {
        public static readonly NoSelection Instance = new();

        public bool StartsInside(int startLine, int endLine) => false;
    }

    private sealed class Selections(IReadOnlyList<TextSelection> selections) : ISelectedLines
    {
        public bool StartsInside(int startLine, int endLine)
        {
            foreach (var s in selections)
            {
                var line = s.StartLineNumber;
                if (line >= startLine && line <= endLine)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// The folding regions of a document, sorted by start line, each with its collapsed state and source (upstream's
/// FoldingRegions). The parent of each region is computed on first use.
/// </summary>
internal sealed class FoldingRegions
{
    /// <summary>The most regions a document can have (upstream's MAX_FOLDING_REGIONS).</summary>
    public const int MaxFoldingRegions = 0xFFFF;

    /// <summary>The highest line number a region can have (upstream's MAX_LINE_NUMBER).</summary>
    public const int MaxLineNumber = 0xFFFFFF;

    private const uint MaskIndent = 0xFF000000;

    private readonly uint[] _startIndexes;
    private readonly uint[] _endIndexes;
    private readonly BitField _collapseStates;
    private readonly BitField _userDefinedStates;
    private readonly BitField _recoveredStates;
    private readonly string?[]? _types;
    private bool _parentsComputed;

    public FoldingRegions(uint[] startIndexes, uint[] endIndexes, string?[]? types = null)
    {
        if (startIndexes.Length != endIndexes.Length || startIndexes.Length > MaxFoldingRegions)
        {
            throw new ArgumentException("invalid startIndexes or endIndexes size");
        }

        _startIndexes = startIndexes;
        _endIndexes = endIndexes;
        _collapseStates = new BitField(startIndexes.Length);
        _userDefinedStates = new BitField(startIndexes.Length);
        _recoveredStates = new BitField(startIndexes.Length);
        _types = types;
        _parentsComputed = false;
    }

    /// <summary>No regions.</summary>
    public static FoldingRegions Empty() => new([], []);

    private void EnsureParentIndices()
    {
        if (_parentsComputed)
        {
            return;
        }

        _parentsComputed = true;
        var parentIndexes = new List<int>();
        for (int i = 0, len = _startIndexes.Length; i < len; i++)
        {
            var startLineNumber = _startIndexes[i];
            var endLineNumber = _endIndexes[i];
            if (startLineNumber > MaxLineNumber || endLineNumber > MaxLineNumber)
            {
                throw new InvalidOperationException("startLineNumber or endLineNumber must not exceed " + MaxLineNumber);
            }

            while (parentIndexes.Count > 0 && !IsInsideLast((int)startLineNumber, (int)endLineNumber))
            {
                parentIndexes.RemoveAt(parentIndexes.Count - 1);
            }

            var parentIndex = parentIndexes.Count > 0 ? parentIndexes[^1] : -1;
            parentIndexes.Add(i);
            _startIndexes[i] = startLineNumber + (uint)((parentIndex & 0xFF) << 24);
            _endIndexes[i] = endLineNumber + (uint)((parentIndex & 0xFF00) << 16);
        }

        bool IsInsideLast(int startLineNumber, int endLineNumber)
        {
            var index = parentIndexes[^1];
            return GetStartLineNumber(index) <= startLineNumber && GetEndLineNumber(index) >= endLineNumber;
        }
    }

    public int Length => _startIndexes.Length;

    public int GetStartLineNumber(int index) => (int)(_startIndexes[index] & MaxLineNumber);

    public int GetEndLineNumber(int index) => (int)(_endIndexes[index] & MaxLineNumber);

    public string? GetType(int index) => _types is not null ? _types[index] : null;

    public bool HasTypes() => _types is not null;

    public bool IsCollapsed(int index) => _collapseStates.Get(index);

    public void SetCollapsed(int index, bool newState) => _collapseStates.Set(index, newState);

    private bool IsUserDefined(int index) => _userDefinedStates.Get(index);

    private void SetUserDefined(int index, bool newState) => _userDefinedStates.Set(index, newState);

    private bool IsRecovered(int index) => _recoveredStates.Get(index);

    private void SetRecovered(int index, bool newState) => _recoveredStates.Set(index, newState);

    public FoldSource GetSource(int index)
    {
        if (IsUserDefined(index))
        {
            return FoldSource.UserDefined;
        }

        if (IsRecovered(index))
        {
            return FoldSource.Recovered;
        }

        return FoldSource.Provider;
    }

    public void SetSource(int index, FoldSource source)
    {
        if (source == FoldSource.UserDefined)
        {
            SetUserDefined(index, true);
            SetRecovered(index, false);
        }
        else if (source == FoldSource.Recovered)
        {
            SetUserDefined(index, false);
            SetRecovered(index, true);
        }
        else
        {
            SetUserDefined(index, false);
            SetRecovered(index, false);
        }
    }

    public bool SetCollapsedAllOfType(string type, bool newState)
    {
        var hasChanged = false;
        if (_types is not null)
        {
            for (var i = 0; i < _types.Length; i++)
            {
                if (_types[i] == type)
                {
                    SetCollapsed(i, newState);
                    hasChanged = true;
                }
            }
        }

        return hasChanged;
    }

    public FoldingRegion ToRegion(int index) => new(this, index);

    public int GetParentIndex(int index)
    {
        EnsureParentIndices();
        var parent = (int)(((_startIndexes[index] & MaskIndent) >> 24) + ((_endIndexes[index] & MaskIndent) >> 16));
        if (parent == MaxFoldingRegions)
        {
            return -1;
        }

        return parent;
    }

    public bool Contains(int index, int line) => GetStartLineNumber(index) <= line && GetEndLineNumber(index) >= line;

    private int FindIndex(int line)
    {
        int low = 0, high = _startIndexes.Length;
        if (high == 0)
        {
            return -1; // no children
        }

        while (low < high)
        {
            var mid = (low + high) / 2;
            if (line < GetStartLineNumber(mid))
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low - 1;
    }

    /// <summary>The index of the innermost region that contains <paramref name="line"/>, or -1.</summary>
    public int FindRange(int line)
    {
        var index = FindIndex(line);
        if (index >= 0)
        {
            var endLineNumber = GetEndLineNumber(index);
            if (endLineNumber >= line)
            {
                return index;
            }

            index = GetParentIndex(index);
            while (index != -1)
            {
                if (Contains(index, line))
                {
                    return index;
                }

                index = GetParentIndex(index);
            }
        }

        return -1;
    }

    public override string ToString()
    {
        var res = new StringBuilder();
        for (var i = 0; i < Length; i++)
        {
            if (i > 0)
            {
                res.Append(", ");
            }

            var abbr = GetSource(i) switch
            {
                FoldSource.UserDefined => 'u',
                FoldSource.Recovered => 'r',
                _ => ' ',
            };
            res.Append('[').Append(abbr).Append(IsCollapsed(i) ? '+' : '-').Append("] ")
                .Append(GetStartLineNumber(i)).Append('/').Append(GetEndLineNumber(i));
        }

        return res.ToString();
    }

    public FoldRange ToFoldRange(int index) => new(
        (int)(_startIndexes[index] & MaxLineNumber),
        (int)(_endIndexes[index] & MaxLineNumber),
        _types is not null ? _types[index] : null,
        IsCollapsed(index),
        GetSource(index));

    public static FoldingRegions FromFoldRanges(IReadOnlyList<FoldRange> ranges)
    {
        var rangesLength = ranges.Count;
        var startIndexes = new uint[rangesLength];
        var endIndexes = new uint[rangesLength];
        var types = new string?[rangesLength];
        var gotTypes = false;
        for (var i = 0; i < rangesLength; i++)
        {
            var range = ranges[i];
            startIndexes[i] = (uint)range.StartLineNumber;
            endIndexes[i] = (uint)range.EndLineNumber;
            types[i] = range.Type;
            if (!string.IsNullOrEmpty(range.Type))
            {
                gotTypes = true;
            }
        }

        var regions = new FoldingRegions(startIndexes, endIndexes, gotTypes ? types : null);
        for (var i = 0; i < rangesLength; i++)
        {
            if (ranges[i].IsCollapsed)
            {
                regions.SetCollapsed(i, true);
            }

            regions.SetSource(i, ranges[i].Source);
        }

        return regions;
    }

    /// <summary>
    /// Two inputs, each a FoldingRegions or a FoldRange list, are merged. Each input must be pre-sorted on
    /// startLineNumber. The first list is assumed to always include all regions currently defined by range providers.
    /// The second list only contains the previously collapsed and all manual ranges. If the line position matches, the
    /// range of the new range is taken, and the range is no longer manual. When an entry in one list overlaps an entry
    /// in the other, the second list's entry "wins" and overlapping entries in the first list are discarded. Invalid
    /// entries are discarded. An entry is invalid if: the start and end line numbers aren't a valid range of line
    /// numbers, it is out of sequence or has the same start line as a preceding entry, it overlaps a preceding entry
    /// and is not fully contained by that entry.
    /// </summary>
    public static List<FoldRange> SanitizeAndMerge(FoldingRegions rangesA, IReadOnlyList<FoldRange> rangesB, int? maxLineNumber, ISelectedLines? selection = null) =>
        SanitizeAndMerge(Reader.Of(rangesA), Reader.Of(rangesB), maxLineNumber, selection);

    /// <inheritdoc cref="SanitizeAndMerge(FoldingRegions, IReadOnlyList{FoldRange}, int?, ISelectedLines?)"/>
    public static List<FoldRange> SanitizeAndMerge(IReadOnlyList<FoldRange> rangesA, IReadOnlyList<FoldRange> rangesB, int? maxLineNumber, ISelectedLines? selection = null) =>
        SanitizeAndMerge(Reader.Of(rangesA), Reader.Of(rangesB), maxLineNumber, selection);

    /// <inheritdoc cref="SanitizeAndMerge(FoldingRegions, IReadOnlyList{FoldRange}, int?, ISelectedLines?)"/>
    public static List<FoldRange> SanitizeAndMerge(FoldingRegions rangesA, FoldingRegions rangesB, int? maxLineNumber, ISelectedLines? selection = null) =>
        SanitizeAndMerge(Reader.Of(rangesA), Reader.Of(rangesB), maxLineNumber, selection);

    private static List<FoldRange> SanitizeAndMerge(Reader getA, Reader getB, int? maxLine, ISelectedLines? selection)
    {
        var maxLineNumber = maxLine ?? int.MaxValue;
        var indexA = 0;
        var indexB = 0;
        var nextA = getA.Get(0);
        var nextB = getB.Get(0);

        var stackedRanges = new List<FoldRange>();
        FoldRange? topStackedRange = null;
        var prevLineNumber = 0;
        var resultRanges = new List<FoldRange>();

        while (nextA is not null || nextB is not null)
        {
            FoldRange? useRange = null;
            if (nextB is not null && (nextA is null || nextA.StartLineNumber >= nextB.StartLineNumber))
            {
                if (nextA is not null && nextA.StartLineNumber == nextB.StartLineNumber)
                {
                    if (nextB.Source == FoldSource.UserDefined)
                    {
                        // a user defined range (possibly unfolded)
                        useRange = nextB;
                    }
                    else
                    {
                        // a previously folded range or a (possibly unfolded) recovered range
                        useRange = nextA;
                        // stays collapsed if the range still has the same number of lines or the selection is not in the range or after it
                        useRange.IsCollapsed = nextB.IsCollapsed && (nextA.EndLineNumber == nextB.EndLineNumber
                            || selection is null || !selection.StartsInside(nextA.StartLineNumber + 1, nextA.EndLineNumber + 1));
                        useRange.Source = FoldSource.Provider;
                    }

                    nextA = getA.Get(++indexA); // not necessary, just for speed
                }
                else
                {
                    useRange = nextB;
                    if (nextB.IsCollapsed && nextB.Source == FoldSource.Provider)
                    {
                        // a previously collapsed range
                        useRange.Source = FoldSource.Recovered;
                    }
                }

                nextB = getB.Get(++indexB);
            }
            else
            {
                // nextA is next. The user folded B set takes precedence and we sometimes need to look
                // ahead in it to check for an upcoming conflict.
                var scanIndex = indexB;
                var prescanB = nextB;
                while (true)
                {
                    if (prescanB is null || prescanB.StartLineNumber > nextA!.EndLineNumber)
                    {
                        useRange = nextA;
                        break; // no conflict, use this nextA
                    }

                    if (prescanB.Source == FoldSource.UserDefined && prescanB.EndLineNumber > nextA.EndLineNumber)
                    {
                        // we found a user folded range, it wins
                        break; // without setting nextResult, so this nextA gets skipped
                    }

                    prescanB = getB.Get(++scanIndex);
                }

                nextA = getA.Get(++indexA);
            }

            if (useRange is not null)
            {
                while (topStackedRange is not null && topStackedRange.EndLineNumber < useRange.StartLineNumber)
                {
                    topStackedRange = stackedRanges.Count > 0 ? Pop(stackedRanges) : null;
                }

                if (useRange.EndLineNumber > useRange.StartLineNumber
                    && useRange.StartLineNumber > prevLineNumber
                    && useRange.EndLineNumber <= maxLineNumber
                    && (topStackedRange is null || topStackedRange.EndLineNumber >= useRange.EndLineNumber))
                {
                    resultRanges.Add(useRange);
                    prevLineNumber = useRange.StartLineNumber;
                    if (topStackedRange is not null)
                    {
                        stackedRanges.Add(topStackedRange);
                    }

                    topStackedRange = useRange;
                }
            }
        }

        return resultRanges;

        static FoldRange Pop(List<FoldRange> list)
        {
            var last = list[^1];
            list.RemoveAt(list.Count - 1);
            return last;
        }
    }

    // upstream's getIndexedFunction: the i-th entry of a list or of regions, or null past the end
    private readonly struct Reader
    {
        private readonly FoldingRegions? _regions;
        private readonly IReadOnlyList<FoldRange>? _list;

        private Reader(FoldingRegions? regions, IReadOnlyList<FoldRange>? list)
        {
            _regions = regions;
            _list = list;
        }

        public static Reader Of(FoldingRegions regions) => new(regions, null);

        public static Reader Of(IReadOnlyList<FoldRange> list) => new(null, list);

        public FoldRange? Get(int i)
        {
            if (_list is not null)
            {
                return i < _list.Count ? _list[i] : null;
            }

            return i < _regions!.Length ? _regions.ToFoldRange(i) : null;
        }
    }

    private sealed class BitField
    {
        private readonly uint[] _states;

        public BitField(int size)
        {
            var numWords = (size + 31) / 32;
            _states = new uint[numWords];
        }

        public bool Get(int index)
        {
            var arrayIndex = index / 32;
            var bit = index % 32;
            return (_states[arrayIndex] & (1u << bit)) != 0;
        }

        public void Set(int index, bool newState)
        {
            var arrayIndex = index / 32;
            var bit = index % 32;
            var value = _states[arrayIndex];
            if (newState)
            {
                _states[arrayIndex] = value | (1u << bit);
            }
            else
            {
                _states[arrayIndex] = value & ~(1u << bit);
            }
        }
    }
}

/// <summary>One region of <see cref="FoldingRegions"/> (upstream's FoldingRegion).</summary>
internal sealed class FoldingRegion : ILineRange
{
    private readonly FoldingRegions _ranges;

    public FoldingRegion(FoldingRegions ranges, int index)
    {
        _ranges = ranges;
        RegionIndex = index;
    }

    public int StartLineNumber => _ranges.GetStartLineNumber(RegionIndex);

    public int EndLineNumber => _ranges.GetEndLineNumber(RegionIndex);

    public int RegionIndex { get; }

    public int ParentIndex => _ranges.GetParentIndex(RegionIndex);

    public bool IsCollapsed => _ranges.IsCollapsed(RegionIndex);

    public bool ContainedBy(ILineRange range) => range.StartLineNumber <= StartLineNumber && range.EndLineNumber >= EndLineNumber;

    public bool ContainsLine(int lineNumber) => StartLineNumber <= lineNumber && lineNumber <= EndLineNumber;

    public bool HidesLine(int lineNumber) => StartLineNumber < lineNumber && lineNumber <= EndLineNumber;
}

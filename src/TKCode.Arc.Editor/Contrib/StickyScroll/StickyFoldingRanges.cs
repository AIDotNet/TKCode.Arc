// Ported from vscode (src/vs/editor/contrib/folding/browser/indentRangeProvider.ts: computeRanges and RangesCollector;
// syntaxRangeProvider.ts: collectSyntaxRanges, sanitizeRanges and its RangesCollector; foldingRanges.ts: the start /
// end / parent index part of FoldingRegions; commit b57d5c9e), MIT license.
//
// Sticky scroll's own copy of what it needs from the folding contribution, which is ported at the same time by another
// track (Contrib/Folding): the regions as start / end lines with a parent index, nothing about collapsed state.
// Differences:
//  - StickyFoldingRegions keeps the start, end and parent index in plain arrays (upstream packs the parent index into
//    the high bits of the start and end arrays and adds collapsed / user-defined / recovered bit fields);
//  - the folding limit reporter is a plain limit (5000, upstream's MAX_FOLDING_REGIONS_FOR_INDENT_DEFAULT) with no
//    report;
//  - region markers with different options are matched separately, as upstream does when the flags differ.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.StickyScroll;

/// <summary>Folding regions as start and end lines, each with the index of the region it is nested in (upstream's FoldingRegions).</summary>
internal sealed class StickyFoldingRegions
{
    /// <summary>Upstream's MAX_LINE_NUMBER: lines past it are not folded.</summary>
    public const int MaxLineNumber = 0xFFFFFF;

    private readonly int[] _startIndexes;
    private readonly int[] _endIndexes;
    private int[]? _parentIndexes;

    public StickyFoldingRegions(int[] startIndexes, int[] endIndexes, string?[]? types = null)
    {
        if (startIndexes.Length != endIndexes.Length)
        {
            throw new ArgumentException("invalid startIndexes or endIndexes size");
        }

        _startIndexes = startIndexes;
        _endIndexes = endIndexes;
        Types = types;
    }

    public int Length => _startIndexes.Length;

    /// <summary>The type of each region (comment, imports, region) when a provider gave them.</summary>
    public string?[]? Types { get; }

    public int GetStartLineNumber(int index) => _startIndexes[index];

    public int GetEndLineNumber(int index) => _endIndexes[index];

    /// <summary>The index of the innermost region that contains region <paramref name="index"/>, or -1 (upstream's getParentIndex).</summary>
    public int GetParentIndex(int index)
    {
        EnsureParentIndices();
        return _parentIndexes![index];
    }

    private void EnsureParentIndices()
    {
        if (_parentIndexes is not null)
        {
            return;
        }

        _parentIndexes = new int[_startIndexes.Length];
        var parentIndexes = new List<int>();
        for (var i = 0; i < _startIndexes.Length; i++)
        {
            var startLineNumber = _startIndexes[i];
            var endLineNumber = _endIndexes[i];
            if (startLineNumber > MaxLineNumber || endLineNumber > MaxLineNumber)
            {
                throw new InvalidOperationException("startLineNumber or endLineNumber must not exceed " + MaxLineNumber);
            }

            while (parentIndexes.Count > 0 && !IsInside(parentIndexes[^1], startLineNumber, endLineNumber))
            {
                parentIndexes.RemoveAt(parentIndexes.Count - 1);
            }

            _parentIndexes[i] = parentIndexes.Count > 0 ? parentIndexes[^1] : -1;
            parentIndexes.Add(i);
        }

        bool IsInside(int index, int startLineNumber, int endLineNumber) =>
            _startIndexes[index] <= startLineNumber && _endIndexes[index] >= endLineNumber;
    }
}

/// <summary>Folding regions from indentation and region markers (upstream's IndentRangeProvider.compute / computeRanges).</summary>
internal static class StickyIndentRanges
{
    /// <summary>Upstream's MAX_FOLDING_REGIONS_FOR_INDENT_DEFAULT.</summary>
    public const int DefaultLimit = 5000;

    /// <summary>The indentation regions of the model with its language's folding rules.</summary>
    public static StickyFoldingRegions Compute(TextModel model, LanguageConfigurationService languageConfigurationService)
    {
        var foldingRules = languageConfigurationService.GetLanguageConfiguration(model.GetLanguageId()).FoldingRules;
        return ComputeRanges(model, foldingRules.OffSide == true, foldingRules.Markers);
    }

    /// <summary>Upstream's computeRanges.</summary>
    public static StickyFoldingRegions ComputeRanges(TextModel model, bool offSide, FoldingMarkers? markers = null, int limit = DefaultLimit)
    {
        var tabSize = model.GetOptions().TabSize;
        var result = new RangesCollector(limit);

        Regex? pattern = null;
        Regex? startPattern = null;
        Regex? endPattern = null;
        if (markers is not null)
        {
            if (markers.Start.Options == markers.End.Options)
            {
                pattern = new Regex($"({markers.Start})|(?:{markers.End})", markers.Start.Options);
            }
            else
            {
                startPattern = markers.Start;
                endPattern = markers.End;
            }
        }

        var previousRegions = new List<PreviousRegion>();
        var sentinelLine = model.GetLineCount() + 1;
        previousRegions.Add(new PreviousRegion(-1, sentinelLine, sentinelLine)); // sentinel, to make sure there's at least one entry

        for (var line = model.GetLineCount(); line > 0; line--)
        {
            var lineContent = model.GetLineContent(line);
            var indent = TextModelUtils.ComputeIndentLevel(lineContent, tabSize);
            var previous = previousRegions[^1];
            if (indent == -1)
            {
                if (offSide)
                {
                    // for offSide languages, empty lines are associated to the previous block
                    // note: the next block is already written to the results, so this only
                    // impacts the end position of the block before
                    previous.EndAbove = line;
                }

                continue; // only whitespace
            }

            var isStartMatch = false;
            var isEndMatch = false;
            if (pattern is not null)
            {
                var m = pattern.Match(lineContent);
                if (m.Success)
                {
                    isStartMatch = m.Groups[1].Success;
                    isEndMatch = !isStartMatch;
                }
            }
            else
            {
                if (startPattern is not null)
                {
                    isStartMatch = startPattern.IsMatch(lineContent);
                }

                if (!isStartMatch && endPattern is not null)
                {
                    isEndMatch = endPattern.IsMatch(lineContent);
                }
            }

            if (isStartMatch || isEndMatch)
            {
                // folding pattern match
                if (isStartMatch)
                {
                    // start pattern match
                    // discard all regions until the folding pattern
                    var i = previousRegions.Count - 1;
                    while (i > 0 && previousRegions[i].Indent != -2)
                    {
                        i--;
                    }

                    if (i > 0)
                    {
                        previousRegions.RemoveRange(i + 1, previousRegions.Count - i - 1);
                        previous = previousRegions[i];

                        // new folding range from pattern, includes the end line
                        result.InsertFirst(line, previous.Line, indent);
                        previous.Line = line;
                        previous.Indent = indent;
                        previous.EndAbove = line;
                        continue;
                    }

                    // no end marker found, treat line as a regular line
                }
                else
                {
                    // end pattern match
                    previousRegions.Add(new PreviousRegion(-2, line, line));
                    continue;
                }
            }

            if (previous.Indent > indent)
            {
                // discard all regions with larger indent
                do
                {
                    previousRegions.RemoveAt(previousRegions.Count - 1);
                    previous = previousRegions[^1];
                }
                while (previous.Indent > indent);

                // new folding range
                var endLineNumber = previous.EndAbove - 1;
                if (endLineNumber - line >= 1)
                {
                    // needs at east size 1
                    result.InsertFirst(line, endLineNumber, indent);
                }
            }

            if (previous.Indent == indent)
            {
                previous.EndAbove = line;
            }
            else
            {
                // previous.indent < indent
                // new region with a bigger indent
                previousRegions.Add(new PreviousRegion(indent, line, line));
            }
        }

        return result.ToIndentRanges(model);
    }

    private sealed class PreviousRegion(int indent, int endAbove, int line)
    {
        // indent or -2 if a marker
        public int Indent { get; set; } = indent;

        // end line number for the region above
        public int EndAbove { get; set; } = endAbove;

        // start line of the region. Only used for marker regions.
        public int Line { get; set; } = line;
    }

    // upstream's RangesCollector of indentRangeProvider.ts
    private sealed class RangesCollector(int limit)
    {
        private readonly List<int> _startIndexes = [];
        private readonly List<int> _endIndexes = [];
        private readonly Dictionary<int, int> _indentOccurrences = [];

        public void InsertFirst(int startLineNumber, int endLineNumber, int indent)
        {
            if (startLineNumber > StickyFoldingRegions.MaxLineNumber || endLineNumber > StickyFoldingRegions.MaxLineNumber)
            {
                return;
            }

            _startIndexes.Add(startLineNumber);
            _endIndexes.Add(endLineNumber);
            if (indent < 1000)
            {
                _indentOccurrences[indent] = _indentOccurrences.GetValueOrDefault(indent) + 1;
            }
        }

        public StickyFoldingRegions ToIndentRanges(TextModel model)
        {
            var length = _startIndexes.Count;
            if (length <= limit)
            {
                // reverse and create arrays of the exact length
                var startIndexes = new int[length];
                var endIndexes = new int[length];
                for (int i = length - 1, k = 0; i >= 0; i--, k++)
                {
                    startIndexes[k] = _startIndexes[i];
                    endIndexes[k] = _endIndexes[i];
                }

                return new StickyFoldingRegions(startIndexes, endIndexes);
            }

            var entries = 0;
            var maxIndent = _indentOccurrences.Count == 0 ? 0 : _indentOccurrences.Keys.Max() + 1;
            for (var i = 0; i < maxIndent; i++)
            {
                var n = _indentOccurrences.GetValueOrDefault(i);
                if (n > 0)
                {
                    if (n + entries > limit)
                    {
                        maxIndent = i;
                        break;
                    }

                    entries += n;
                }
            }

            var tabSize = model.GetOptions().TabSize;
            // reverse and create arrays of the exact length
            var limitedStarts = new List<int>(limit);
            var limitedEnds = new List<int>(limit);
            for (var i = length - 1; i >= 0; i--)
            {
                var startIndex = _startIndexes[i];
                var lineContent = model.GetLineContent(startIndex);
                var indent = TextModelUtils.ComputeIndentLevel(lineContent, tabSize);
                if (indent < maxIndent || (indent == maxIndent && entries++ < limit))
                {
                    limitedStarts.Add(startIndex);
                    limitedEnds.Add(_endIndexes[i]);
                }
            }

            return new StickyFoldingRegions([.. limitedStarts], [.. limitedEnds]);
        }
    }
}

/// <summary>Folding regions from the folding range providers (upstream's SyntaxRangeProvider.compute).</summary>
internal static class StickySyntaxRanges
{
    /// <summary>A range a provider gave, with the provider's rank (upstream's IFoldingRangeData).</summary>
    public sealed record FoldingRangeData(int Start, int End, int Rank, FoldingRangeKind? Kind);

    /// <summary>
    /// The regions of the providers; null when none gave a list (upstream then falls back to its fallback provider,
    /// which sticky scroll does not give).
    /// </summary>
    public static async Task<StickyFoldingRegions?> Compute(IReadOnlyList<IFoldingRangeProvider> providers, TextModel model, CancellationToken token, int limit = StickyIndentRanges.DefaultLimit)
    {
        var ranges = await CollectSyntaxRanges(providers, model, token).ConfigureAwait(true);
        if (model.IsDisposed())
        {
            return null;
        }

        return ranges is null ? null : SanitizeRanges(ranges, limit);
    }

    // upstream's collectSyntaxRanges
    private static async Task<List<FoldingRangeData>?> CollectSyntaxRanges(IReadOnlyList<IFoldingRangeProvider> providers, TextModel model, CancellationToken token)
    {
        List<FoldingRangeData>? rangeData = null;
        var results = await Task.WhenAll(providers.Select(async (provider, i) =>
        {
            try
            {
                return (Index: i, Ranges: await provider.ProvideFoldingRanges(model, new FoldingContext(), token).ConfigureAwait(true));
            }
            catch (OperationCanceledException)
            {
                return (Index: i, Ranges: (IReadOnlyList<FoldingRange>?)null);
            }
            catch (Exception e)
            {
                UnexpectedErrors.Report(e);
                return (Index: i, Ranges: (IReadOnlyList<FoldingRange>?)null);
            }
        })).ConfigureAwait(true);

        if (token.IsCancellationRequested)
        {
            return null;
        }

        var nLines = model.GetLineCount();
        foreach (var (index, ranges) in results)
        {
            if (ranges is null)
            {
                continue;
            }

            rangeData ??= [];
            foreach (var r in ranges)
            {
                if (r.Start > 0 && r.End > r.Start && r.End <= nLines)
                {
                    rangeData.Add(new FoldingRangeData(r.Start, r.End, index, r.Kind));
                }
            }
        }

        return rangeData;
    }

    /// <summary>Sorts the ranges and drops those that cross their parent (upstream's sanitizeRanges).</summary>
    public static StickyFoldingRegions SanitizeRanges(List<FoldingRangeData> rangeData, int limit = StickyIndentRanges.DefaultLimit)
    {
        var sorted = rangeData.OrderBy(static d => d.Start).ThenBy(static d => d.Rank).ToList();
        var collector = new SyntaxRangesCollector(limit);
        FoldingRangeData? top = null;
        var previous = new List<FoldingRangeData>();
        foreach (var entry in sorted)
        {
            if (top is null)
            {
                top = entry;
                collector.Add(entry.Start, entry.End, entry.Kind?.Value, previous.Count);
            }
            else if (entry.Start > top.Start)
            {
                if (entry.End <= top.End)
                {
                    previous.Add(top);
                    top = entry;
                    collector.Add(entry.Start, entry.End, entry.Kind?.Value, previous.Count);
                }
                else
                {
                    if (entry.Start > top.End)
                    {
                        do
                        {
                            top = previous.Count > 0 ? previous[^1] : null;
                            if (previous.Count > 0)
                            {
                                previous.RemoveAt(previous.Count - 1);
                            }
                        }
                        while (top is not null && entry.Start > top.End);

                        if (top is not null)
                        {
                            previous.Add(top);
                        }

                        top = entry;
                    }

                    collector.Add(entry.Start, entry.End, entry.Kind?.Value, previous.Count);
                }
            }
        }

        return collector.ToIndentRanges();
    }

    // upstream's RangesCollector of syntaxRangeProvider.ts
    private sealed class SyntaxRangesCollector(int limit)
    {
        private readonly List<int> _startIndexes = [];
        private readonly List<int> _endIndexes = [];
        private readonly List<int> _nestingLevels = [];
        private readonly int[] _nestingLevelCounts = new int[30];
        private readonly List<string?> _types = [];

        public void Add(int startLineNumber, int endLineNumber, string? type, int nestingLevel)
        {
            if (startLineNumber > StickyFoldingRegions.MaxLineNumber || endLineNumber > StickyFoldingRegions.MaxLineNumber)
            {
                return;
            }

            _startIndexes.Add(startLineNumber);
            _endIndexes.Add(endLineNumber);
            _nestingLevels.Add(nestingLevel);
            _types.Add(type);
            if (nestingLevel < 30)
            {
                _nestingLevelCounts[nestingLevel]++;
            }
        }

        public StickyFoldingRegions ToIndentRanges()
        {
            var length = _startIndexes.Count;
            if (length <= limit)
            {
                return new StickyFoldingRegions([.. _startIndexes], [.. _endIndexes], [.. _types]);
            }

            var entries = 0;
            var maxLevel = _nestingLevelCounts.Length;
            for (var i = 0; i < _nestingLevelCounts.Length; i++)
            {
                var n = _nestingLevelCounts[i];
                if (n > 0)
                {
                    if (n + entries > limit)
                    {
                        maxLevel = i;
                        break;
                    }

                    entries += n;
                }
            }

            var startIndexes = new List<int>(limit);
            var endIndexes = new List<int>(limit);
            var types = new List<string?>(limit);
            for (var i = 0; i < length; i++)
            {
                var level = _nestingLevels[i];
                if (level < maxLevel || (level == maxLevel && entries++ < limit))
                {
                    startIndexes.Add(_startIndexes[i]);
                    endIndexes.Add(_endIndexes[i]);
                    types.Add(_types[i]);
                }
            }

            return new StickyFoldingRegions([.. startIndexes], [.. endIndexes], [.. types]);
        }
    }
}

// Ported from vscode (src/vs/editor/contrib/folding/browser/indentRangeProvider.ts, and the RangeProvider and
// FoldingLimitReporter interfaces of folding.ts, commit b57d5c9e), MIT license.
// Differences:
//  - compute returns a Task (upstream: a Promise) and takes a CancellationToken;
//  - the folding markers are .NET regular expressions (the language configurations translate upstream's JavaScript
//    literals with JsRegex); when both have the same options they are joined into one expression as upstream does
//    when the flags are equal; .NET expressions keep no lastIndex, so upstream's resets are not needed;
//  - FoldingLimitReporter.update takes the limit as an int? (null for upstream's false).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>Computes the folding regions of a document (upstream's RangeProvider in folding.ts).</summary>
internal interface IRangeProvider : IDisposable
{
    /// <summary>The provider's id (<c>indent</c>, <c>syntax</c>).</summary>
    string Id { get; }

    /// <summary>The regions, or null when there are none to report (the request was cancelled or there is no result).</summary>
    Task<FoldingRegions?> Compute(CancellationToken cancellationToken);
}

/// <summary>How many regions a provider may report, and what it computed (upstream's FoldingLimitReporter).</summary>
internal interface IFoldingLimitReporter
{
    /// <summary>The most regions to keep.</summary>
    int Limit { get; }

    /// <summary>Reports how many regions were computed and, when they were cut down, the limit (else null).</summary>
    void Update(int computed, int? limited);
}

/// <summary>A limit reporter with a fixed limit and an optional callback (upstream's object literals in the tests and in _executeFoldingRangeProvider).</summary>
internal sealed class FoldingLimitReporter(int limit, Action<int, int?>? update = null) : IFoldingLimitReporter
{
    public int Limit { get; } = limit;

    public void Update(int computed, int? limited) => update?.Invoke(computed, limited);
}

/// <summary>The regions of a document from its indentation and folding markers (upstream's IndentRangeProvider).</summary>
internal sealed class IndentRangeProvider : IRangeProvider
{
    /// <summary>The id of this provider.</summary>
    public const string IdIndentProvider = "indent";

    /// <summary>The default limit (upstream's MAX_FOLDING_REGIONS_FOR_INDENT_DEFAULT).</summary>
    public const int MaxFoldingRegionsForIndentDefault = 5000;

    private static readonly IFoldingLimitReporter s_foldingRangesLimitDefault = new FoldingLimitReporter(MaxFoldingRegionsForIndentDefault);

    private readonly TextModel _editorModel;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly IFoldingLimitReporter _foldingRangesLimit;

    public IndentRangeProvider(TextModel editorModel, LanguageConfigurationService languageConfigurationService, IFoldingLimitReporter foldingRangesLimit)
    {
        _editorModel = editorModel;
        _languageConfigurationService = languageConfigurationService;
        _foldingRangesLimit = foldingRangesLimit;
    }

    public string Id => IdIndentProvider;

    public void Dispose()
    {
    }

    public Task<FoldingRegions?> Compute(CancellationToken cancellationToken)
    {
        var foldingRules = _languageConfigurationService.GetLanguageConfiguration(_editorModel.GetLanguageId()).FoldingRules;
        var offSide = foldingRules?.OffSide == true;
        var markers = foldingRules?.Markers;
        return Task.FromResult<FoldingRegions?>(ComputeRanges(_editorModel, offSide, markers, _foldingRangesLimit));
    }

    // upstream's PreviousRegion: indent or -2 if a marker; the end line number for the region above; the start line of
    // the region (only used for marker regions)
    private sealed class PreviousRegion(int indent, int endAbove, int line)
    {
        public int Indent { get; set; } = indent;

        public int EndAbove { get; set; } = endAbove;

        public int Line { get; set; } = line;
    }

    /// <summary>The regions from indentation and markers (upstream's computeRanges).</summary>
    public static FoldingRegions ComputeRanges(TextModel model, bool offSide, FoldingMarkers? markers = null, IFoldingLimitReporter? foldingRangesLimit = null)
    {
        var tabSize = model.GetOptions().TabSize;
        var result = new RangesCollector(foldingRangesLimit ?? s_foldingRangesLimitDefault);

        Regex? pattern = null;
        Regex? startPattern = null;
        Regex? endPattern = null;
        if (markers is not null)
        {
            if (markers.Start.Options == markers.End.Options)
            {
                pattern = new Regex($"({markers.Start})|(?:{markers.End})", markers.Start.Options, markers.Start.MatchTimeout);
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
                    isStartMatch = m.Groups[1].Success && m.Groups[1].Length > 0;
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
                    // start pattern match: discard all regions until the folding pattern
                    var i = previousRegions.Count - 1;
                    while (i > 0 && previousRegions[i].Indent != -2)
                    {
                        i--;
                    }

                    if (i > 0)
                    {
                        previousRegions.RemoveRange(i + 1, previousRegions.Count - (i + 1));
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
                // previous.indent < indent: new region with a bigger indent
                previousRegions.Add(new PreviousRegion(indent, line, line));
            }
        }

        return result.ToIndentRanges(model);
    }
}

/// <summary>Collects indentation regions from the bottom up (upstream's RangesCollector of indentRangeProvider.ts; public there for testing).</summary>
internal sealed class RangesCollector
{
    private readonly List<int> _startIndexes = new();
    private readonly List<int> _endIndexes = new();
    private readonly List<int> _indentOccurrences = new();
    private readonly IFoldingLimitReporter _foldingRangesLimit;

    public RangesCollector(IFoldingLimitReporter foldingRangesLimit)
    {
        _foldingRangesLimit = foldingRangesLimit;
    }

    public void InsertFirst(int startLineNumber, int endLineNumber, int indent)
    {
        if (startLineNumber > FoldingRegions.MaxLineNumber || endLineNumber > FoldingRegions.MaxLineNumber)
        {
            return;
        }

        _startIndexes.Add(startLineNumber);
        _endIndexes.Add(endLineNumber);
        if (indent < 1000)
        {
            while (_indentOccurrences.Count <= indent)
            {
                _indentOccurrences.Add(0);
            }

            _indentOccurrences[indent]++;
        }
    }

    public FoldingRegions ToIndentRanges(TextModel model)
    {
        var length = _startIndexes.Count;
        var limit = _foldingRangesLimit.Limit;
        if (length <= limit)
        {
            _foldingRangesLimit.Update(length, null);

            // reverse and create arrays of the exact length
            var startIndexes = new uint[length];
            var endIndexes = new uint[length];
            for (int i = length - 1, k = 0; i >= 0; i--, k++)
            {
                startIndexes[k] = (uint)_startIndexes[i];
                endIndexes[k] = (uint)_endIndexes[i];
            }

            return new FoldingRegions(startIndexes, endIndexes);
        }
        else
        {
            _foldingRangesLimit.Update(length, limit);

            var entries = 0;
            var maxIndent = _indentOccurrences.Count;
            for (var i = 0; i < _indentOccurrences.Count; i++)
            {
                var n = _indentOccurrences[i];
                if (n != 0)
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
            var startIndexes = new uint[limit];
            var endIndexes = new uint[limit];
            for (int i = length - 1, k = 0; i >= 0; i--)
            {
                var startIndex = _startIndexes[i];
                var lineContent = model.GetLineContent(startIndex);
                var indent = TextModelUtils.ComputeIndentLevel(lineContent, tabSize);
                if (indent < maxIndent || (indent == maxIndent && entries++ < limit))
                {
                    startIndexes[k] = (uint)startIndex;
                    endIndexes[k] = (uint)_endIndexes[i];
                    k++;
                }
            }

            return new FoldingRegions(startIndexes, endIndexes);
        }
    }
}

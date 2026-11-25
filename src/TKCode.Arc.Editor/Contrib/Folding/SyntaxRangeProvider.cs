// Ported from vscode (src/vs/editor/contrib/folding/browser/syntaxRangeProvider.ts, commit b57d5c9e), MIT license.
// Differences:
//  - providers are IFoldingRangeProvider (the contract's interface; their DidChange event replaces onDidChange);
//  - a provider that throws is reported to UnexpectedErrors (upstream: onUnexpectedExternalError) and counts as no
//    result; the providers run one after another on the calling thread's context (upstream: Promise.all, which also
//    starts them all at once; the order of the collected ranges is the same).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>A folding range from a provider with the provider's rank (upstream's IFoldingRangeData).</summary>
internal sealed class FoldingRangeData(int start, int end, int rank, FoldingRangeKind? kind)
{
    public int Start { get; } = start;
    public int End { get; } = end;
    public int Rank { get; } = rank;
    public FoldingRangeKind? Kind { get; } = kind;
}

/// <summary>The regions the language's folding range providers report (upstream's SyntaxRangeProvider).</summary>
internal sealed class SyntaxRangeProvider : IRangeProvider
{
    /// <summary>The id of this provider.</summary>
    public const string IdSyntaxProvider = "syntax";

    private static readonly FoldingContext s_foldingContext = new();

    private readonly TextModel _editorModel;
    private readonly IReadOnlyList<IFoldingRangeProvider> _providers;
    private readonly Action _handleFoldingRangesChange;
    private readonly IFoldingLimitReporter _foldingRangesLimit;
    private readonly IRangeProvider? _fallbackRangeProvider;

    public SyntaxRangeProvider(
        TextModel editorModel,
        IReadOnlyList<IFoldingRangeProvider> providers,
        Action handleFoldingRangesChange,
        IFoldingLimitReporter foldingRangesLimit,
        IRangeProvider? fallbackRangeProvider)
    {
        _editorModel = editorModel;
        _providers = providers;
        _handleFoldingRangesChange = handleFoldingRangesChange;
        _foldingRangesLimit = foldingRangesLimit;
        _fallbackRangeProvider = fallbackRangeProvider;
        foreach (var provider in providers)
        {
            provider.DidChange += OnProviderChanged;
        }
    }

    public string Id => IdSyntaxProvider;

    private void OnProviderChanged(IFoldingRangeProvider provider) => _handleFoldingRangesChange();

    public async Task<FoldingRegions?> Compute(CancellationToken cancellationToken)
    {
        var ranges = await CollectSyntaxRanges(_providers, _editorModel, cancellationToken).ConfigureAwait(true);
        if (_editorModel.IsDisposed())
        {
            return null;
        }

        if (ranges is not null)
        {
            return SanitizeRanges(ranges, _foldingRangesLimit);
        }

        return _fallbackRangeProvider is not null ? await _fallbackRangeProvider.Compute(cancellationToken).ConfigureAwait(true) : null;
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.DidChange -= OnProviderChanged;
        }

        _fallbackRangeProvider?.Dispose();
    }

    private static async Task<List<FoldingRangeData>?> CollectSyntaxRanges(IReadOnlyList<IFoldingRangeProvider> providers, TextModel model, CancellationToken cancellationToken)
    {
        List<FoldingRangeData>? rangeData = null;
        for (var i = 0; i < providers.Count; i++)
        {
            IReadOnlyList<FoldingRange>? ranges;
            try
            {
                ranges = await providers[i].ProvideFoldingRanges(model, s_foldingContext, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                UnexpectedErrors.Report(e);
                continue;
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                continue;
            }

            if (ranges is not null)
            {
                rangeData ??= new List<FoldingRangeData>();
                var nLines = model.GetLineCount();
                foreach (var r in ranges)
                {
                    if (r.Start > 0 && r.End > r.Start && r.End <= nLines)
                    {
                        rangeData.Add(new FoldingRangeData(r.Start, r.End, i, r.Kind));
                    }
                }
            }
        }

        return rangeData;
    }

    /// <summary>Sorts the ranges, drops the ones that overlap without nesting and limits them by nesting level (upstream's sanitizeRanges).</summary>
    public static FoldingRegions SanitizeRanges(List<FoldingRangeData> rangeData, IFoldingLimitReporter foldingRangesLimit)
    {
        var sorted = rangeData
            .Select((d, i) => (Data: d, Index: i))
            .OrderBy(static d => d.Data.Start)
            .ThenBy(static d => d.Data.Rank)
            .ThenBy(static d => d.Index) // a stable sort, as JavaScript's
            .Select(static d => d.Data)
            .ToList();
        var collector = new SyntaxRangesCollector(foldingRangesLimit);

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
                            top = Pop(previous);
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

        static FoldingRangeData? Pop(List<FoldingRangeData> list)
        {
            if (list.Count == 0)
            {
                return null;
            }

            var last = list[^1];
            list.RemoveAt(list.Count - 1);
            return last;
        }
    }

    // upstream's RangesCollector of syntaxRangeProvider.ts
    private sealed class SyntaxRangesCollector(IFoldingLimitReporter foldingRangesLimit)
    {
        private readonly List<int> _startIndexes = new();
        private readonly List<int> _endIndexes = new();
        private readonly List<int> _nestingLevels = new();
        private readonly List<int> _nestingLevelCounts = new();
        private readonly List<string?> _types = new();

        public void Add(int startLineNumber, int endLineNumber, string? type, int nestingLevel)
        {
            if (startLineNumber > FoldingRegions.MaxLineNumber || endLineNumber > FoldingRegions.MaxLineNumber)
            {
                return;
            }

            _startIndexes.Add(startLineNumber);
            _endIndexes.Add(endLineNumber);
            _nestingLevels.Add(nestingLevel);
            _types.Add(type);
            if (nestingLevel < 30)
            {
                while (_nestingLevelCounts.Count <= nestingLevel)
                {
                    _nestingLevelCounts.Add(0);
                }

                _nestingLevelCounts[nestingLevel]++;
            }
        }

        public FoldingRegions ToIndentRanges()
        {
            var length = _startIndexes.Count;
            var limit = foldingRangesLimit.Limit;
            if (length <= limit)
            {
                foldingRangesLimit.Update(length, null);

                var startIndexes = new uint[length];
                var endIndexes = new uint[length];
                for (var i = 0; i < length; i++)
                {
                    startIndexes[i] = (uint)_startIndexes[i];
                    endIndexes[i] = (uint)_endIndexes[i];
                }

                return new FoldingRegions(startIndexes, endIndexes, [.. _types]);
            }
            else
            {
                foldingRangesLimit.Update(length, limit);

                var entries = 0;
                var maxLevel = _nestingLevelCounts.Count;
                for (var i = 0; i < _nestingLevelCounts.Count; i++)
                {
                    var n = _nestingLevelCounts[i];
                    if (n != 0)
                    {
                        if (n + entries > limit)
                        {
                            maxLevel = i;
                            break;
                        }

                        entries += n;
                    }
                }

                var startIndexes = new uint[limit];
                var endIndexes = new uint[limit];
                var types = new string?[limit];
                for (int i = 0, k = 0; i < length; i++)
                {
                    var level = _nestingLevels[i];
                    if (level < maxLevel || (level == maxLevel && entries++ < limit))
                    {
                        startIndexes[k] = (uint)_startIndexes[i];
                        endIndexes[k] = (uint)_endIndexes[i];
                        types[k] = _types[i];
                        k++;
                    }
                }

                return new FoldingRegions(startIndexes, endIndexes, types);
            }
        }
    }
}

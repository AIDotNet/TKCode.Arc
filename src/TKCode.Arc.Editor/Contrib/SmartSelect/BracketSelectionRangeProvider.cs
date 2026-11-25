// Ported from vscode (src/vs/editor/contrib/smartSelect/browser/bracketSelections.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the model's bracket pairs are read with the language configuration service given to the constructor (upstream's
//    model carries its own);
//  - _maxDuration is a constructor argument instead of a mutable static (the tests set it);
//  - a round that runs out of time yields with Task.Yield instead of setTimeout;
//  - LinkedList<Range> is a Queue<TextRange> (only push and shift are used).

using System.Diagnostics;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;

namespace TKCode.Arc.Editor.Contrib.SmartSelect;

/// <summary>Selection ranges from the brackets around a position: inside, then with the brackets (and the line before).</summary>
internal sealed class BracketSelectionRangeProvider : ISelectionRangeProvider
{
    /// <summary>Upstream's default _maxDuration: how long one round may search, in milliseconds.</summary>
    public const int DefaultMaxDuration = 30;

    private const int MaxRounds = 2;

    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly int _maxDuration;

    public BracketSelectionRangeProvider(LanguageConfigurationService languageConfigurationService, int maxDuration = DefaultMaxDuration)
    {
        _languageConfigurationService = languageConfigurationService;
        _maxDuration = maxDuration;
    }

    public async Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token)
    {
        var result = new List<IReadOnlyList<SelectionRange>>();
        var bracketPairs = model.GetBracketPairs(_languageConfigurationService);

        foreach (var position in positions)
        {
            var bucket = new List<SelectionRange>();
            result.Add(bucket);

            var ranges = new Dictionary<string, Queue<TextRange>>(StringComparer.Ordinal);
            await BracketsRightYield(bracketPairs, position, ranges);
            await BracketsLeftYield(bracketPairs, model, position, ranges, bucket);
        }

        return result;
    }

    // _bracketsRightYield
    private async Task BracketsRightYield(BracketPairsTextModelPart bracketPairs, TextPosition pos, Dictionary<string, Queue<TextRange>> ranges)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var round = 0;
        while (true)
        {
            if (round >= MaxRounds)
            {
                break;
            }
            if (bracketPairs.FindNextBracket(pos) is not { } bracket)
            {
                break;
            }
            if (stopwatch.ElapsedMilliseconds > _maxDuration)
            {
                // upstream continues in a setTimeout with a new round, whose counts start empty
                await Task.Yield();
                round++;
                counts.Clear();
                stopwatch.Restart();
                continue;
            }
            if (bracket.BracketInfo.IsOpeningBracket)
            {
                var key = bracket.BracketInfo.BracketText;
                // wait for closing
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
            else
            {
                var key = ((ClosingBracketKind)bracket.BracketInfo).OpeningBrackets[0].BracketText;
                // process closing
                var val = counts.GetValueOrDefault(key) - 1;
                counts[key] = Math.Max(0, val);
                if (val < 0)
                {
                    if (!ranges.TryGetValue(key, out var list))
                    {
                        list = new Queue<TextRange>();
                        ranges[key] = list;
                    }
                    list.Enqueue(bracket.Range);
                }
            }
            pos = bracket.Range.End;
        }
    }

    // _bracketsLeftYield
    private async Task BracketsLeftYield(BracketPairsTextModelPart bracketPairs, TextModel model, TextPosition pos, Dictionary<string, Queue<TextRange>> ranges, List<SelectionRange> bucket)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var round = 0;
        while (true)
        {
            if (round >= MaxRounds && ranges.Count == 0)
            {
                break;
            }
            if (bracketPairs.FindPrevBracket(pos) is not { } bracket)
            {
                break;
            }
            if (stopwatch.ElapsedMilliseconds > _maxDuration)
            {
                // upstream continues in a setTimeout with a new round, whose counts start empty
                await Task.Yield();
                round++;
                counts.Clear();
                stopwatch.Restart();
                continue;
            }
            if (!bracket.BracketInfo.IsOpeningBracket)
            {
                var key = ((ClosingBracketKind)bracket.BracketInfo).OpeningBrackets[0].BracketText;
                // wait for opening
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
            else
            {
                var key = bracket.BracketInfo.BracketText;
                // opening
                var val = counts.GetValueOrDefault(key) - 1;
                counts[key] = Math.Max(0, val);
                if (val < 0 && ranges.TryGetValue(key, out var list))
                {
                    var closing = list.Dequeue();
                    if (list.Count == 0)
                    {
                        ranges.Remove(key);
                    }
                    var innerBracket = new TextRange(bracket.Range.End, closing.Start);
                    var outerBracket = new TextRange(bracket.Range.Start, closing.End);
                    bucket.Add(new SelectionRange(innerBracket));
                    bucket.Add(new SelectionRange(outerBracket));
                    AddBracketLeading(model, outerBracket, bucket);
                }
            }
            pos = bracket.Range.Start;
        }
    }

    // _addBracketLeading
    private static void AddBracketLeading(TextModel model, TextRange bracket, List<SelectionRange> bucket)
    {
        if (bracket.StartLineNumber == bracket.EndLineNumber)
        {
            return;
        }
        // xxxxxxxx {
        //
        // }
        var startLine = bracket.StartLineNumber;
        var column = model.GetLineFirstNonWhitespaceColumn(startLine);
        if (column != 0 && column != bracket.StartColumn)
        {
            bucket.Add(new SelectionRange(new TextRange(new TextPosition(startLine, column), bracket.End)));
            bucket.Add(new SelectionRange(new TextRange(new TextPosition(startLine, 1), bracket.End)));
        }

        // xxxxxxxx
        // {
        //
        // }
        var aboveLine = startLine - 1;
        if (aboveLine > 0)
        {
            var aboveColumn = model.GetLineFirstNonWhitespaceColumn(aboveLine);
            if (aboveColumn == bracket.StartColumn && aboveColumn != model.GetLineLastNonWhitespaceColumn(aboveLine))
            {
                bucket.Add(new SelectionRange(new TextRange(new TextPosition(aboveLine, aboveColumn), bracket.End)));
                bucket.Add(new SelectionRange(new TextRange(new TextPosition(aboveLine, 1), bracket.End)));
            }
        }
    }
}

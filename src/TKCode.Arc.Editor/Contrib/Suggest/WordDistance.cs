// Ported from vscode (src/vs/editor/contrib/suggest/browser/wordDistance.ts and the BracketSelectionRangeProvider of
// src/vs/editor/contrib/smartSelect/browser/bracketSelections.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - WordDistance.Create is synchronous and takes the model, the position and a function that reads the editor's
//    position (upstream: the editor worker service and the editor); the word ranges come from
//    SuggestWords.ComputeWordRanges on the model (upstream: the worker's $computeWordRanges);
//  - the bracket search does not yield to the event loop after 30 ms (upstream's setTimeout rounds); it stops after
//    the same number of rounds' worth of brackets instead (MaxBrackets).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>How far a completion's word is from the cursor, in nested blocks (upstream's WordDistance).</summary>
internal abstract class WordDistance
{
    /// <summary>No locality bonus: every distance is 0.</summary>
    public static readonly WordDistance None = new NoneDistance();

    /// <summary>The distance of <paramref name="suggestion"/>'s word from <paramref name="anchor"/>.</summary>
    public abstract int Distance(TextPosition anchor, CompletionItem suggestion);

    /// <summary>
    /// Computes the word ranges of the blocks around <paramref name="position"/>; <see cref="None"/> when
    /// <paramref name="localityBonus"/> is off or there are no brackets around it.
    /// </summary>
    public static WordDistance Create(
        TextModel model,
        TextPosition position,
        Func<TextPosition?> getEditorPosition,
        LanguageConfigurationService? languageConfigurationService,
        bool localityBonus)
    {
        if (!localityBonus || languageConfigurationService is null)
        {
            return None;
        }

        var ranges = BracketSelectionRangeProvider.ProvideSelectionRanges(model, position, languageConfigurationService);
        if (ranges.Count == 0)
        {
            return None;
        }

        var wordRanges = SuggestWords.ComputeWordRanges(model, ranges[0], SuggestWords.GetWordDefinition(model, languageConfigurationService));

        // remove current word
        var wordUntilPos = SuggestWords.GetWordUntilPosition(model, position, languageConfigurationService);
        wordRanges.Remove(wordUntilPos.Word);

        return new BlockDistance(position, getEditorPosition, ranges, wordRanges);
    }

    private sealed class NoneDistance : WordDistance
    {
        public override int Distance(TextPosition anchor, CompletionItem suggestion) => 0;
    }

    private sealed class BlockDistance(
        TextPosition position,
        Func<TextPosition?> getEditorPosition,
        IReadOnlyList<TextRange> ranges,
        Dictionary<string, List<TextRange>> wordRanges) : WordDistance
    {
        public override int Distance(TextPosition anchor, CompletionItem item)
        {
            if (getEditorPosition() != position)
            {
                return 0;
            }
            if (item.Kind == CompletionItemKind.Keyword)
            {
                return 2 << 20;
            }

            var word = item.Label.Label;
            if (!wordRanges.TryGetValue(word, out var wordLines) || wordLines.Count == 0)
            {
                return 2 << 20;
            }

            var idx = wordLines.BinarySearch(new TextRange(anchor, anchor), Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts));
            var bestWordRange = idx >= 0 ? wordLines[idx] : wordLines[Math.Max(0, ~idx - 1)];
            var blockDistance = ranges.Count;
            foreach (var range in ranges)
            {
                if (!range.ContainsRange(bestWordRange))
                {
                    break;
                }
                blockDistance -= 1;
            }
            return blockDistance;
        }
    }
}

/// <summary>The ranges of the brackets around a position, innermost first (upstream's BracketSelectionRangeProvider).</summary>
internal static class BracketSelectionRangeProvider
{
    // upstream yields after 30 ms and gives up after 2 rounds
    private const int MaxBrackets = 10000;

    public static List<TextRange> ProvideSelectionRanges(TextModel model, TextPosition position, LanguageConfigurationService languageConfigurationService)
    {
        var bucket = new List<TextRange>();
        var brackets = model.GetBracketPairs(languageConfigurationService);
        var ranges = new Dictionary<string, LinkedList<TextRange>>(StringComparer.Ordinal);
        BracketsRight(brackets, position, ranges);
        BracketsLeft(model, brackets, position, ranges, bucket);
        return bucket;
    }

    private static void BracketsRight(Model.BracketPairs.BracketPairsTextModelPart brackets, TextPosition pos, Dictionary<string, LinkedList<TextRange>> ranges)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var steps = 0; steps < MaxBrackets; steps++)
        {
            var bracket = brackets.FindNextBracket(pos);
            if (bracket is null)
            {
                break;
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
                        list = new LinkedList<TextRange>();
                        ranges[key] = list;
                    }
                    list.AddLast(bracket.Range);
                }
            }
            pos = bracket.Range.End;
        }
    }

    private static void BracketsLeft(TextModel model, Model.BracketPairs.BracketPairsTextModelPart brackets, TextPosition pos, Dictionary<string, LinkedList<TextRange>> ranges, List<TextRange> bucket)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var steps = 0; steps < MaxBrackets; steps++)
        {
            var bracket = brackets.FindPrevBracket(pos);
            if (bracket is null)
            {
                break;
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
                if (val < 0 && ranges.TryGetValue(key, out var list) && list.First is { } first)
                {
                    var closing = first.Value;
                    list.RemoveFirst();
                    if (list.Count == 0)
                    {
                        ranges.Remove(key);
                    }
                    var innerBracket = new TextRange(bracket.Range.End, closing.Start);
                    var outerBracket = new TextRange(bracket.Range.Start, closing.End);
                    bucket.Add(innerBracket);
                    bucket.Add(outerBracket);
                    AddBracketLeading(model, outerBracket, bucket);
                }
            }
            pos = bracket.Range.Start;
        }
    }

    private static void AddBracketLeading(TextModel model, TextRange bracket, List<TextRange> bucket)
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
            bucket.Add(new TextRange(new TextPosition(startLine, column), bracket.End));
            bucket.Add(new TextRange(new TextPosition(startLine, 1), bracket.End));
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
                bucket.Add(new TextRange(new TextPosition(aboveLine, aboveColumn), bracket.End));
                bucket.Add(new TextRange(new TextPosition(aboveLine, 1), bracket.End));
            }
        }
    }
}

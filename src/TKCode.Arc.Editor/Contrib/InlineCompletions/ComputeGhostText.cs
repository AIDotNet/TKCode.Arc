// Ported from vscode (commit b57d5c9e), MIT license:
//  - src/vs/editor/contrib/inlineCompletions/browser/model/computeGhostText.ts;
//  - src/vs/editor/contrib/inlineCompletions/browser/model/inlineCompletionIsVisible.ts;
//  - matchesSubString of src/vs/base/common/filters.ts;
//  - getSecondaryEdits of src/vs/editor/contrib/inlineCompletions/browser/model/inlineCompletionsModel.ts.
//
// Differences: computeGhostText's one-entry diff cache (module state shared by every model) is left out, the diff is
// computed each time (both strings are at most 5000 characters); the mode is the enum InlineSuggestMode; the diff is
// the engine's LcsDiff on int elements; getSecondaryEdits computes offsets with the model's GetOffsetAt / GetPositionAt
// (upstream: a TextModelText transformer).

using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.InlineSuggestions;

/// <summary>Upstream's inlineSuggest.mode: how a suggestion may differ from the text to be shown as ghost text.</summary>
internal enum InlineSuggestMode
{
    /// <summary>Only text added after the existing text.</summary>
    Prefix,

    /// <summary>Insertions anywhere in the existing text.</summary>
    Subword,

    /// <summary>Insertions anywhere, but none before the cursor (the default).</summary>
    SubwordSmart,
}

/// <summary>Computes the ghost text of an inline completion.</summary>
internal static class GhostTextComputer
{
    /// <summary>
    /// The ghost text of <paramref name="edit"/>, or null when it cannot be shown as ghost text. The preview suffix
    /// length sets where to split the completion text: if the text is <c>hello</c> and the suffix length is 2, the
    /// non-preview part is <c>hel</c> and the preview part is <c>lo</c>.
    /// </summary>
    public static GhostText? ComputeGhostText(
        TextReplacement edit,
        TextModel model,
        InlineSuggestMode mode,
        TextPosition? cursorPosition = null,
        int previewSuffixLength = 0)
    {
        var e = SingleTextEditHelpers.SingleTextRemoveCommonPrefix(edit, model);

        if (e.Range.EndLineNumber != e.Range.StartLineNumber)
        {
            // This edit might span multiple lines, but the first lines must be a common prefix.
            return null;
        }

        var sourceLine = model.GetLineContent(e.Range.StartLineNumber);
        var sourceIndentationLength = TextHelpers.GetLeadingWhitespace(sourceLine).Length;

        var suggestionTouchesIndentation = e.Range.StartColumn - 1 <= sourceIndentationLength;
        if (suggestionTouchesIndentation)
        {
            // source:      ··········[······abc]
            //                         ^^^^^^^^^ inlineCompletion.range
            //              ^^^^^^^^^^ ^^^^^^ sourceIndentationLength
            //                         ^^^^^^ replacedIndentation.length
            //                               ^^^ rangeThatDoesNotReplaceIndentation
            // inlineCompletion.text: '··foo'
            //                         ^^ suggestionAddedIndentationLength
            var suggestionAddedIndentationLength = TextHelpers.GetLeadingWhitespace(e.Text).Length;

            var replacedIndentation = JsSubstring(sourceLine, e.Range.StartColumn - 1, sourceIndentationLength);

            var startPosition = e.Range.Start;
            var endPosition = e.Range.End;
            var newStartPosition = startPosition.Column + replacedIndentation.Length <= endPosition.Column
                ? startPosition.Delta(0, replacedIndentation.Length)
                : endPosition;
            var rangeThatDoesNotReplaceIndentation = new TextRange(newStartPosition, endPosition);

            var suggestionWithoutIndentationChange = e.Text.StartsWith(replacedIndentation, StringComparison.Ordinal)
                // Adds more indentation without changing existing indentation: We can add ghost text for this
                ? e.Text[replacedIndentation.Length..]
                // Changes or removes existing indentation. Only add ghost text for the non-indentation part.
                : e.Text[suggestionAddedIndentationLength..];

            e = new TextReplacement(rangeThatDoesNotReplaceIndentation, suggestionWithoutIndentationChange);
        }

        // This is a single line string
        var valueToBeReplaced = model.GetValueInRange(e.Range);

        var changes = CachingDiff(valueToBeReplaced, e.Text);
        if (changes is null)
        {
            // No ghost text in case the diff would be too slow to compute
            return null;
        }

        var lineNumber = e.Range.StartLineNumber;
        var parts = new List<GhostTextPart>();

        if (mode == InlineSuggestMode.Prefix)
        {
            var filteredChanges = changes.Where(static c => c.OriginalLength == 0).ToList();
            if (filteredChanges.Count > 1 || (filteredChanges.Count == 1 && filteredChanges[0].OriginalStart != valueToBeReplaced.Length))
            {
                // Prefixes only have a single change.
                return null;
            }
        }

        var previewStartInCompletionText = e.Text.Length - previewSuffixLength;

        foreach (var c in changes)
        {
            var insertColumn = e.Range.StartColumn + c.OriginalStart + c.OriginalLength;

            if (mode == InlineSuggestMode.SubwordSmart && cursorPosition is { } cursor && cursor.LineNumber == e.Range.StartLineNumber && insertColumn < cursor.Column)
            {
                // No ghost text before cursor
                return null;
            }

            if (c.OriginalLength > 0)
            {
                return null;
            }

            if (c.ModifiedLength == 0)
            {
                continue;
            }

            var modifiedEnd = c.ModifiedStart + c.ModifiedLength;
            var nonPreviewTextEnd = Math.Max(c.ModifiedStart, Math.Min(modifiedEnd, previewStartInCompletionText));
            var nonPreviewText = JsSubstring(e.Text, c.ModifiedStart, nonPreviewTextEnd);
            var italicText = JsSubstring(e.Text, nonPreviewTextEnd, Math.Max(c.ModifiedStart, modifiedEnd));

            if (nonPreviewText.Length > 0)
            {
                parts.Add(new GhostTextPart(insertColumn, nonPreviewText, false));
            }

            if (italicText.Length > 0)
            {
                parts.Add(new GhostTextPart(insertColumn, italicText, true));
            }
        }

        return new GhostText(lineNumber, parts);
    }

    // JavaScript's substring: clamps and swaps its arguments
    private static string JsSubstring(string s, int start, int end)
    {
        start = Math.Clamp(start, 0, s.Length);
        end = Math.Clamp(end, 0, s.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return s[start..end];
    }

    private static IReadOnlyList<DiffChange>? CachingDiff(string originalValue, string newValue)
    {
        var changes = SmartDiff(originalValue, newValue, true);
        if (changes is not null)
        {
            var deletedChars = DeletedCharacters(changes);
            if (deletedChars > 0)
            {
                // For performance reasons, don't compute diff if there is nothing to improve
                var newChanges = SmartDiff(originalValue, newValue, false);
                if (newChanges is not null && DeletedCharacters(newChanges) < deletedChars)
                {
                    // Disabling smartness seems to be better here
                    changes = newChanges;
                }
            }
        }

        return changes;
    }

    private static int DeletedCharacters(IReadOnlyList<DiffChange> changes) => changes.Sum(static c => c.OriginalLength);

    /// <summary>
    /// When matching <c>if ()</c> with <c>if (f() = 1) { g(); }</c>, align it like this: <c>if (       )</c>, not like
    /// this: <c>if (  )</c>, also not like this: <c>if (             )</c>. The parentheses are preprocessed to ensure
    /// that they match correctly.
    /// </summary>
    public static IReadOnlyList<DiffChange>? SmartDiff(string originalValue, string newValue, bool smartBracketMatching)
    {
        if (originalValue.Length > 5000 || newValue.Length > 5000)
        {
            // We don't want to work on strings that are too big
            return null;
        }

        static int GetMaxCharCode(string val)
        {
            var maxCharCode = 0;
            foreach (var ch in val)
            {
                maxCharCode = Math.Max(maxCharCode, ch);
            }

            return maxCharCode;
        }

        var maxCharCode = Math.Max(GetMaxCharCode(originalValue), GetMaxCharCode(newValue));
        int GetUniqueCharCode(int id)
        {
            if (id < 0)
            {
                throw new InvalidOperationException("unexpected");
            }

            return maxCharCode + id + 1;
        }

        int[] GetElements(string source)
        {
            var level = 0;
            var group = 0;
            var characters = new int[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                // TODO support more brackets
                if (smartBracketMatching && source[i] == '(')
                {
                    var id = (group * 100) + level;
                    characters[i] = GetUniqueCharCode(2 * id);
                    level++;
                }
                else if (smartBracketMatching && source[i] == ')')
                {
                    level = Math.Max(level - 1, 0);
                    var id = (group * 100) + level;
                    characters[i] = GetUniqueCharCode((2 * id) + 1);
                    if (level == 0)
                    {
                        group++;
                    }
                }
                else
                {
                    characters[i] = source[i];
                }
            }

            return characters;
        }

        var elements1 = GetElements(originalValue);
        var elements2 = GetElements(newValue);

        return new LcsDiff(new IntSequence(elements1), new IntSequence(elements2)).ComputeDiff(false).Changes;
    }

    private sealed class IntSequence(int[] elements) : IDiffSequence
    {
        public int[]? GetIntElements() => elements;
    }

    // ---- inlineCompletionIsVisible.ts

    public static bool InlineCompletionIsVisible(TextReplacement singleTextEdit, TextRange? originalRange, TextModel model, TextPosition cursorPosition)
    {
        var minimizedReplacement = SingleTextEditHelpers.SingleTextRemoveCommonPrefix(singleTextEdit, model);
        var editRange = singleTextEdit.Range;
        if ((originalRange is { } original && original.Start != editRange.Start)
            || cursorPosition.LineNumber != minimizedReplacement.Range.StartLineNumber
            // if the completion is empty after removing the common prefix of the completion and the model, the
            // completion item would not be visible
            || minimizedReplacement.IsEmpty)
        {
            return false;
        }

        // We might consider comparing by .toLowerText, but this requires GhostTextReplacement
        var originalValue = model.GetValueInRange(minimizedReplacement.Range, EndOfLinePreference.LF);
        var filterText = minimizedReplacement.Text;

        var cursorPosIndex = Math.Max(0, cursorPosition.Column - minimizedReplacement.Range.StartColumn);

        var filterTextBefore = JsSubstring(filterText, 0, cursorPosIndex);
        var filterTextAfter = JsSubstring(filterText, cursorPosIndex, filterText.Length);

        var originalValueBefore = JsSubstring(originalValue, 0, cursorPosIndex);
        var originalValueAfter = JsSubstring(originalValue, cursorPosIndex, originalValue.Length);

        var originalValueIndent = model.GetLineIndentColumn(minimizedReplacement.Range.StartLineNumber);
        if (minimizedReplacement.Range.StartColumn <= originalValueIndent)
        {
            // Remove indentation
            originalValueBefore = originalValueBefore.TrimStart();
            if (originalValueBefore.Length == 0)
            {
                originalValueAfter = originalValueAfter.TrimStart();
            }

            filterTextBefore = filterTextBefore.TrimStart();
            if (filterTextBefore.Length == 0)
            {
                filterTextAfter = filterTextAfter.TrimStart();
            }
        }

        return filterTextBefore.StartsWith(originalValueBefore, StringComparison.Ordinal)
            && MatchesSubString(originalValueAfter, filterTextAfter);
    }

    /// <summary>Upstream's matchesSubString (filters.ts): whether the characters of word occur in order in the other.</summary>
    public static bool MatchesSubString(string word, string wordToMatchAgainst)
    {
        if (word.Length > wordToMatchAgainst.Length)
        {
            return false;
        }

        word = word.ToLowerInvariant();
        wordToMatchAgainst = wordToMatchAgainst.ToLowerInvariant();
        int i = 0, j = 0;
        while (true)
        {
            if (i == word.Length)
            {
                return true;
            }

            if (j == wordToMatchAgainst.Length)
            {
                return false;
            }

            // upstream recurses: a matching character is taken (no backtracking), else the other word moves on
            if (word[i] == wordToMatchAgainst[j])
            {
                i++;
            }

            j++;
        }
    }

    // ---- getSecondaryEdits (inlineCompletionsModel.ts)

    /// <summary>The edits at the other cursors that the primary edit implies (null where the context differs).</summary>
    public static IReadOnlyList<TextReplacement> GetSecondaryEdits(TextModel textModel, IReadOnlyList<TextPosition> positions, TextReplacement primaryTextRepl)
    {
        if (positions.Count == 1)
        {
            // No secondary cursor positions
            return [];
        }

        var primaryOffset = textModel.GetOffsetAt(positions[0]);
        var secondaryOffsets = positions.Skip(1).Select(textModel.GetOffsetAt).ToList();

        primaryTextRepl = primaryTextRepl.RemoveCommonPrefixAndSuffix(textModel);
        var replaceStart = textModel.GetOffsetAt(primaryTextRepl.Range.Start);
        var replaceEnd = textModel.GetOffsetAt(primaryTextRepl.Range.End);
        var replaceLength = replaceEnd - replaceStart;

        var deltaFromOffsetToRangeStart = replaceStart - primaryOffset;
        var (primaryContextStart, primaryContextEnd) = (Math.Min(replaceStart, primaryOffset), Math.Max(replaceEnd, primaryOffset));
        var primaryContextValue = GetValueOfOffsetRange(textModel, primaryContextStart, primaryContextEnd);

        var replacements = new List<TextReplacement>();
        foreach (var secondaryOffset in secondaryOffsets)
        {
            var newRangeStart = secondaryOffset + deltaFromOffsetToRangeStart;
            var newRangeEnd = newRangeStart + replaceLength;

            var (contextStart, contextEnd) = (Math.Min(newRangeStart, secondaryOffset), Math.Max(newRangeEnd, secondaryOffset));
            var contextValue = GetValueOfOffsetRange(textModel, contextStart, contextEnd);
            if (contextValue != primaryContextValue)
            {
                continue;
            }

            replacements.Add(new TextReplacement(
                new TextRange(textModel.GetPositionAt(newRangeStart), textModel.GetPositionAt(newRangeEnd)),
                primaryTextRepl.Text));
        }

        return replacements;
    }

    private static string? GetValueOfOffsetRange(TextModel model, int start, int end)
    {
        var length = model.GetValueLength();
        if (start < 0 || end > length)
        {
            return null;
        }

        return model.GetValueInRange(new TextRange(model.GetPositionAt(start), model.GetPositionAt(end)));
    }
}

// Ported from vscode (src/vs/editor/common/model/textModelSearch.ts: isValidMatch, leftIsWordBounday and
// rightIsWordBounday, commit b57d5c9e), MIT license. Upstream's misspelled "Bounday" is kept.

namespace TKCode.Arc.Editor.Model;

/// <summary>Whether a simple (non-regex) match falls on word boundaries, the way Monaco decides it.</summary>
internal static class SearchMatchValidation
{
    internal static bool IsValidMatch(
        WordCharacterClassifier wordSeparators,
        ReadOnlySpan<char> text,
        int textLength,
        int matchStartIndex,
        int matchLength) =>
        LeftIsWordBounday(wordSeparators, text, textLength, matchStartIndex, matchLength)
        && RightIsWordBounday(wordSeparators, text, textLength, matchStartIndex, matchLength);

    private static bool LeftIsWordBounday(
        WordCharacterClassifier wordSeparators,
        ReadOnlySpan<char> text,
        int textLength,
        int matchStartIndex,
        int matchLength)
    {
        _ = textLength;
        if (matchStartIndex == 0)
        {
            // Match starts at start of string
            return true;
        }

        var charBefore = text[matchStartIndex - 1];
        if (wordSeparators.Get(charBefore) != WordCharacterClass.Regular)
        {
            // The character before the match is a word separator
            return true;
        }

        if (charBefore is '\r' or '\n')
        {
            // The character before the match is line break or carriage return.
            return true;
        }

        if (matchLength > 0)
        {
            var firstCharInMatch = text[matchStartIndex];
            if (wordSeparators.Get(firstCharInMatch) != WordCharacterClass.Regular)
            {
                // The first character inside the match is a word separator
                return true;
            }
        }

        return false;
    }

    private static bool RightIsWordBounday(
        WordCharacterClassifier wordSeparators,
        ReadOnlySpan<char> text,
        int textLength,
        int matchStartIndex,
        int matchLength)
    {
        if (matchStartIndex + matchLength == textLength)
        {
            // Match ends at end of string
            return true;
        }

        var charAfter = text[matchStartIndex + matchLength];
        if (wordSeparators.Get(charAfter) != WordCharacterClass.Regular)
        {
            // The character after the match is a word separator
            return true;
        }

        if (charAfter is '\r' or '\n')
        {
            // The character after the match is line break or carriage return.
            return true;
        }

        if (matchLength > 0)
        {
            var lastCharInMatch = text[matchStartIndex + matchLength - 1];
            if (wordSeparators.Get(lastCharInMatch) != WordCharacterClass.Regular)
            {
                // The last character in the match is a word separator
                return true;
            }
        }

        return false;
    }
}

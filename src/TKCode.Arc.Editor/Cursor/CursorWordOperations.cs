// Ported from vscode (src/vs/editor/common/cursor/cursorWordOperations.ts, commit b57d5c9e), MIT license.
// The class is split over CursorWordOperations.cs (finding words, moving), CursorWordOperations.Delete.cs (deleting,
// word selection) and WordPartOperations.cs.
//
// Differences from upstream:
//  - No Intl.Segmenter: WordCharacterClassifier has no word segmenter (.NET has none to stand in for it), so the
//    lookups of findPrevIntlWordBeforeOrAtOffset / findNextIntlWordAtOrAfterOffset are left out (they find nothing
//    without segmenter locales) and the wordSegmenterLocales / intlSegmenterLocales arguments are not used. Words in
//    scripts without spaces (Japanese, Chinese, ...) are therefore one word, as upstream with no locales.
//  - IFindWordResult is the record struct FindWordResult, the const enum WordType a nested enum; DeleteWordContext is a
//    record class.
//  - WordPartOperations is a static class of its own that calls WordOperations (upstream it extends WordOperations to
//    inherit the static members); upstream's _moveWordPartLeft, _moveWordPartRight, _deleteWordPartLeft and
//    _deleteWordPartRight are WordOperations.MoveWordPartLeft, MoveWordPartRight, DeleteWordPartLeft and
//    DeleteWordPartRight (WordPartOperations has the public members of the same names), the other private _members
//    drop the underscore.
//  - WordPartOperations' candidate arrays filter out null and return null when fewer than three candidates remain,
//    where upstream reads undefined from the sorted array.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>How a word move or delete decides where words start and end.</summary>
internal enum WordNavigationType
{
    WordStart = 0,
    WordStartFast = 1,
    WordEnd = 2,

    /// <summary>Respect chrome definition of a word.</summary>
    WordAccessibility = 3,
}

/// <summary>What a word delete works on.</summary>
internal sealed record DeleteWordContext(WordCharacterClassifier WordSeparators, TextModel Model, TextSelection Selection, bool WhitespaceHeuristics, EditorAutoClosingEditStrategy AutoClosingDelete, EditorAutoClosingStrategy AutoClosingBrackets, EditorAutoClosingStrategy AutoClosingQuotes, AutoClosingPairs AutoClosingPairs, IReadOnlyList<TextRange> AutoClosedCharacters);

/// <summary>Word moves, word deletes and word selection.</summary>
internal static partial class WordOperations
{
    private enum WordType
    {
        None = 0,
        Regular = 1,
        Separator = 2,
    }

    /// <param name="Start">The index where the word starts.</param>
    /// <param name="End">The index where the word ends.</param>
    /// <param name="WordType">The word type.</param>
    /// <param name="NextCharClass">The reason the word ended.</param>
    private readonly record struct FindWordResult(int Start, int End, WordType WordType, WordCharacterClass NextCharClass);

    private static FindWordResult CreateWord(string lineContent, WordType wordType, WordCharacterClass nextCharClass, int start, int end) =>
        new(start, end, wordType, nextCharClass);

    private static FindWordResult? FindPreviousWordOnLine(WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        return DoFindPreviousWordOnLine(lineContent, wordSeparators, position);
    }

    private static FindWordResult? DoFindPreviousWordOnLine(string lineContent, WordCharacterClassifier wordSeparators, TextPosition position)
    {
        var wordType = WordType.None;

        for (var chIndex = position.Column - 2; chIndex >= 0; chIndex--)
        {
            var chCode = lineContent[chIndex];
            var chClass = wordSeparators.Get(chCode);

            if (chClass == WordCharacterClass.Regular)
            {
                if (wordType == WordType.Separator)
                {
                    return CreateWord(lineContent, wordType, chClass, chIndex + 1, FindEndOfWord(lineContent, wordSeparators, wordType, chIndex + 1));
                }
                wordType = WordType.Regular;
            }
            else if (chClass == WordCharacterClass.WordSeparator)
            {
                if (wordType == WordType.Regular)
                {
                    return CreateWord(lineContent, wordType, chClass, chIndex + 1, FindEndOfWord(lineContent, wordSeparators, wordType, chIndex + 1));
                }
                wordType = WordType.Separator;
            }
            else if (chClass == WordCharacterClass.Whitespace)
            {
                if (wordType != WordType.None)
                {
                    return CreateWord(lineContent, wordType, chClass, chIndex + 1, FindEndOfWord(lineContent, wordSeparators, wordType, chIndex + 1));
                }
            }
        }

        if (wordType != WordType.None)
        {
            return CreateWord(lineContent, wordType, WordCharacterClass.Whitespace, 0, FindEndOfWord(lineContent, wordSeparators, wordType, 0));
        }

        return null;
    }

    private static int FindEndOfWord(string lineContent, WordCharacterClassifier wordSeparators, WordType wordType, int startIndex)
    {
        var len = lineContent.Length;
        for (var chIndex = startIndex; chIndex < len; chIndex++)
        {
            var chCode = lineContent[chIndex];
            var chClass = wordSeparators.Get(chCode);

            if (chClass == WordCharacterClass.Whitespace)
            {
                return chIndex;
            }
            if (wordType == WordType.Regular && chClass == WordCharacterClass.WordSeparator)
            {
                return chIndex;
            }
            if (wordType == WordType.Separator && chClass == WordCharacterClass.Regular)
            {
                return chIndex;
            }
        }
        return len;
    }

    private static FindWordResult? FindNextWordOnLine(WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        return DoFindNextWordOnLine(lineContent, wordSeparators, position);
    }

    private static FindWordResult? DoFindNextWordOnLine(string lineContent, WordCharacterClassifier wordSeparators, TextPosition position)
    {
        var wordType = WordType.None;
        var len = lineContent.Length;

        for (var chIndex = position.Column - 1; chIndex < len; chIndex++)
        {
            var chCode = lineContent[chIndex];
            var chClass = wordSeparators.Get(chCode);

            if (chClass == WordCharacterClass.Regular)
            {
                if (wordType == WordType.Separator)
                {
                    return CreateWord(lineContent, wordType, chClass, FindStartOfWord(lineContent, wordSeparators, wordType, chIndex - 1), chIndex);
                }
                wordType = WordType.Regular;
            }
            else if (chClass == WordCharacterClass.WordSeparator)
            {
                if (wordType == WordType.Regular)
                {
                    return CreateWord(lineContent, wordType, chClass, FindStartOfWord(lineContent, wordSeparators, wordType, chIndex - 1), chIndex);
                }
                wordType = WordType.Separator;
            }
            else if (chClass == WordCharacterClass.Whitespace)
            {
                if (wordType != WordType.None)
                {
                    return CreateWord(lineContent, wordType, chClass, FindStartOfWord(lineContent, wordSeparators, wordType, chIndex - 1), chIndex);
                }
            }
        }

        if (wordType != WordType.None)
        {
            return CreateWord(lineContent, wordType, WordCharacterClass.Whitespace, FindStartOfWord(lineContent, wordSeparators, wordType, len - 1), len);
        }

        return null;
    }

    private static int FindStartOfWord(string lineContent, WordCharacterClassifier wordSeparators, WordType wordType, int startIndex)
    {
        for (var chIndex = startIndex; chIndex >= 0; chIndex--)
        {
            var chCode = lineContent[chIndex];
            var chClass = wordSeparators.Get(chCode);

            if (chClass == WordCharacterClass.Whitespace)
            {
                return chIndex + 1;
            }
            if (wordType == WordType.Regular && chClass == WordCharacterClass.WordSeparator)
            {
                return chIndex + 1;
            }
            if (wordType == WordType.Separator && chClass == WordCharacterClass.Regular)
            {
                return chIndex + 1;
            }
        }
        return 0;
    }

    public static TextPosition MoveWordLeft(
        WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position, WordNavigationType wordNavigationType, bool hasMulticursor)
    {
        var lineNumber = position.LineNumber;
        var column = position.Column;

        if (column == 1)
        {
            if (lineNumber > 1)
            {
                lineNumber = lineNumber - 1;
                column = model.GetLineMaxColumn(lineNumber);
            }
        }

        var prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, new TextPosition(lineNumber, column));

        if (wordNavigationType == WordNavigationType.WordStart)
        {
            return new TextPosition(lineNumber, prevWordOnLine is { } w ? w.Start + 1 : 1);
        }

        if (wordNavigationType == WordNavigationType.WordStartFast)
        {
            if (
                !hasMulticursor // avoid having multiple cursors stop at different locations when doing word start
                && prevWordOnLine is { } prev
                && prev.WordType == WordType.Separator
                && prev.End - prev.Start == 1
                && prev.NextCharClass == WordCharacterClass.Regular)
            {
                // Skip over a word made up of one single separator and followed by a regular character
                prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, new TextPosition(lineNumber, prev.Start + 1));
            }

            return new TextPosition(lineNumber, prevWordOnLine is { } w ? w.Start + 1 : 1);
        }

        if (wordNavigationType == WordNavigationType.WordAccessibility)
        {
            while (prevWordOnLine is { } prev && prev.WordType == WordType.Separator)
            {
                // Skip over words made up of only separators
                prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, new TextPosition(lineNumber, prev.Start + 1));
            }

            return new TextPosition(lineNumber, prevWordOnLine is { } w ? w.Start + 1 : 1);
        }

        // We are stopping at the ending of words

        if (prevWordOnLine is { } p && column <= p.End + 1)
        {
            prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, new TextPosition(lineNumber, p.Start + 1));
        }

        return new TextPosition(lineNumber, prevWordOnLine is { } word ? word.End + 1 : 1);
    }

    /// <summary>Upstream's <c>_moveWordPartLeft</c>: the previous camelCase, snake_case or kebab-case boundary.</summary>
    public static TextPosition MoveWordPartLeft(ICursorSimpleModel model, TextPosition position)
    {
        var lineNumber = position.LineNumber;
        var maxColumn = model.GetLineMaxColumn(lineNumber);

        if (position.Column == 1)
        {
            return lineNumber > 1 ? new TextPosition(lineNumber - 1, model.GetLineMaxColumn(lineNumber - 1)) : position;
        }

        var lineContent = model.GetLineContent(lineNumber);
        for (var column = position.Column - 1; column > 1; column--)
        {
            int left = lineContent[column - 2];
            int right = lineContent[column - 1];

            if (left == '_' && right != '_')
            {
                // snake_case_variables
                return new TextPosition(lineNumber, column);
            }

            if (left == '-' && right != '-')
            {
                // kebab-case-variables
                return new TextPosition(lineNumber, column);
            }

            if ((Strings.IsLowerAsciiLetter(left) || Strings.IsAsciiDigit(left)) && Strings.IsUpperAsciiLetter(right))
            {
                // camelCaseVariables
                return new TextPosition(lineNumber, column);
            }

            if (Strings.IsUpperAsciiLetter(left) && Strings.IsUpperAsciiLetter(right))
            {
                // thisIsACamelCaseWithOneLetterWords
                if (column + 1 < maxColumn)
                {
                    int rightRight = lineContent[column];
                    if (Strings.IsLowerAsciiLetter(rightRight) || Strings.IsAsciiDigit(rightRight))
                    {
                        return new TextPosition(lineNumber, column);
                    }
                }
            }
        }

        return new TextPosition(lineNumber, 1);
    }

    public static TextPosition MoveWordRight(
        WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position, WordNavigationType wordNavigationType)
    {
        var lineNumber = position.LineNumber;
        var column = position.Column;

        var movedDown = false;
        if (column == model.GetLineMaxColumn(lineNumber))
        {
            if (lineNumber < model.GetLineCount())
            {
                movedDown = true;
                lineNumber = lineNumber + 1;
                column = 1;
            }
        }

        var nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, column));

        if (wordNavigationType == WordNavigationType.WordEnd)
        {
            if (nextWordOnLine is { } next && next.WordType == WordType.Separator)
            {
                if (next.End - next.Start == 1 && next.NextCharClass == WordCharacterClass.Regular)
                {
                    // Skip over a word made up of one single separator and followed by a regular character
                    nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, next.End + 1));
                }
            }
            if (nextWordOnLine is { } w)
            {
                column = w.End + 1;
            }
            else
            {
                column = model.GetLineMaxColumn(lineNumber);
            }
        }
        else if (wordNavigationType == WordNavigationType.WordAccessibility)
        {
            if (movedDown)
            {
                // If we move to the next line, pretend that the cursor is right before the first character.
                // This is needed when the first word starts right at the first character - and in order not to miss it,
                // we need to start before.
                column = 0;
            }

            while (nextWordOnLine is { } next
                && (next.WordType == WordType.Separator
                    || next.Start + 1 <= column))
            {
                // Skip over a word made up of one single separator
                // Also skip over word if it begins before current cursor position to ascertain we're moving forward at least 1 character.
                nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, next.End + 1));
            }

            if (nextWordOnLine is { } w)
            {
                column = w.Start + 1;
            }
            else
            {
                column = model.GetLineMaxColumn(lineNumber);
            }
        }
        else
        {
            if (nextWordOnLine is { } next && !movedDown && column >= next.Start + 1)
            {
                nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, next.End + 1));
            }
            if (nextWordOnLine is { } w)
            {
                column = w.Start + 1;
            }
            else
            {
                column = model.GetLineMaxColumn(lineNumber);
            }
        }

        return new TextPosition(lineNumber, column);
    }

    /// <summary>Upstream's <c>_moveWordPartRight</c>: the next camelCase, snake_case or kebab-case boundary.</summary>
    public static TextPosition MoveWordPartRight(ICursorSimpleModel model, TextPosition position)
    {
        var lineNumber = position.LineNumber;
        var maxColumn = model.GetLineMaxColumn(lineNumber);

        if (position.Column == maxColumn)
        {
            return lineNumber < model.GetLineCount() ? new TextPosition(lineNumber + 1, 1) : position;
        }

        var lineContent = model.GetLineContent(lineNumber);
        for (var column = position.Column + 1; column < maxColumn; column++)
        {
            int left = lineContent[column - 2];
            int right = lineContent[column - 1];

            if (left != '_' && right == '_')
            {
                // snake_case_variables
                return new TextPosition(lineNumber, column);
            }

            if (left != '-' && right == '-')
            {
                // kebab-case-variables
                return new TextPosition(lineNumber, column);
            }

            if ((Strings.IsLowerAsciiLetter(left) || Strings.IsAsciiDigit(left)) && Strings.IsUpperAsciiLetter(right))
            {
                // camelCaseVariables
                return new TextPosition(lineNumber, column);
            }

            if (Strings.IsUpperAsciiLetter(left) && Strings.IsUpperAsciiLetter(right))
            {
                // thisIsACamelCaseWithOneLetterWords
                if (column + 1 < maxColumn)
                {
                    int rightRight = lineContent[column];
                    if (Strings.IsLowerAsciiLetter(rightRight) || Strings.IsAsciiDigit(rightRight))
                    {
                        return new TextPosition(lineNumber, column);
                    }
                }
            }
        }

        return new TextPosition(lineNumber, maxColumn);
    }
}

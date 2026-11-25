// Ported from vscode (src/vs/editor/common/cursor/cursorWordOperations.ts, commit b57d5c9e), MIT license: the word
// deletes, the word at a position and word selection. See CursorWordOperations.cs for the differences from upstream.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

internal static partial class WordOperations
{
    private static TextRange? DeleteWordLeftWhitespace(ICursorSimpleModel model, TextPosition position)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        var startIndex = position.Column - 2;
        var lastNonWhitespace = Strings.LastNonWhitespaceIndex(lineContent, startIndex);
        if (lastNonWhitespace + 1 < startIndex)
        {
            return new TextRange(position.LineNumber, lastNonWhitespace + 2, position.LineNumber, position.Column);
        }
        return null;
    }

    public static TextRange? DeleteWordLeft(DeleteWordContext ctx, WordNavigationType wordNavigationType)
    {
        var wordSeparators = ctx.WordSeparators;
        var model = ctx.Model;
        var selection = ctx.Selection;
        var whitespaceHeuristics = ctx.WhitespaceHeuristics;

        if (!selection.IsEmpty)
        {
            return selection;
        }

        if (DeleteOperations.IsAutoClosingPairDelete(ctx.AutoClosingDelete, ctx.AutoClosingBrackets, ctx.AutoClosingQuotes, ctx.AutoClosingPairs.AutoClosingPairsOpenByEnd, ctx.Model, [ctx.Selection], ctx.AutoClosedCharacters))
        {
            var autoClosedPosition = ctx.Selection.Position;
            return new TextRange(autoClosedPosition.LineNumber, autoClosedPosition.Column - 1, autoClosedPosition.LineNumber, autoClosedPosition.Column + 1);
        }

        var position = new TextPosition(selection.PositionLineNumber, selection.PositionColumn);

        var lineNumber = position.LineNumber;
        var column = position.Column;

        if (lineNumber == 1 && column == 1)
        {
            // Ignore deleting at beginning of file
            return null;
        }

        if (whitespaceHeuristics)
        {
            var r = DeleteWordLeftWhitespace(model, position);
            if (r is not null)
            {
                return r;
            }
        }

        var prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, position);

        if (wordNavigationType == WordNavigationType.WordStart)
        {
            if (prevWordOnLine is { } prev)
            {
                column = prev.Start + 1;
            }
            else
            {
                if (column > 1)
                {
                    column = 1;
                }
                else
                {
                    lineNumber--;
                    column = model.GetLineMaxColumn(lineNumber);
                }
            }
        }
        else
        {
            if (prevWordOnLine is { } prev && column <= prev.End + 1)
            {
                prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, new TextPosition(lineNumber, prev.Start + 1));
            }
            if (prevWordOnLine is { } word)
            {
                column = word.End + 1;
            }
            else
            {
                if (column > 1)
                {
                    column = 1;
                }
                else
                {
                    lineNumber--;
                    column = model.GetLineMaxColumn(lineNumber);
                }
            }
        }

        return new TextRange(lineNumber, column, position.LineNumber, position.Column);
    }

    public static TextRange DeleteInsideWord(WordCharacterClassifier wordSeparators, TextModel model, TextSelection selection, bool onlyWord = false)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var position = new TextPosition(selection.PositionLineNumber, selection.PositionColumn);

        var r = DeleteInsideWordWhitespace(model, position);
        if (r is { } range)
        {
            return range;
        }

        return DeleteInsideWordDetermineDeleteRange(wordSeparators, model, position, onlyWord);
    }

    private static bool CharAtIsWhitespace(string str, int index)
    {
        var charCode = str[index];
        return charCode is ' ' or '\t';
    }

    private static TextRange? DeleteInsideWordWhitespace(ICursorSimpleModel model, TextPosition position)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        var lineContentLength = lineContent.Length;

        if (lineContentLength == 0)
        {
            // empty line
            return null;
        }

        var leftIndex = Math.Max(position.Column - 2, 0);
        if (!CharAtIsWhitespace(lineContent, leftIndex))
        {
            // touches a non-whitespace character to the left
            return null;
        }

        var rightIndex = Math.Min(position.Column - 1, lineContentLength - 1);
        if (!CharAtIsWhitespace(lineContent, rightIndex))
        {
            // touches a non-whitespace character to the right
            return null;
        }

        // walk over whitespace to the left
        while (leftIndex > 0 && CharAtIsWhitespace(lineContent, leftIndex - 1))
        {
            leftIndex--;
        }

        // walk over whitespace to the right
        while (rightIndex + 1 < lineContentLength && CharAtIsWhitespace(lineContent, rightIndex + 1))
        {
            rightIndex++;
        }

        return new TextRange(position.LineNumber, leftIndex + 1, position.LineNumber, rightIndex + 2);
    }

    private static TextRange DeleteInsideWordDetermineDeleteRange(
        WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position, bool onlyWord)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        var lineLength = lineContent.Length;
        if (lineLength == 0)
        {
            // empty line
            if (position.LineNumber > 1)
            {
                return new TextRange(position.LineNumber - 1, model.GetLineMaxColumn(position.LineNumber - 1), position.LineNumber, 1);
            }
            else
            {
                if (position.LineNumber < model.GetLineCount())
                {
                    return new TextRange(position.LineNumber, 1, position.LineNumber + 1, 1);
                }
                else
                {
                    // empty model
                    return new TextRange(position.LineNumber, 1, position.LineNumber, 1);
                }
            }
        }

        bool TouchesWord(FindWordResult word) => word.Start + 1 <= position.Column && position.Column <= word.End + 1;

        TextRange CreateRangeWithPosition(int startColumn, int endColumn)
        {
            startColumn = Math.Min(startColumn, position.Column);
            endColumn = Math.Max(endColumn, position.Column);
            return new TextRange(position.LineNumber, startColumn, position.LineNumber, endColumn);
        }

        TextRange DeleteWordAndAdjacentWhitespace(FindWordResult word)
        {
            var startColumn = word.Start + 1;
            var endColumn = word.End + 1;
            if (onlyWord)
            {
                return CreateRangeWithPosition(startColumn, endColumn);
            }
            var expandedToTheRight = false;
            while (endColumn - 1 < lineLength && CharAtIsWhitespace(lineContent, endColumn - 1))
            {
                expandedToTheRight = true;
                endColumn++;
            }
            if (!expandedToTheRight)
            {
                while (startColumn > 1 && CharAtIsWhitespace(lineContent, startColumn - 2))
                {
                    startColumn--;
                }
            }
            return CreateRangeWithPosition(startColumn, endColumn);
        }

        var prevWordOnLine = FindPreviousWordOnLine(wordSeparators, model, position);
        if (prevWordOnLine is { } prevWord && TouchesWord(prevWord))
        {
            return DeleteWordAndAdjacentWhitespace(prevWord);
        }
        var nextWordOnLine = FindNextWordOnLine(wordSeparators, model, position);
        if (nextWordOnLine is { } nextWord && TouchesWord(nextWord))
        {
            return DeleteWordAndAdjacentWhitespace(nextWord);
        }
        if (prevWordOnLine is { } prev && nextWordOnLine is { } next)
        {
            return CreateRangeWithPosition(prev.End + 1, next.Start + 1);
        }
        if (prevWordOnLine is { } prevOnly)
        {
            return CreateRangeWithPosition(prevOnly.Start + 1, prevOnly.End + 1);
        }
        if (nextWordOnLine is { } nextOnly)
        {
            return CreateRangeWithPosition(nextOnly.Start + 1, nextOnly.End + 1);
        }

        return CreateRangeWithPosition(1, lineLength + 1);
    }

    /// <summary>Upstream's <c>_deleteWordPartLeft</c>: the range to the previous word part boundary.</summary>
    public static TextRange DeleteWordPartLeft(ICursorSimpleModel model, TextSelection selection)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var pos = selection.Position;
        var toPosition = MoveWordPartLeft(model, pos);
        return new TextRange(pos.LineNumber, pos.Column, toPosition.LineNumber, toPosition.Column);
    }

    private static int FindFirstNonWhitespaceChar(string str, int startIndex)
    {
        var len = str.Length;
        for (var chIndex = startIndex; chIndex < len; chIndex++)
        {
            var ch = str[chIndex];
            if (ch != ' ' && ch != '\t')
            {
                return chIndex;
            }
        }
        return len;
    }

    private static TextRange? DeleteWordRightWhitespace(ICursorSimpleModel model, TextPosition position)
    {
        var lineContent = model.GetLineContent(position.LineNumber);
        var startIndex = position.Column - 1;
        var firstNonWhitespace = FindFirstNonWhitespaceChar(lineContent, startIndex);
        if (startIndex < firstNonWhitespace)
        {
            // bingo
            return new TextRange(position.LineNumber, position.Column, position.LineNumber, firstNonWhitespace + 1);
        }
        return null;
    }

    public static TextRange? DeleteWordRight(DeleteWordContext ctx, WordNavigationType wordNavigationType)
    {
        var wordSeparators = ctx.WordSeparators;
        var model = ctx.Model;
        var selection = ctx.Selection;
        var whitespaceHeuristics = ctx.WhitespaceHeuristics;

        if (!selection.IsEmpty)
        {
            return selection;
        }

        var position = new TextPosition(selection.PositionLineNumber, selection.PositionColumn);

        var lineNumber = position.LineNumber;
        var column = position.Column;

        var lineCount = model.GetLineCount();
        var maxColumn = model.GetLineMaxColumn(lineNumber);
        if (lineNumber == lineCount && column == maxColumn)
        {
            // Ignore deleting at end of file
            return null;
        }

        if (whitespaceHeuristics)
        {
            var r = DeleteWordRightWhitespace(model, position);
            if (r is not null)
            {
                return r;
            }
        }

        var nextWordOnLine = FindNextWordOnLine(wordSeparators, model, position);

        if (wordNavigationType == WordNavigationType.WordEnd)
        {
            if (nextWordOnLine is { } next)
            {
                column = next.End + 1;
            }
            else
            {
                if (column < maxColumn || lineNumber == lineCount)
                {
                    column = maxColumn;
                }
                else
                {
                    lineNumber++;
                    nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, 1));
                    if (nextWordOnLine is { } nextOnNextLine)
                    {
                        column = nextOnNextLine.Start + 1;
                    }
                    else
                    {
                        column = model.GetLineMaxColumn(lineNumber);
                    }
                }
            }
        }
        else
        {
            if (nextWordOnLine is { } next && column >= next.Start + 1)
            {
                nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, next.End + 1));
            }
            if (nextWordOnLine is { } word)
            {
                column = word.Start + 1;
            }
            else
            {
                if (column < maxColumn || lineNumber == lineCount)
                {
                    column = maxColumn;
                }
                else
                {
                    lineNumber++;
                    nextWordOnLine = FindNextWordOnLine(wordSeparators, model, new TextPosition(lineNumber, 1));
                    if (nextWordOnLine is { } nextOnNextLine)
                    {
                        column = nextOnNextLine.Start + 1;
                    }
                    else
                    {
                        column = model.GetLineMaxColumn(lineNumber);
                    }
                }
            }
        }

        return new TextRange(lineNumber, column, position.LineNumber, position.Column);
    }

    /// <summary>Upstream's <c>_deleteWordPartRight</c>: the range to the next word part boundary.</summary>
    public static TextRange DeleteWordPartRight(ICursorSimpleModel model, TextSelection selection)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var pos = selection.Position;
        var toPosition = MoveWordPartRight(model, pos);
        return new TextRange(pos.LineNumber, pos.Column, toPosition.LineNumber, toPosition.Column);
    }

    private static WordAtPosition CreateWordAtPosition(TextModel model, int lineNumber, FindWordResult word)
    {
        var range = new TextRange(lineNumber, word.Start + 1, lineNumber, word.End + 1);
        return new WordAtPosition(model.GetValueInRange(range), range.StartColumn, range.EndColumn);
    }

    /// <summary>
    /// The regular word at or next to <paramref name="position"/>, or null. <paramref name="intlSegmenterLocales"/> is
    /// not used: there is no word segmenter (see the file header).
    /// </summary>
    public static WordAtPosition? GetWordAtPosition(TextModel model, string wordSeparators, IReadOnlyList<string> intlSegmenterLocales, TextPosition position)
    {
        var classifier = WordCharacterClassifier.GetMapForWordSeparators(wordSeparators);
        var prevWord = FindPreviousWordOnLine(classifier, model, position);
        if (prevWord is { } prev && prev.WordType == WordType.Regular && prev.Start <= position.Column - 1 && position.Column - 1 <= prev.End)
        {
            return CreateWordAtPosition(model, position.LineNumber, prev);
        }
        var nextWord = FindNextWordOnLine(classifier, model, position);
        if (nextWord is { } next && next.WordType == WordType.Regular && next.Start <= position.Column - 1 && position.Column - 1 <= next.End)
        {
            return CreateWordAtPosition(model, position.LineNumber, next);
        }
        return null;
    }

    /// <summary>Word selection (double click, and dragging after it).</summary>
    public static SingleCursorState Word(
        CursorConfiguration config, ICursorSimpleModel model, SingleCursorState cursor, bool inSelectionMode, TextPosition position)
    {
        var wordSeparators = WordCharacterClassifier.GetMapForWordSeparators(config.WordSeparators);
        var prevWord = FindPreviousWordOnLine(wordSeparators, model, position);
        var nextWord = FindNextWordOnLine(wordSeparators, model, position);

        int startColumn;
        int endColumn;

        if (!inSelectionMode)
        {
            // Entering word selection for the first time
            if (prevWord is { } p1 && p1.WordType == WordType.Regular && p1.Start <= position.Column - 1 && position.Column - 1 <= p1.End)
            {
                // isTouchingPrevWord (Regular word)
                startColumn = p1.Start + 1;
                endColumn = p1.End + 1;
            }
            else if (prevWord is { } p2 && p2.WordType == WordType.Separator && p2.Start <= position.Column - 1 && position.Column - 1 < p2.End)
            {
                // isTouchingPrevWord (Separator word) - stricter check, don't include end boundary
                startColumn = p2.Start + 1;
                endColumn = p2.End + 1;
            }
            else if (nextWord is { } n1 && n1.WordType == WordType.Regular && n1.Start <= position.Column - 1 && position.Column - 1 <= n1.End)
            {
                // isTouchingNextWord (Regular word)
                startColumn = n1.Start + 1;
                endColumn = n1.End + 1;
            }
            else if (nextWord is { } n2 && n2.WordType == WordType.Separator && n2.Start <= position.Column - 1 && position.Column - 1 < n2.End)
            {
                // isTouchingNextWord (Separator word) - stricter check, don't include end boundary
                startColumn = n2.Start + 1;
                endColumn = n2.End + 1;
            }
            else
            {
                if (prevWord is { } p3)
                {
                    startColumn = p3.End + 1;
                }
                else
                {
                    startColumn = 1;
                }
                if (nextWord is { } n3)
                {
                    endColumn = n3.Start + 1;
                }
                else
                {
                    endColumn = model.GetLineMaxColumn(position.LineNumber);
                }
            }

            return new SingleCursorState(
                new TextRange(position.LineNumber, startColumn, position.LineNumber, endColumn), SelectionStartKind.Word, 0,
                new TextPosition(position.LineNumber, endColumn), 0);
        }

        if (prevWord is { } p4 && p4.WordType == WordType.Regular && p4.Start < position.Column - 1 && position.Column - 1 < p4.End)
        {
            // isInsidePrevWord (Regular word)
            startColumn = p4.Start + 1;
            endColumn = p4.End + 1;
        }
        else if (nextWord is { } n4 && n4.WordType == WordType.Regular && n4.Start < position.Column - 1 && position.Column - 1 < n4.End)
        {
            // isInsideNextWord (Regular word)
            startColumn = n4.Start + 1;
            endColumn = n4.End + 1;
        }
        else
        {
            startColumn = position.Column;
            endColumn = position.Column;
        }

        var lineNumber = position.LineNumber;
        int column;
        if (cursor.SelectionStart.ContainsPosition(position))
        {
            column = cursor.SelectionStart.EndColumn;
        }
        else if (position.IsBeforeOrEqual(cursor.SelectionStart.Start))
        {
            column = startColumn;
            var possiblePosition = new TextPosition(lineNumber, column);
            if (cursor.SelectionStart.ContainsPosition(possiblePosition))
            {
                column = cursor.SelectionStart.EndColumn;
            }
        }
        else
        {
            column = endColumn;
            var possiblePosition = new TextPosition(lineNumber, column);
            if (cursor.SelectionStart.ContainsPosition(possiblePosition))
            {
                column = cursor.SelectionStart.StartColumn;
            }
        }

        return cursor.Move(true, lineNumber, column, 0);
    }
}

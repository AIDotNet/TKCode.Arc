// Ported from vscode (src/vs/editor/common/model/indentationGuesser.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>What <see cref="IndentationGuesser.GuessIndentation"/> found.</summary>
/// <param name="TabSize">When indenting with spaces: how many spaces make an indent.</param>
/// <param name="InsertSpaces">Whether the text indents with spaces.</param>
internal readonly record struct GuessedIndentation(int TabSize, bool InsertSpaces);

internal static class IndentationGuesser
{
    // Prefer even guesses for the tab size; limited to [2, 8].
    private static ReadOnlySpan<int> AllowedTabSizeGuesses => [2, 4, 6, 8, 3, 5, 7];

    private const int MaxAllowedTabSizeGuess = 8;

    /// <summary>Guesses the indentation of a text from its first 10 000 lines.</summary>
    public static GuessedIndentation GuessIndentation(PieceTreeTextBuffer source, int defaultTabSize, bool defaultInsertSpaces)
    {
        var linesCount = Math.Min(source.GetLineCount(), 10000);

        var linesIndentedWithTabsCount = 0; // lines with at least one tab in the indentation
        var linesIndentedWithSpacesCount = 0; // lines with only spaces in the indentation

        var previousLineText = string.Empty; // the latest line that had non-whitespace characters
        var previousLineIndentation = 0; // the index of its first non-whitespace character

        Span<int> spacesDiffCount = stackalloc int[MaxAllowedTabSizeGuess + 1]; // tab size scores
        spacesDiffCount.Clear();

        for (var lineNumber = 1; lineNumber <= linesCount; lineNumber++)
        {
            var currentLineText = source.GetLineContent(lineNumber);
            var currentLineLength = currentLineText.Length;

            var currentLineHasContent = false;
            var currentLineIndentation = 0;
            var currentLineSpacesCount = 0;
            var currentLineTabsCount = 0;
            for (var j = 0; j < currentLineLength; j++)
            {
                var charCode = currentLineText[j];
                if (charCode == '\t')
                {
                    currentLineTabsCount++;
                }
                else if (charCode == ' ')
                {
                    currentLineSpacesCount++;
                }
                else
                {
                    currentLineHasContent = true;
                    currentLineIndentation = j;
                    break;
                }
            }

            // Ignore empty and whitespace-only lines.
            if (!currentLineHasContent)
            {
                continue;
            }

            if (currentLineTabsCount > 0)
            {
                linesIndentedWithTabsCount++;
            }
            else if (currentLineSpacesCount > 1)
            {
                linesIndentedWithSpacesCount++;
            }

            SpacesDiff(
                previousLineText, previousLineIndentation, currentLineText, currentLineIndentation,
                out var currentSpacesDiff, out var looksLikeAlignment);

            if (looksLikeAlignment)
            {
                // When indenting with spaces and the difference equals the tab size, it may still be indentation:
                //
                // - item1
                //   - item2
                //
                // Otherwise skip the line:
                //
                // const a = 1,
                //       b = 2;
                if (!(defaultInsertSpaces && defaultTabSize == currentSpacesDiff))
                {
                    continue;
                }
            }

            if (currentSpacesDiff <= MaxAllowedTabSizeGuess)
            {
                spacesDiffCount[currentSpacesDiff]++;
            }

            previousLineText = currentLineText;
            previousLineIndentation = currentLineIndentation;
        }

        var insertSpaces = defaultInsertSpaces;
        if (linesIndentedWithTabsCount != linesIndentedWithSpacesCount)
        {
            insertSpaces = linesIndentedWithTabsCount < linesIndentedWithSpacesCount;
        }

        var tabSize = defaultTabSize;

        // Guess the tab size only when inserting spaces.
        if (insertSpaces)
        {
            var tabSizeScore = 0;
            foreach (var possibleTabSize in AllowedTabSizeGuesses)
            {
                var possibleTabSizeScore = spacesDiffCount[possibleTabSize];
                if (possibleTabSizeScore > tabSizeScore)
                {
                    tabSizeScore = possibleTabSizeScore;
                    tabSize = possibleTabSize;
                }
            }

            // 2 wins over 4 only with at least 2/3 of the occurrences of 4: YAML-like files have some 4-space
            // differences from deeper nesting, while 4 stays preferred where it clearly dominates.
            if (tabSize == 4 && spacesDiffCount[4] > 0 && spacesDiffCount[2] > 0
                && spacesDiffCount[2] * 3 >= spacesDiffCount[4] * 2)
            {
                tabSize = 2;
            }
        }

        return new GuessedIndentation(tabSize, insertSpaces);
    }

    /// <summary>Computes the difference in spaces between the indentation of two lines.</summary>
    private static void SpacesDiff(string a, int aLength, string b, int bLength, out int spacesDiff, out bool looksLikeAlignment)
    {
        spacesDiff = 0;
        looksLikeAlignment = false;

        // This can go both ways, e.g. a: "\t", b: "\t    " counts 1 tab and 4 spaces.
        int i;
        for (i = 0; i < aLength && i < bLength; i++)
        {
            if (a[i] != b[i])
            {
                break;
            }
        }

        int aSpacesCnt = 0, aTabsCount = 0;
        for (var j = i; j < aLength; j++)
        {
            if (a[j] == ' ')
            {
                aSpacesCnt++;
            }
            else
            {
                aTabsCount++;
            }
        }

        int bSpacesCnt = 0, bTabsCount = 0;
        for (var j = i; j < bLength; j++)
        {
            if (b[j] == ' ')
            {
                bSpacesCnt++;
            }
            else
            {
                bTabsCount++;
            }
        }

        if (aSpacesCnt > 0 && aTabsCount > 0)
        {
            return;
        }

        if (bSpacesCnt > 0 && bTabsCount > 0)
        {
            return;
        }

        var tabsDiff = Math.Abs(aTabsCount - bTabsCount);
        var diff = Math.Abs(aSpacesCnt - bSpacesCnt);

        if (tabsDiff == 0)
        {
            // The difference may come from alignment, which is not a hint for the indentation.
            spacesDiff = diff;

            if (diff > 0 && 0 <= bSpacesCnt - 1 && bSpacesCnt - 1 < a.Length && bSpacesCnt < b.Length)
            {
                if (b[bSpacesCnt] != ' ' && a[bSpacesCnt - 1] == ' ')
                {
                    if (a[^1] == ',')
                    {
                        // const a = b + c,
                        //       d = b - c;
                        looksLikeAlignment = true;
                    }
                }
            }

            return;
        }

        if (diff % tabsDiff == 0)
        {
            spacesDiff = diff / tabsDiff;
        }
    }
}

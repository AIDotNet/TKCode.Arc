// Ported from vscode (src/vs/editor/common/core/cursorColumns.ts and core/misc/indentation.ts, commit b57d5c9e),
// MIT license.
// Differences from upstream: the private _nextVisibleColumn is NextVisibleColumn; a column below 1 is clamped to an
// empty prefix explicitly, as JavaScript's substring does with a negative end.

using System.Text;

namespace TKCode.Arc.Editor;

/// <summary>
/// Tab stop arithmetic on visible columns. A visible column is a rough horizontal position: 0-based, a tab
/// advances to the next multiple of the tab size.
/// </summary>
internal static class CursorColumns
{
    // A column in a position is the gap between two adjacent characters. A visible column is a very rough
    // approximation of the horizontal screen position of a column. For example, using a tab size of 4:
    //
    //   |<TAB>|<TAB>|T|ext
    //   |     |     | \---- column = 4, visible column = 9
    //   |     |     \------ column = 3, visible column = 8
    //   |     \------------ column = 2, visible column = 4
    //   \------------------ column = 1, visible column = 0
    //
    // NOTE: Visual columns do not work well for RTL text or variable-width fonts or characters.
    // NOTE: These methods work and make sense both on the model and on the view model.

    private static int NextVisibleColumn(int codePoint, int visibleColumn, int tabSize)
    {
        if (codePoint == '\t')
        {
            return NextRenderTabStop(visibleColumn, tabSize);
        }
        if (Strings.IsFullWidthCharacter(codePoint) || Strings.IsEmojiImprecise(codePoint))
        {
            return visibleColumn + 2;
        }
        return visibleColumn + 1;
    }

    /// <summary>The visible column (0-based) of the 1-based <paramref name="column"/> in <paramref name="lineContent"/>.</summary>
    public static int VisibleColumnFromColumn(string lineContent, int column, int tabSize)
    {
        var textLen = Math.Max(0, Math.Min(column - 1, lineContent.Length));
        var text = lineContent.Substring(0, textLen);
        var iterator = new GraphemeIterator(text);

        var result = 0;
        while (!iterator.Eol())
        {
            var codePoint = Strings.GetNextCodePoint(text, textLen, iterator.Offset);
            iterator.NextGraphemeLength();

            result = NextVisibleColumn(codePoint, result, tabSize);
        }

        return result;
    }

    /// <summary>The value to display as "Col" in the status bar (1-based; tabs expanded, code points counted once).</summary>
    public static int ToStatusbarColumn(string lineContent, int column, int tabSize)
    {
        var text = lineContent.Substring(0, Math.Max(0, Math.Min(column - 1, lineContent.Length)));
        var iterator = new CodePointIterator(text);

        var result = 0;
        while (!iterator.Eol())
        {
            var codePoint = iterator.NextCodePoint();

            if (codePoint == '\t')
            {
                result = NextRenderTabStop(result, tabSize);
            }
            else
            {
                result = result + 1;
            }
        }

        return result + 1;
    }

    /// <summary>The 1-based column nearest to <paramref name="visibleColumn"/> (0-based) in <paramref name="lineContent"/>.</summary>
    public static int ColumnFromVisibleColumn(string lineContent, int visibleColumn, int tabSize)
    {
        if (visibleColumn <= 0)
        {
            return 1;
        }

        var lineContentLength = lineContent.Length;
        var iterator = new GraphemeIterator(lineContent);

        var beforeVisibleColumn = 0;
        var beforeColumn = 1;
        while (!iterator.Eol())
        {
            var codePoint = Strings.GetNextCodePoint(lineContent, lineContentLength, iterator.Offset);
            iterator.NextGraphemeLength();

            var afterVisibleColumn = NextVisibleColumn(codePoint, beforeVisibleColumn, tabSize);
            var afterColumn = iterator.Offset + 1;

            if (afterVisibleColumn >= visibleColumn)
            {
                var beforeDelta = visibleColumn - beforeVisibleColumn;
                var afterDelta = afterVisibleColumn - visibleColumn;
                if (afterDelta < beforeDelta)
                {
                    return afterColumn;
                }
                else
                {
                    return beforeColumn;
                }
            }

            beforeVisibleColumn = afterVisibleColumn;
            beforeColumn = afterColumn;
        }

        // walked the entire string
        return lineContentLength + 1;
    }

    /// <summary>The next tab stop after <paramref name="visibleColumn"/> (0-based).</summary>
    public static int NextRenderTabStop(int visibleColumn, int tabSize) => visibleColumn + tabSize - (visibleColumn % tabSize);

    /// <summary>The next indent stop after <paramref name="visibleColumn"/> (0-based).</summary>
    public static int NextIndentTabStop(int visibleColumn, int indentSize) => NextRenderTabStop(visibleColumn, indentSize);

    /// <summary>The tab stop before <paramref name="column"/> (0-based).</summary>
    public static int PrevRenderTabStop(int column, int tabSize) => Math.Max(0, column - 1 - ((column - 1) % tabSize));

    /// <summary>The indent stop before <paramref name="column"/> (0-based).</summary>
    public static int PrevIndentTabStop(int column, int indentSize) => PrevRenderTabStop(column, indentSize);
}

internal static class Indentation
{
    /// <summary>Rewrites the leading whitespace of <paramref name="str"/> with tabs or spaces, keeping its width.</summary>
    public static string NormalizeIndentation(string str, int indentSize, bool insertSpaces)
    {
        var firstNonWhitespaceIndex = Strings.FirstNonWhitespaceIndex(str);
        if (firstNonWhitespaceIndex == -1)
        {
            firstNonWhitespaceIndex = str.Length;
        }

        var spacesCnt = 0;
        for (var i = 0; i < firstNonWhitespaceIndex; i++)
        {
            if (str[i] == '\t')
            {
                spacesCnt = CursorColumns.NextIndentTabStop(spacesCnt, indentSize);
            }
            else
            {
                spacesCnt++;
            }
        }

        var result = new StringBuilder(str.Length);
        if (!insertSpaces)
        {
            result.Append('\t', spacesCnt / indentSize);
            spacesCnt %= indentSize;
        }

        result.Append(' ', spacesCnt);
        result.Append(str, firstNonWhitespaceIndex, str.Length - firstNonWhitespaceIndex);
        return result.ToString();
    }
}

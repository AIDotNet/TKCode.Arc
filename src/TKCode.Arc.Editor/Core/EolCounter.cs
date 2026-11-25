// Ported from vscode (src/vs/editor/common/core/misc/eolCounter.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor;

/// <summary>Which line endings a string contains; the values combine as flags.</summary>
[Flags]
internal enum StringEol
{
    Unknown = 0,
    LF = 1,
    CRLF = 2,

    /// <summary>A lone <c>\r</c>, or a mix of <c>\n</c> and <c>\r\n</c>.</summary>
    Invalid = 3
}

/// <summary>What <see cref="EolCounter.CountEOL"/> found in a string.</summary>
internal readonly record struct EolCount(int Count, int FirstLineLength, int LastLineLength, StringEol Eol);

internal static class EolCounter
{
    /// <summary>Counts the line endings of <paramref name="text"/> and measures its first and last line.</summary>
    public static EolCount CountEOL(ReadOnlySpan<char> text)
    {
        var eolCount = 0;
        var firstLineLength = 0;
        var lastLineStart = 0;
        var eol = StringEol.Unknown;
        for (int i = 0, len = text.Length; i < len; i++)
        {
            var chr = text[i];
            if (chr == '\r')
            {
                if (eolCount == 0)
                {
                    firstLineLength = i;
                }
                eolCount++;
                if (i + 1 < len && text[i + 1] == '\n')
                {
                    // \r\n... case
                    eol |= StringEol.CRLF;
                    i++; // skip \n
                }
                else
                {
                    // \r... case
                    eol |= StringEol.Invalid;
                }
                lastLineStart = i + 1;
            }
            else if (chr == '\n')
            {
                // \n... case
                eol |= StringEol.LF;
                if (eolCount == 0)
                {
                    firstLineLength = i;
                }
                eolCount++;
                lastLineStart = i + 1;
            }
        }
        if (eolCount == 0)
        {
            firstLineLength = text.Length;
        }
        return new EolCount(eolCount, firstLineLength, text.Length - lastLineStart, eol);
    }
}

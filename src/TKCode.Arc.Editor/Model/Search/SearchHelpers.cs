// Ported from vscode (src/vs/base/common/strings.ts: getNextCodePoint, escapeRegExpCharacters, and the
// String.prototype.substring/substr semantics used by src/vs/editor/common/model/textModelSearch.ts),
// commit b57d5c9e, MIT license.

using System.Text;

namespace TKCode.Arc.Editor.Model;

/// <summary>Small string helpers the search port needs to behave like the JavaScript original.</summary>
internal static class SearchHelpers
{
    /// <summary>The code point that starts at <paramref name="offset"/>, or the UTF-16 code unit when it is not a pair.</summary>
    internal static int GetNextCodePoint(ReadOnlySpan<char> str, int len, int offset)
    {
        if (offset < 0 || offset >= len)
        {
            return 0;
        }

        var charCode = str[offset];
        if (char.IsHighSurrogate(charCode) && offset + 1 < len && char.IsLowSurrogate(str[offset + 1]))
        {
            return char.ConvertToUtf32(charCode, str[offset + 1]);
        }

        return charCode;
    }

    /// <summary>
    /// <c>String.prototype.substring(start, end)</c> as a view of <paramref name="text"/>: indices are clamped and
    /// swapped when reversed.
    /// </summary>
    internal static ReadOnlySpan<char> JsSubstring(ReadOnlySpan<char> text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return text.Slice(start, end - start);
    }

    /// <summary>
    /// <c>String.prototype.substr(start)</c> as a view of <paramref name="text"/>: an index past the end gives the
    /// empty text.
    /// </summary>
    internal static ReadOnlySpan<char> JsSubstr(ReadOnlySpan<char> text, int start)
    {
        if (start < 0)
        {
            start = Math.Max(text.Length + start, 0);
        }

        return start >= text.Length ? ReadOnlySpan<char>.Empty : text.Slice(start);
    }

    /// <summary>Escapes the characters JavaScript's <c>escapeRegExpCharacters</c> escapes.</summary>
    internal static string EscapeRegExpCharacters(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            if (ch is '\\' or '{' or '}' or '*' or '+' or '?' or '|' or '^' or '$' or '.' or '[' or ']' or '(' or ')')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}

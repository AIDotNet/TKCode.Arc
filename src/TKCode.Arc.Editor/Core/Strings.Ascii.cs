// Ported from vscode (src/vs/base/common/strings.ts, commit b57d5c9e), MIT license: isAsciiDigit, isLowerAsciiLetter,
// isUpperAsciiLetter and lastNonWhitespaceIndex with a start index (the word operations need them).
// Difference from upstream: a start index past the end returns that index, as upstream does (charCodeAt gives NaN
// there, which is neither a space nor a tab).

namespace TKCode.Arc.Editor;

internal static partial class Strings
{
    /// <summary>Whether <paramref name="code"/> is 0 to 9.</summary>
    public static bool IsAsciiDigit(int code) => code >= '0' && code <= '9';

    /// <summary>Whether <paramref name="code"/> is a to z.</summary>
    public static bool IsLowerAsciiLetter(int code) => code >= 'a' && code <= 'z';

    /// <summary>Whether <paramref name="code"/> is A to Z.</summary>
    public static bool IsUpperAsciiLetter(int code) => code >= 'A' && code <= 'Z';

    /// <summary>
    /// The index of the last character at or before <paramref name="startIndex"/> that is not a space or tab, or -1.
    /// </summary>
    public static int LastNonWhitespaceIndex(string str, int startIndex)
    {
        for (var i = startIndex; i >= 0; i--)
        {
            var chCode = CharCodeAt(str, i);
            if (chCode != ' ' && chCode != '\t')
            {
                return i;
            }
        }
        return -1;
    }
}

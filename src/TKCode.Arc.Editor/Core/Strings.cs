// Ported from vscode (src/vs/base/common/strings.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor;

internal static partial class Strings
{
    /// <summary>The index of the first character that is not a space or tab, or -1.</summary>
    public static int FirstNonWhitespaceIndex(ReadOnlySpan<char> str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] != ' ' && str[i] != '\t')
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The index of the last character that is not a space or tab, or -1.</summary>
    public static int LastNonWhitespaceIndex(ReadOnlySpan<char> str)
    {
        for (var i = str.Length - 1; i >= 0; i--)
        {
            if (str[i] != ' ' && str[i] != '\t')
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>One letter for <paramref name="n"/>: a to z, then A to Z, then again from a.</summary>
    public static string SingleLetterHash(int n)
    {
        const int lettersCnt = 'Z' - 'A' + 1;

        n %= 2 * lettersCnt;

        if (n < lettersCnt)
        {
            return ((char)('a' + n)).ToString();
        }

        return ((char)('A' + n - lettersCnt)).ToString();
    }

    /// <summary>Whether every character is tab, line feed, carriage return or printable ASCII (0x20 to 0x7E).</summary>
    public static bool IsBasicASCII(ReadOnlySpan<char> str)
    {
        foreach (var ch in str)
        {
            if (!(ch is '\t' or '\n' or '\r' or (>= (char)0x20 and <= (char)0x7E)))
            {
                return false;
            }
        }
        return true;
    }
}

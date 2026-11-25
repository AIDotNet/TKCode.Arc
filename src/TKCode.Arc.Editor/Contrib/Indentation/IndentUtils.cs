// Ported from vscode (src/vs/editor/contrib/indentation/common/indentUtils.ts, commit b57d5c9e), MIT license.
// Differences: the module functions are in the static class IndentUtils.

using System.Text;

namespace TKCode.Arc.Editor.Contrib.Indentation;

/// <summary>Counts and builds indentation.</summary>
internal static class IndentUtils
{
    /// <summary>The width of <paramref name="str"/> in spaces, a tab counting <paramref name="tabSize"/>.</summary>
    public static int GetSpaceCnt(string str, int tabSize)
    {
        var spacesCnt = 0;

        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] == '\t')
            {
                spacesCnt += tabSize;
            }
            else
            {
                spacesCnt++;
            }
        }

        return spacesCnt;
    }

    /// <summary>An indentation <paramref name="spacesCnt"/> spaces wide, with tabs unless <paramref name="insertSpaces"/>.</summary>
    public static string GenerateIndent(int spacesCnt, int tabSize, bool insertSpaces)
    {
        spacesCnt = spacesCnt < 0 ? 0 : spacesCnt;

        var result = new StringBuilder();
        if (!insertSpaces)
        {
            var tabsCnt = spacesCnt / tabSize;
            spacesCnt %= tabSize;
            for (var i = 0; i < tabsCnt; i++)
            {
                result.Append('\t');
            }
        }

        for (var i = 0; i < spacesCnt; i++)
        {
            result.Append(' ');
        }

        return result.ToString();
    }
}

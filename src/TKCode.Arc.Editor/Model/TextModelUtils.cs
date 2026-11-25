// Ported from vscode (src/vs/editor/common/model/utils.ts, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

internal static class TextModelUtils
{
    /// <summary>
    /// Returns -1 when the line consists of whitespace; otherwise the indent level (the visible column of the first
    /// character that is not a space or a tab).
    /// </summary>
    public static int ComputeIndentLevel(string line, int tabSize)
    {
        var indent = 0;
        var i = 0;
        var len = line.Length;

        while (i < len)
        {
            var chCode = line[i];
            if (chCode == ' ')
            {
                indent++;
            }
            else if (chCode == '\t')
            {
                indent = indent - (indent % tabSize) + tabSize;
            }
            else
            {
                break;
            }

            i++;
        }

        if (i == len)
        {
            return -1; // line only consists of whitespace
        }

        return indent;
    }
}

// Ported from vscode (src/vs/editor/common/model/textModelSearch.ts: LineFeedCounter, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>Counts the line feeds before an offset, for mapping a multiline match back onto a CRLF document.</summary>
internal sealed class LineFeedCounter
{
    private readonly int[] _lineFeedOffsets;

    internal LineFeedCounter(string text)
    {
        var offsets = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                offsets.Add(i);
            }
        }

        _lineFeedOffsets = offsets.ToArray();
    }

    /// <summary>The number of line feeds strictly before <paramref name="offset"/>.</summary>
    internal int FindLineFeedCountBeforeOffset(int offset)
    {
        var lineFeedOffsets = _lineFeedOffsets;
        var min = 0;
        var max = lineFeedOffsets.Length - 1;

        if (max == -1)
        {
            // no line feeds
            return 0;
        }

        if (offset <= lineFeedOffsets[0])
        {
            // before first line feed
            return 0;
        }

        while (min < max)
        {
            var mid = min + ((max - min) / 2);

            if (lineFeedOffsets[mid] >= offset)
            {
                max = mid - 1;
            }
            else
            {
                if (lineFeedOffsets[mid + 1] >= offset)
                {
                    // bingo!
                    min = mid;
                    max = mid;
                }
                else
                {
                    min = mid + 1;
                }
            }
        }

        return min + 1;
    }
}

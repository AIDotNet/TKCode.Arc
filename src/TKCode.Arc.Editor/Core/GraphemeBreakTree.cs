// Ported from vscode (src/vs/base/common/strings.ts: GraphemeBreakTree, commit b57d5c9e), MIT license.
// Differences from upstream: the instance is created eagerly and holds no state (the table is a constant span in
// GraphemeBreakTree.Data.cs), so it is safe to share between threads; the lookup stops at the end of the table
// instead of reading past it (no code point reaches that, but upstream would return undefined there).

namespace TKCode.Arc.Editor;

/// <summary>Looks up the <see cref="GraphemeBreakType"/> of a code point.</summary>
internal sealed partial class GraphemeBreakTree
{
    private static readonly GraphemeBreakTree s_instance = new();

    private GraphemeBreakTree()
    {
    }

    public static GraphemeBreakTree GetInstance() => s_instance;

    public GraphemeBreakType GetGraphemeBreakType(int codePoint)
    {
        // !!! Let's make 7bit ASCII a bit faster: 0..31
        if (codePoint < 32)
        {
            if (codePoint == '\n')
            {
                return GraphemeBreakType.LF;
            }
            if (codePoint == '\r')
            {
                return GraphemeBreakType.CR;
            }
            return GraphemeBreakType.Control;
        }
        // !!! Let's make 7bit ASCII a bit faster: 32..126
        if (codePoint < 127)
        {
            return GraphemeBreakType.Other;
        }

        var data = GraphemeBreakRawData;
        var nodeCount = data.Length / 3;
        var nodeIndex = 1;
        while (nodeIndex < nodeCount)
        {
            if (codePoint < data[3 * nodeIndex])
            {
                // go left
                nodeIndex = 2 * nodeIndex;
            }
            else if (codePoint > data[3 * nodeIndex + 1])
            {
                // go right
                nodeIndex = 2 * nodeIndex + 1;
            }
            else
            {
                // hit
                return (GraphemeBreakType)data[3 * nodeIndex + 2];
            }
        }

        return GraphemeBreakType.Other;
    }
}

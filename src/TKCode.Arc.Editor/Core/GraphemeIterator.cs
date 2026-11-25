// Ported from vscode (src/vs/base/common/strings.ts: GraphemeIterator, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor;

/// <summary>
/// Walks a string one grapheme cluster at a time, using the simplified UAX #29 rules of
/// <see cref="Strings.BreakBetweenGraphemeBreakType"/>.
/// </summary>
internal sealed class GraphemeIterator
{
    private readonly CodePointIterator _iterator;

    public GraphemeIterator(string str, int offset = 0)
    {
        _iterator = new CodePointIterator(str, offset);
    }

    /// <summary>The current offset in UTF-16 code units.</summary>
    public int Offset => _iterator.Offset;

    /// <summary>The length in code units of the grapheme that starts at the offset; moves the offset after it.</summary>
    public int NextGraphemeLength()
    {
        var graphemeBreakTree = GraphemeBreakTree.GetInstance();
        var iterator = _iterator;
        var initialOffset = iterator.Offset;

        var graphemeBreakType = graphemeBreakTree.GetGraphemeBreakType(iterator.NextCodePoint());
        while (!iterator.Eol())
        {
            var offset = iterator.Offset;
            var nextGraphemeBreakType = graphemeBreakTree.GetGraphemeBreakType(iterator.NextCodePoint());
            if (Strings.BreakBetweenGraphemeBreakType(graphemeBreakType, nextGraphemeBreakType))
            {
                // move iterator back
                iterator.SetOffset(offset);
                break;
            }
            graphemeBreakType = nextGraphemeBreakType;
        }
        return iterator.Offset - initialOffset;
    }

    /// <summary>The length in code units of the grapheme that ends at the offset; moves the offset before it.</summary>
    public int PrevGraphemeLength()
    {
        var graphemeBreakTree = GraphemeBreakTree.GetInstance();
        var iterator = _iterator;
        var initialOffset = iterator.Offset;

        var graphemeBreakType = graphemeBreakTree.GetGraphemeBreakType(iterator.PrevCodePoint());
        while (iterator.Offset > 0)
        {
            var offset = iterator.Offset;
            var prevGraphemeBreakType = graphemeBreakTree.GetGraphemeBreakType(iterator.PrevCodePoint());
            if (Strings.BreakBetweenGraphemeBreakType(prevGraphemeBreakType, graphemeBreakType))
            {
                // move iterator back
                iterator.SetOffset(offset);
                break;
            }
            graphemeBreakType = prevGraphemeBreakType;
        }
        return initialOffset - iterator.Offset;
    }

    /// <summary>Whether the offset is at or past the end of the string.</summary>
    public bool Eol() => _iterator.Eol();
}

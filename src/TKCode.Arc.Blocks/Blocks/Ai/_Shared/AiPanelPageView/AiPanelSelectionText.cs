using System.Text;

namespace TKCode.Arc;

/// <summary>One selectable text of the page in reading order: a heading, a paragraph or a table cell.</summary>
/// <param name="Block">The block the text belongs to.</param>
/// <param name="Text">The text.</param>
/// <param name="Row">The table row (0 is the header row), or -1 outside a table.</param>
/// <param name="Column">The table column, or -1 outside a table.</param>
internal sealed record AiPanelTextPiece(AiPanelBlock Block, string Text, int Row = -1, int Column = -1);

/// <summary>A caret position on the page: a character offset in one of the selectable texts (in reading order).</summary>
/// <param name="Index">The text, in reading order.</param>
/// <param name="Offset">The character offset in it.</param>
internal readonly record struct AiPanelTextPoint(int Index, int Offset) : IComparable<AiPanelTextPoint>
{
    public int CompareTo(AiPanelTextPoint other) => Index != other.Index ? Index.CompareTo(other.Index) : Offset.CompareTo(other.Offset);
}

/// <summary>
/// The page selection as data: which part of each text a range covers, the text it reads as and the block it belongs to.
/// Pure, so the rules are tested without pointers. A range belongs to the block it starts in (the first text with selected
/// characters), whichever way it was dragged; a selection in a table takes the table's label.
/// </summary>
internal static class AiPanelSelectionText
{
    /// <summary>The two ends in reading order.</summary>
    public static (AiPanelTextPoint Start, AiPanelTextPoint End) Order(AiPanelTextPoint a, AiPanelTextPoint b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>The selected characters of text <paramref name="index"/> (empty when the range does not reach it).</summary>
    public static (int Start, int End) RangeIn(int index, int length, AiPanelTextPoint start, AiPanelTextPoint end)
    {
        if (index < start.Index || index > end.Index)
        {
            return (0, 0);
        }
        var from = index == start.Index ? Math.Clamp(start.Offset, 0, length) : 0;
        var to = index == end.Index ? Math.Clamp(end.Offset, 0, length) : length;
        return to > from ? (from, to) : (0, 0);
    }

    /// <summary>
    /// Describes the range between <paramref name="anchor"/> and <paramref name="focus"/>, or null when it selects no character.
    /// Texts join with a tab between cells of one table row and a line break between rows and between blocks.
    /// </summary>
    public static AiPanelSelection? Describe(IReadOnlyList<AiPanelTextPiece> pieces, AiPanelTextPoint anchor, AiPanelTextPoint focus,
                                             IReadOnlyList<AiPanelBlock> blocks)
    {
        var (start, end) = Order(anchor, focus);
        AiPanelBlock? owner = null;
        var text = new StringBuilder();
        AiPanelTextPiece? previous = null;
        for (var i = Math.Max(0, start.Index); i <= end.Index && i < pieces.Count; i++)
        {
            var piece = pieces[i];
            var (from, to) = RangeIn(i, piece.Text.Length, start, end);
            if (to <= from)
            {
                continue;
            }
            if (previous is not null)
            {
                var sameRow = ReferenceEquals(previous.Block, piece.Block) && previous.Row >= 0 && previous.Row == piece.Row;
                text.Append(sameRow ? '\t' : '\n');
            }
            owner ??= piece.Block;
            text.Append(piece.Text, from, to - from);
            previous = piece;
        }
        return owner is null ? null : new AiPanelSelection(owner.Id, LabelOf(owner, blocks), text.ToString());
    }

    /// <summary>
    /// The name a selection in <paramref name="block"/> goes by: its label; a heading without one is named by its text, any
    /// other block by the nearest heading above it; empty when there is none.
    /// </summary>
    public static string LabelOf(AiPanelBlock block, IReadOnlyList<AiPanelBlock> blocks)
    {
        if (!string.IsNullOrWhiteSpace(block.Label))
        {
            return block.Label!;
        }
        if (block is AiPanelHeading own)
        {
            return own.Text;
        }
        var index = -1;
        for (var i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], block))
            {
                index = i;
                break;
            }
        }
        for (var i = index - 1; i >= 0; i--)
        {
            if (blocks[i] is AiPanelHeading heading)
            {
                return string.IsNullOrWhiteSpace(heading.Label) ? heading.Text : heading.Label!;
            }
        }
        return string.Empty;
    }

    /// <summary>The word at <paramref name="offset"/> (a double click): letters, digits and joiners, or the single other character there.</summary>
    public static (int Start, int End) WordAt(string text, int offset)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }
        offset = Math.Clamp(offset, 0, text.Length);
        // A caret after the last letter of a word selects that word.
        var probe = offset < text.Length && IsWordChar(text[offset]) ? offset
            : offset > 0 && IsWordChar(text[offset - 1]) ? offset - 1
            : offset;
        if (probe >= text.Length || !IsWordChar(text[probe]))
        {
            var single = Math.Min(probe, text.Length - 1);
            return (single, single + 1);
        }
        var start = probe;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }
        var end = probe;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }
        return (start, end);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '\'' or '’' or '_' or '-';
}

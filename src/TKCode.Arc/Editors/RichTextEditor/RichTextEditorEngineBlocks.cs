using System.Collections.Immutable;

namespace TKCode.Arc;

/// <summary>Whole-block commands behind the block handle: units (a block with what belongs to it) moved, copied, removed and added after.</summary>
internal sealed partial class RichTextEditorEngine
{
    /// <summary>
    /// The unit the block at <paramref name="index"/> starts or belongs to, as <c>[Start, End)</c>: a table is one unit, a
    /// list item takes its nested items along, every other block stands alone.
    /// </summary>
    public static (int Start, int End) UnitExtent(IReadOnlyList<RichTextEditorBlock> blocks, int index)
    {
        var block = blocks[index];
        if (block.IsCell)
        {
            var table = RichTextEditorTables.Extent(blocks, index);
            return (table.Start, table.End);
        }
        var end = index + 1;
        if (block.IsCallout)
        {
            // A box of several callout paragraphs moves, copies and goes as one.
            var start = index;
            while (start > 0 && blocks[start - 1].SharesCalloutBox(block))
            {
                start--;
            }
            while (end < blocks.Count && blocks[end].SharesCalloutBox(block))
            {
                end++;
            }
            return (start, end);
        }
        if (block.IsList)
        {
            while (end < blocks.Count && blocks[end].IsList && blocks[end].Level > block.Level)
            {
                end++;
            }
        }
        return (index, end);
    }

    /// <summary>True when a block boundary may hold a moved unit: everywhere except between the cells of a table.</summary>
    public static bool IsDropGap(IReadOnlyList<RichTextEditorBlock> blocks, int gap) =>
        gap == blocks.Count || !blocks[gap].IsCell || blocks[gap].StartsTable;

    /// <summary>Moves the unit holding <paramref name="index"/> so it comes before the block at <paramref name="gap"/> (the block count means the end).</summary>
    public bool MoveUnit(int index, int gap)
    {
        if (index < 0 || index >= _blocks.Count || gap < 0 || gap > _blocks.Count || !IsDropGap(_blocks, gap))
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        if (gap >= start && gap <= end)
        {
            return false;
        }
        var moved = _blocks.GetRange(start, end - start);
        var rest = _blocks.RemoveRange(start, end - start);
        var at = gap > end ? gap - (end - start) : gap;
        var blocks = rest.InsertRange(at, moved);
        Commit(blocks, RichTextEditorSelection.Caret(NearestTextual(blocks, Math.Min(at, blocks.Count - 1), forward: true)), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>Moves the unit holding <paramref name="index"/> above the unit before it (<paramref name="direction"/> -1) or below the unit after it (1).</summary>
    public bool MoveUnitBy(int index, int direction)
    {
        if (index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        if (direction < 0)
        {
            return start > 0 && MoveUnit(index, UnitExtent(_blocks, start - 1).Start);
        }
        return end < _blocks.Count && MoveUnit(index, UnitExtent(_blocks, end).End);
    }

    /// <summary>Copies the unit holding <paramref name="index"/> right after itself and puts the caret in the copy.</summary>
    public bool DuplicateUnit(int index)
    {
        if (index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        var blocks = _blocks.InsertRange(end, _blocks.GetRange(start, end - start));
        Commit(blocks, RichTextEditorSelection.Caret(NearestTextual(blocks, end, forward: true)), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>Removes the unit holding <paramref name="index"/>; an emptied document keeps one empty paragraph.</summary>
    public bool DeleteUnit(int index)
    {
        if (index < 0 || index >= _blocks.Count)
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        var blocks = _blocks.RemoveRange(start, end - start);
        if (blocks.Count == 0)
        {
            blocks = blocks.Add(RichTextEditorBlock.EmptyParagraph);
        }
        Commit(blocks, RichTextEditorSelection.Caret(NearestTextual(blocks, Math.Min(start, blocks.Count - 1), forward: true)), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>Adds an empty paragraph after the unit holding <paramref name="index"/> and returns its index (the caret moves into it).</summary>
    public int InsertParagraphAfterUnit(int index)
    {
        var (_, end) = UnitExtent(_blocks, index);
        var blocks = _blocks.Insert(end, RichTextEditorBlock.EmptyParagraph);
        Commit(blocks, RichTextEditorSelection.Caret(end, 0), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return end;
    }

    /// <summary>Sets the color and icon of the callout box holding the block at <paramref name="index"/> (null keeps the current one).</summary>
    public bool SetCallout(int index, RichTextColor? tone, string? icon)
    {
        if (!Docs || index < 0 || index >= _blocks.Count || !_blocks[index].IsCallout)
        {
            return false;
        }
        var (start, end) = UnitExtent(_blocks, index);
        var blocks = _blocks;
        for (var i = start; i < end; i++)
        {
            var attrs = blocks[i].Attrs;
            blocks = blocks.SetItem(i, blocks[i].WithAttrs(attrs with { Tone = tone ?? attrs.Tone, Icon = icon ?? attrs.Icon }));
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Turns the block at <paramref name="index"/> into <paramref name="type"/> (as the toolbar does for the caret block).</summary>
    public void SetBlockTypeAt(int index, RichTextBlockType type)
    {
        if (index < 0 || index >= _blocks.Count || !_blocks[index].IsTextual || _blocks[index].IsCell)
        {
            return;
        }
        _selection = RichTextEditorSelection.Caret(index, Math.Min(_selection.Focus.Block == index ? _selection.Focus.Offset : 0, _blocks[index].Length));
        SetBlockType(type);
    }
}

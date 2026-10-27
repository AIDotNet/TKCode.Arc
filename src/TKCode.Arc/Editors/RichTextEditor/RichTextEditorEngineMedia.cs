using System.Collections.Immutable;

namespace TKCode.Arc;

/// <summary>Images and tables: inserting them, moving between cells and editing a table's rows and columns.</summary>
internal sealed partial class RichTextEditorEngine
{
    private const int MaxTableRows = RichTextEditorTables.MaxRows;
    private const int MaxTableColumns = RichTextEditorTables.MaxColumns;

    /// <summary>True when the caret rests in a table cell.</summary>
    public bool IsInTable => _blocks[_selection.Focus.Block].IsCell;

    /// <summary>True when the selection is collapsed on an image (the image is selected as a whole).</summary>
    public bool IsImageSelected => _selection.IsCollapsed && _blocks[_selection.Focus.Block].IsImage;

    private void RemoveImage(int index)
    {
        var blocks = _blocks.RemoveAt(index);
        if (blocks.Count == 0)
        {
            blocks = blocks.Add(RichTextEditorBlock.EmptyParagraph);
        }
        var caret = NearestTextual(blocks, Math.Min(index, blocks.Count - 1), forward: true);
        Commit(blocks, RichTextEditorSelection.Caret(caret), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
    }

    /// <summary>
    /// Puts <paramref name="insert"/> at the caret as whole blocks: in place of an empty paragraph, between the halves of a split
    /// paragraph, before a block the caret is at the start of, and after anything else (a list, a table or an atomic block as a whole).
    /// </summary>
    private static (ImmutableList<RichTextEditorBlock> Blocks, int At) Place(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition caret, IReadOnlyList<RichTextEditorBlock> insert)
    {
        var index = caret.Block;
        var block = blocks[index];
        int at;
        if (block.IsEmpty && block.Type == RichTextBlockType.Paragraph)
        {
            blocks = blocks.RemoveAt(index);
            at = index;
        }
        else if (block.IsCell)
        {
            at = RichTextEditorTables.Extent(blocks, index).End;
        }
        else if (block.IsAtomic)
        {
            at = index + 1;
        }
        else if (block.IsList)
        {
            at = index + 1;
            while (at < blocks.Count && blocks[at].IsList)
            {
                at++;
            }
        }
        else if (!block.IsCode && caret.Offset > 0 && caret.Offset < block.Length)
        {
            var rest = RichTextEditorBlock.Create(block.Type, block.Level, block.Slice(caret.Offset, block.Length), block.ContinuedAttrs);
            blocks = blocks.SetItem(index, block.Delete(caret.Offset, block.Length)).Insert(index + 1, rest);
            at = index + 1;
        }
        else
        {
            at = !block.IsCode && caret.Offset == 0 && block.Length > 0 ? index : index + 1;
        }
        return (blocks.InsertRange(at, insert), at);
    }

    /// <summary>Inserts an image at the caret (the Docs profile only); the caret moves to the paragraph after it. Returns the image's block index, or -1.</summary>
    public int InsertImage(string src, string? alt)
    {
        if (!Docs || string.IsNullOrWhiteSpace(src) || !RichTextEditorHtml.IsSafeImageUrl(src))
        {
            return -1;
        }
        var (blocks, caret) = DeleteSelection();
        var (placed, at) = Place(blocks, caret, new[] { RichTextEditorBlock.Image(src.Trim(), alt) });
        blocks = placed;
        var after = ParagraphAfter(ref blocks, at);
        Commit(blocks, RichTextEditorSelection.Caret(after, 0), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return at;
    }

    /// <summary>Changes the address and description of the image at <paramref name="block"/>.</summary>
    public bool SetImage(int block, string src, string? alt)
    {
        if (block < 0 || block >= _blocks.Count || !_blocks[block].IsImage || string.IsNullOrWhiteSpace(src) || !RichTextEditorHtml.IsSafeImageUrl(src))
        {
            return false;
        }
        var image = _blocks[block];
        var changed = image.WithAttrs(image.Attrs with { Src = src.Trim(), Alt = alt });
        if (changed.Attrs == image.Attrs)
        {
            return false;
        }
        Commit(_blocks.SetItem(block, changed), _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Sets the width of the image at <paramref name="block"/> in pixels; 0 returns it to its natural size.</summary>
    public bool SetImageWidth(int block, int width)
    {
        if (block < 0 || block >= _blocks.Count || !_blocks[block].IsImage)
        {
            return false;
        }
        var image = _blocks[block];
        var changed = image.WithAttrs(image.Attrs with { Width = width });
        if (changed.Attrs == image.Attrs)
        {
            return false;
        }
        Commit(_blocks.SetItem(block, changed), _selection, RichTextEditorHistoryKind.Command, true);
        return true;
    }

    /// <summary>Selects the image at <paramref name="block"/> as a whole.</summary>
    public void SelectImage(int block)
    {
        if (block >= 0 && block < _blocks.Count && _blocks[block].IsImage)
        {
            SetSelection(RichTextEditorSelection.Caret(block, 0));
        }
    }

    /// <summary>Inserts a table of empty cells (the Docs profile only); the caret moves into its first cell. Returns false when the size is out of range.</summary>
    public bool InsertTable(int rows, int cols)
    {
        if (!Docs || rows < 1 || cols < 1 || rows > MaxTableRows || cols > MaxTableColumns)
        {
            return false;
        }
        var (blocks, caret) = DeleteSelection();
        CommitTable(blocks, caret, rows, cols);
        return true;
    }

    private void CommitTable(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition caret, int rows, int cols)
    {
        var (placed, at) = Place(blocks, caret, RichTextEditorTables.Create(rows, cols));
        Commit(placed, RichTextEditorSelection.Caret(at, 0), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
    }

    /// <summary>Tab in a table: the next cell (a new row after the last one); Shift+Tab: the previous cell. Returns false outside a table.</summary>
    public bool TabInTable(bool backward)
    {
        var blocks = _blocks;
        var index = _selection.Focus.Block;
        if (!blocks[index].IsCell)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(blocks, index);
        var row = blocks[index].Row;
        var col = blocks[index].Col;
        do
        {
            if (backward)
            {
                if (col > 0)
                {
                    col--;
                }
                else if (row > 0)
                {
                    row--;
                    col = table.Cols - 1;
                }
                else
                {
                    break;
                }
            }
            else if (col + 1 < table.Cols)
            {
                col++;
            }
            else
            {
                row++;
                col = 0;
            }
        }
        while (row < table.Rows && blocks[table.IndexOf(row, col)].IsCovered);
        GoToCell(blocks, table, row, col);
        return true;
    }

    /// <summary>Enter in a cell: the cell below in the same column, with a new row at the bottom.</summary>
    private void MoveInTable(ImmutableList<RichTextEditorBlock> blocks, int index, int rowStep, int columnStep, bool addRowAtEnd)
    {
        var table = RichTextEditorTables.Extent(blocks, index);
        var cell = blocks[index];
        var row = Math.Clamp(rowStep > 0 ? cell.Row + cell.RowSpan : cell.Row + rowStep, 0, addRowAtEnd ? table.Rows : table.Rows - 1);
        var col = Math.Clamp(columnStep > 0 ? cell.Col + cell.ColSpan : cell.Col + columnStep, 0, table.Cols - 1);
        GoToCell(blocks, table, row, col);
    }

    private void GoToCell(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, int row, int col)
    {
        if (row >= table.Rows)
        {
            blocks = blocks.InsertRange(table.End, NewRow(blocks, table, table.Rows));
            table = table with { End = table.End + table.Cols, Rows = table.Rows + 1 };
        }
        var caret = RichTextEditorSelection.Caret(RichTextEditorTables.OwnerIndex(blocks, table, row, col), 0);
        if (ReferenceEquals(blocks, _blocks))
        {
            SetSelection(caret);
        }
        else
        {
            Commit(blocks, caret, RichTextEditorHistoryKind.Command, true);
        }
        _lastInput = InputKind.None;
    }

    private static List<RichTextEditorBlock> NewRow(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, int row)
    {
        var cells = new List<RichTextEditorBlock>(table.Cols);
        for (var c = 0; c < table.Cols; c++)
        {
            var align = blocks[table.IndexOf(0, c)].Align;
            cells.Add(RichTextEditorBlock.Cell(row, c).WithAttrs(new RichTextEditorBlockAttrs(Align: align, Row: row, Col: c)));
        }
        return cells;
    }

    /// <summary>Adds an empty row below the caret's row (above the header is the same as below it); below a merged cell is below its last row.</summary>
    public bool AddTableRow(bool below = true) => EditTable((grid, table, row, col) =>
    {
        var at = below || row == 0 ? row + grid[row][col].RowSpan : row;
        RichTextEditorTables.InsertRow(grid, at, c => RichTextEditorBlock.Cell(at, c).WithAttrs(new RichTextEditorBlockAttrs(Align: grid[0][c].Align, Row: at, Col: c)));
        return (at <= row ? row + 1 : row, col);
    }, max: table => table.Rows < MaxTableRows);

    /// <summary>Adds an empty column after (or before) the caret's column; after a merged cell is after its last column.</summary>
    public bool AddTableColumn(bool after = true) => EditTable((grid, table, row, col) =>
    {
        var at = after ? col + grid[row][col].ColSpan : col;
        RichTextEditorTables.InsertColumn(grid, at, _ => RichTextEditorBlock.Cell(0, at));
        return (row, at <= col ? col + 1 : col);
    }, max: table => table.Cols < MaxTableColumns);

    /// <summary>Deletes the caret's row (the whole table when it is the only row).</summary>
    public bool DeleteTableRow()
    {
        if (!IsInTable)
        {
            return false;
        }
        if (RichTextEditorTables.Extent(_blocks, _selection.Focus.Block).Rows == 1)
        {
            return DeleteTable();
        }
        return EditTable((grid, _, row, col) =>
        {
            RichTextEditorTables.DeleteRow(grid, row);
            return (Math.Min(row, grid.Count - 1), col);
        });
    }

    /// <summary>Deletes the caret's column (the whole table when it is the only column).</summary>
    public bool DeleteTableColumn()
    {
        if (!IsInTable)
        {
            return false;
        }
        if (RichTextEditorTables.Extent(_blocks, _selection.Focus.Block).Cols == 1)
        {
            return DeleteTable();
        }
        return EditTable((grid, _, row, col) =>
        {
            RichTextEditorTables.DeleteColumn(grid, col);
            return (row, Math.Min(col, grid[0].Count - 1));
        });
    }

    /// <summary>
    /// The cells the selection reaches as a rectangle of grid positions, widened to whole merged cells: the corners are the cell where
    /// the selection starts and the one where it ends. Null unless both lie in one table.
    /// </summary>
    private (RichTextEditorTableExtent Table, int R1, int C1, int R2, int C2)? SelectedCells()
    {
        var first = _blocks[_selection.Start.Block];
        var last = _blocks[_selection.End.Block];
        if (!Docs || !first.IsCell || !last.IsCell)
        {
            return null;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Start.Block);
        if (_selection.End.Block >= table.End)
        {
            return null;
        }
        var (r1, c1, r2, c2) = RichTextEditorTables.Expand(
            (r, c) => _blocks[table.IndexOf(r, c)],
            Math.Min(first.Row, last.Row), Math.Min(first.Col, last.Col),
            Math.Max(first.Row + first.RowSpan - 1, last.Row + last.RowSpan - 1), Math.Max(first.Col + first.ColSpan - 1, last.Col + last.ColSpan - 1));
        return (table, r1, c1, r2, c2);
    }

    /// <summary>True when the selection reaches two or more cells of one table, so <see cref="MergeCells"/> has something to merge.</summary>
    public bool CanMergeCells => SelectedCells() is { } rect
        && RichTextEditorTables.IsMergeable((r, c) => _blocks[rect.Table.IndexOf(r, c)], rect.R1, rect.C1, rect.R2, rect.C2);

    /// <summary>True when the caret is in a merged cell, so <see cref="SplitCell"/> has something to split.</summary>
    public bool CanSplitCell => Docs && IsInTable && _blocks[_selection.Focus.Block].IsMerged;

    /// <summary>Merges the cells the selection reaches into one; their texts join as lines and the caret goes to the merged cell.</summary>
    public bool MergeCells()
    {
        if (!CanMergeCells || SelectedCells() is not { } rect)
        {
            return false;
        }
        return EditTable((grid, _, _, _) =>
        {
            RichTextEditorTables.Merge(grid, rect.R1, rect.C1, rect.R2, rect.C2);
            return (rect.R1, rect.C1);
        });
    }

    /// <summary>Splits the merged cell at the caret back into single cells; the text stays in the first one.</summary>
    public bool SplitCell()
    {
        if (!CanSplitCell)
        {
            return false;
        }
        return EditTable((grid, _, row, col) => RichTextEditorTables.Split(grid, row, col) ?? (row, col));
    }

    /// <summary>True when the table at the caret draws its first row as a header.</summary>
    public bool HasHeaderRow => IsInTable && !_blocks[RichTextEditorTables.Extent(_blocks, _selection.Focus.Block).Start].Attrs.NoHeaderRow;

    /// <summary>True when the table at the caret draws its first column as a header.</summary>
    public bool HasHeaderColumn => IsInTable && _blocks[RichTextEditorTables.Extent(_blocks, _selection.Focus.Block).Start].Attrs.HeaderColumn;

    /// <summary>Turns the header row (<paramref name="column"/> false) or the header column of the table at the caret on or off.</summary>
    public bool SetTableHeader(bool column, bool on)
    {
        if (!Docs || !IsInTable)
        {
            return false;
        }
        var start = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block).Start;
        var head = _blocks[start];
        var changed = head.WithAttrs(column ? head.Attrs with { HeaderColumn = on } : head.Attrs with { NoHeaderRow = !on });
        if (changed.Attrs == head.Attrs)
        {
            return false;
        }
        Commit(_blocks.SetItem(start, changed), _selection, RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>The background shared by the cells the selection reaches, or <see cref="RichTextColor.Default"/> when they differ or none is set.</summary>
    public RichTextColor CellTone
    {
        get
        {
            if (SelectedCells() is not { } rect)
            {
                return RichTextColor.Default;
            }
            var tone = _blocks[rect.Table.IndexOf(rect.R1, rect.C1)].Tone;
            for (var r = rect.R1; r <= rect.R2; r++)
            {
                for (var c = rect.C1; c <= rect.C2; c++)
                {
                    var cell = _blocks[rect.Table.IndexOf(r, c)];
                    if (!cell.IsCovered && cell.Tone != tone)
                    {
                        return RichTextColor.Default;
                    }
                }
            }
            return tone;
        }
    }

    /// <summary>Gives the cells the selection reaches the background <paramref name="tone"/> (<see cref="RichTextColor.Default"/> clears it).</summary>
    public bool SetCellTone(RichTextColor tone)
    {
        if (!Docs || SelectedCells() is not { } rect)
        {
            return false;
        }
        var blocks = _blocks;
        for (var r = rect.R1; r <= rect.R2; r++)
        {
            for (var c = rect.C1; c <= rect.C2; c++)
            {
                var index = rect.Table.IndexOf(r, c);
                if (!blocks[index].IsCovered)
                {
                    blocks = blocks.SetItem(index, blocks[index].WithAttrs(blocks[index].Attrs with { Tone = tone }));
                }
            }
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>
    /// Stores the column widths (pixels; 0 shares the free width) of the table holding the cell at <paramref name="cell"/>; extra widths are
    /// ignored and missing ones are 0.
    /// </summary>
    public bool SetColumnWidths(int cell, IReadOnlyList<int> widths)
    {
        if (!Docs || cell < 0 || cell >= _blocks.Count || !_blocks[cell].IsCell)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, cell);
        var blocks = _blocks;
        for (var c = 0; c < table.Cols; c++)
        {
            var index = table.IndexOf(0, c);
            blocks = blocks.SetItem(index, blocks[index].WithAttrs(blocks[index].Attrs with { Width = c < widths.Count ? widths[c] : 0 }));
        }
        if (RichTextEditorHistory.SameContent(blocks, _blocks))
        {
            return false;
        }
        Commit(blocks, _selection, RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>
    /// The rectangle of cells the selection picks when it runs from one cell to another of the same table (without covering the whole table
    /// from its first character to its last): those cells draw selected and deleting, copying or pasting works on them as a grid.
    /// </summary>
    public (RichTextEditorTableExtent Table, int R1, int C1, int R2, int C2)? CellSelection =>
        _selection.Anchor.Block != _selection.Focus.Block && SelectedCells() is { } rect && !CoversTable(_blocks, rect.Table.Start, _selection.Start, _selection.End)
            ? rect
            : null;

    /// <summary>The blocks with the cells of <paramref name="rect"/> emptied.</summary>
    private static ImmutableList<RichTextEditorBlock> EmptyCells(ImmutableList<RichTextEditorBlock> blocks, (RichTextEditorTableExtent Table, int R1, int C1, int R2, int C2) rect)
    {
        for (var r = rect.R1; r <= rect.R2; r++)
        {
            for (var c = rect.C1; c <= rect.C2; c++)
            {
                var index = rect.Table.IndexOf(r, c);
                if (blocks[index].Length > 0)
                {
                    blocks = blocks.SetItem(index, blocks[index].Delete(0, blocks[index].Length));
                }
            }
        }
        return blocks;
    }

    /// <summary>The cells of <paramref name="rect"/> as a table of their own (for copying), keeping their merges, colors and alignment.</summary>
    private List<RichTextEditorBlock> CellFragment((RichTextEditorTableExtent Table, int R1, int C1, int R2, int C2) rect)
    {
        var result = new List<RichTextEditorBlock>();
        for (var r = rect.R1; r <= rect.R2; r++)
        {
            for (var c = rect.C1; c <= rect.C2; c++)
            {
                var cell = _blocks[rect.Table.IndexOf(r, c)];
                var width = _blocks[rect.Table.IndexOf(0, c)].Width;
                var attrs = cell.Attrs with { Row = r - rect.R1, Col = c - rect.C1, Width = r == rect.R1 ? width : 0, NoHeaderRow = false, HeaderColumn = false };
                result.Add(RichTextEditorBlock.Create(RichTextBlockType.TableCell, 0, cell.Slice(0, cell.Length), attrs));
            }
        }
        return result;
    }

    /// <summary>The cells of <paramref name="rect"/> as tab-separated text: a tab between cells, a line per row, line breaks inside a cell as spaces.</summary>
    private string CellText((RichTextEditorTableExtent Table, int R1, int C1, int R2, int C2) rect)
    {
        var sb = new System.Text.StringBuilder();
        for (var r = rect.R1; r <= rect.R2; r++)
        {
            if (r > rect.R1)
            {
                sb.Append('\n');
            }
            for (var c = rect.C1; c <= rect.C2; c++)
            {
                if (c > rect.C1)
                {
                    sb.Append('\t');
                }
                sb.Append(_blocks[rect.Table.IndexOf(r, c)].Text.Replace('\n', ' ').Replace('\t', ' '));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// The grid a paste brings into a table: the cells of a pasted table (one table and nothing else), or tab-separated text. Null when the
    /// paste is something else.
    /// </summary>
    private static List<List<IReadOnlyList<RichTextEditorRun>>>? PastedGrid(string text, IReadOnlyList<RichTextEditorBlock>? fragment)
    {
        if (fragment is { Count: > 0 } && fragment.All(b => b.IsCell) && fragment[0].StartsTable && fragment.Skip(1).All(b => !b.StartsTable))
        {
            var rows = fragment.Max(b => b.Row) + 1;
            var cols = fragment.Max(b => b.Col) + 1;
            var grid = new List<List<IReadOnlyList<RichTextEditorRun>>>(rows);
            for (var r = 0; r < rows; r++)
            {
                grid.Add(Enumerable.Range(0, cols).Select(_ => (IReadOnlyList<RichTextEditorRun>)Array.Empty<RichTextEditorRun>()).ToList());
            }
            foreach (var cell in fragment)
            {
                grid[cell.Row][cell.Col] = cell.Slice(0, cell.Length);
            }
            return grid;
        }
        if (fragment is not null || !text.Contains('\t'))
        {
            return null;
        }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
        return lines.Select(line => line.Split('\t')
            .Select(value => (IReadOnlyList<RichTextEditorRun>)(value.Length == 0 ? Array.Empty<RichTextEditorRun>() : new[] { new RichTextEditorRun(value) }))
            .ToList()).ToList();
    }

    /// <summary>
    /// Pastes a grid into the table at the selection: it fills the cells from the selected rectangle's (or the caret's) cell rightwards and
    /// downwards, adding rows and columns it needs (up to the limits). Covered cells are skipped. False when the paste is not a grid.
    /// </summary>
    private bool PasteCells(string text, IReadOnlyList<RichTextEditorBlock>? fragment)
    {
        if (!Docs || PastedGrid(text, fragment) is not { } source)
        {
            return false;
        }
        int origin;
        if (CellSelection is { } rect)
        {
            origin = rect.Table.IndexOf(rect.R1, rect.C1);
        }
        else if (_blocks[_selection.Start.Block].IsCell && _selection.Start.Block == _selection.End.Block)
        {
            origin = _selection.Start.Block;
        }
        else
        {
            return false;
        }
        _selection = RichTextEditorSelection.Caret(origin, 0);
        var r0 = _blocks[origin].Row;
        var c0 = _blocks[origin].Col;
        var lastRow = r0;
        var lastCol = c0;
        var done = EditTable((grid, _, _, _) =>
        {
            var rows = Math.Min(r0 + source.Count, MaxTableRows);
            var cols = Math.Min(c0 + source.Max(line => line.Count), MaxTableColumns);
            while (grid.Count < rows)
            {
                var at = grid.Count;
                RichTextEditorTables.InsertRow(grid, at, c => RichTextEditorBlock.Cell(at, c).WithAttrs(new RichTextEditorBlockAttrs(Align: grid[0][c].Align, Row: at, Col: c)));
            }
            while (grid[0].Count < cols)
            {
                var at = grid[0].Count;
                RichTextEditorTables.InsertColumn(grid, at, _ => RichTextEditorBlock.Cell(0, at));
            }
            for (var r = 0; r < source.Count && r0 + r < rows; r++)
            {
                for (var c = 0; c < source[r].Count && c0 + c < cols; c++)
                {
                    var target = grid[r0 + r][c0 + c];
                    if (target.IsCovered)
                    {
                        continue;
                    }
                    grid[r0 + r][c0 + c] = RichTextEditorBlock.Create(RichTextBlockType.TableCell, 0, source[r][c], target.Attrs);
                    (lastRow, lastCol) = (r0 + r, c0 + c);
                }
            }
            return (lastRow, lastCol);
        });
        if (done)
        {
            var table = RichTextEditorTables.Extent(_blocks, origin);
            var last = table.IndexOf(lastRow, lastCol);
            SetSelection(RichTextEditorSelection.Caret(last, _blocks[last].Length));
        }
        return done;
    }

    /// <summary>True when the caret's row can move up (<paramref name="delta"/> -1) or down (1) past its neighbour.</summary>
    public bool CanMoveTableRow(int delta)
    {
        if (!Docs || !IsInTable)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block);
        var cell = _blocks[_selection.Focus.Block];
        return RichTextEditorTables.CanMoveRow((r, c) => _blocks[table.IndexOf(r, c)], table.Rows, table.Cols, cell.Row, delta < 0 ? cell.Row - 1 : cell.Row + 2);
    }

    /// <summary>True when the caret's column can move left (<paramref name="delta"/> -1) or right (1) past its neighbour.</summary>
    public bool CanMoveTableColumn(int delta)
    {
        if (!Docs || !IsInTable)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block);
        var cell = _blocks[_selection.Focus.Block];
        return RichTextEditorTables.CanMoveColumn((r, c) => _blocks[table.IndexOf(r, c)], table.Rows, table.Cols, cell.Col, delta < 0 ? cell.Col - 1 : cell.Col + 2);
    }

    /// <summary>
    /// Moves row <paramref name="row"/> of the table at the caret to the gap <paramref name="to"/> (0 is above the first row); refused when a
    /// merged cell spans rows through the row or across the gap. The caret stays in its cell.
    /// </summary>
    public bool MoveTableRow(int row, int to)
    {
        if (!Docs || !IsInTable)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block);
        if (!RichTextEditorTables.CanMoveRow((r, c) => _blocks[table.IndexOf(r, c)], table.Rows, table.Cols, row, to))
        {
            return false;
        }
        return EditTable((grid, _, caretRow, caretCol) =>
        {
            RichTextEditorTables.MoveRow(grid, row, to);
            return (RichTextEditorTables.Moved(caretRow, row, to), caretCol);
        });
    }

    /// <summary>Moves column <paramref name="col"/> of the table at the caret to the gap <paramref name="to"/>, with its width; as <see cref="MoveTableRow"/> for rows.</summary>
    public bool MoveTableColumn(int col, int to)
    {
        if (!Docs || !IsInTable)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block);
        if (!RichTextEditorTables.CanMoveColumn((r, c) => _blocks[table.IndexOf(r, c)], table.Rows, table.Cols, col, to))
        {
            return false;
        }
        return EditTable((grid, _, caretRow, caretCol) =>
        {
            RichTextEditorTables.MoveColumn(grid, col, to);
            return (caretRow, RichTextEditorTables.Moved(caretCol, col, to));
        }, columns: widths =>
        {
            var list = widths.ToList();
            var moved = list[col];
            list.RemoveAt(col);
            list.Insert(to > col ? to - 1 : to, moved);
            return list.ToArray();
        });
    }

    /// <summary>Selects every cell of row <paramref name="row"/> (or column <paramref name="col"/> when <paramref name="row"/> is negative) of the table holding <paramref name="cell"/>.</summary>
    public void SelectTableCells(int cell, int row, int col)
    {
        if (cell < 0 || cell >= _blocks.Count || !_blocks[cell].IsCell)
        {
            return;
        }
        var table = RichTextEditorTables.Extent(_blocks, cell);
        var (from, to) = row >= 0
            ? (RichTextEditorTables.OwnerIndex(_blocks, table, row, 0), RichTextEditorTables.OwnerIndex(_blocks, table, row, table.Cols - 1))
            : (RichTextEditorTables.OwnerIndex(_blocks, table, 0, col), RichTextEditorTables.OwnerIndex(_blocks, table, table.Rows - 1, col));
        SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(from, 0), new RichTextEditorPosition(to, _blocks[to].Length)));
    }

    /// <summary>Deletes the whole table the caret is in.</summary>
    public bool DeleteTable()
    {
        if (!IsInTable)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, _selection.Focus.Block);
        var blocks = _blocks.RemoveRange(table.Start, table.End - table.Start);
        if (blocks.Count == 0)
        {
            blocks = blocks.Add(RichTextEditorBlock.EmptyParagraph);
        }
        var caret = NearestTextual(blocks, Math.Min(table.Start, blocks.Count - 1), forward: true);
        Commit(blocks, RichTextEditorSelection.Caret(caret), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }

    /// <summary>Rebuilds the caret's table from a grid of rows that <paramref name="edit"/> changes; it returns the cell the caret goes to.</summary>
    /// <param name="edit">Changes the grid and returns where the caret goes.</param>
    /// <param name="max">Says whether the table may change at all.</param>
    /// <param name="columns">Reorders the column widths when <paramref name="edit"/> reorders the columns.</param>
    private bool EditTable(Func<List<List<RichTextEditorBlock>>, RichTextEditorTableExtent, int, int, (int Row, int Col)> edit, Func<RichTextEditorTableExtent, bool>? max = null,
        Func<int[], int[]>? columns = null)
    {
        var index = _selection.Focus.Block;
        if (!Docs || !_blocks[index].IsCell)
        {
            return false;
        }
        var table = RichTextEditorTables.Extent(_blocks, index);
        if (max is not null && !max(table))
        {
            return false;
        }
        var cell = _blocks[index];
        var grid = new List<List<RichTextEditorBlock>>(table.Rows);
        for (var r = 0; r < table.Rows; r++)
        {
            var cells = new List<RichTextEditorBlock>(table.Cols);
            for (var c = 0; c < table.Cols; c++)
            {
                cells.Add(_blocks[table.IndexOf(r, c)]);
            }
            grid.Add(cells);
        }
        // Column widths and the header flags belong to the table: they stay with their columns (a new column shares the free width) and
        // with the first cell, whichever cells end up there.
        var first = grid[0][0].Attrs;
        var widths = grid[0].Select(c => c.Width).ToArray();
        var (row, col) = edit(grid, table, cell.Row, cell.Col);
        (row, col) = RichTextEditorTables.Owner((r, c) => grid[r][c], row, col);
        if (grid.Count == 0)
        {
            return false;
        }
        if (grid[0].Count != widths.Length)
        {
            widths = grid[0].Select(c => c.Width).ToArray();
        }
        else if (columns is not null)
        {
            widths = columns(widths);
        }
        var flat = new List<RichTextEditorBlock>();
        for (var r = 0; r < grid.Count; r++)
        {
            for (var c = 0; c < grid[r].Count; c++)
            {
                var attrs = grid[r][c].Attrs with { Row = r, Col = c, Width = r == 0 ? widths[c] : 0 };
                if (r == 0 && c == 0)
                {
                    attrs = attrs with { NoHeaderRow = first.NoHeaderRow, HeaderColumn = first.HeaderColumn };
                }
                flat.Add(grid[r][c].WithAttrs(attrs));
            }
        }
        var blocks = _blocks.RemoveRange(table.Start, table.End - table.Start).InsertRange(table.Start, flat);
        // The caret keeps its offset in its own cell (wherever the cell moved to), and starts other cells at their beginning.
        var target = flat[row * grid[0].Count + col];
        var offset = row == cell.Row && col == cell.Col || target.Length == 0 || string.Equals(target.Text, cell.Text, StringComparison.Ordinal)
            ? Math.Min(_selection.Focus.Offset, flat[row * grid[0].Count + col].Length)
            : 0;
        Commit(blocks, RichTextEditorSelection.Caret(table.Start + row * grid[0].Count + col, offset), RichTextEditorHistoryKind.Command, true);
        _lastInput = InputKind.None;
        return true;
    }
}

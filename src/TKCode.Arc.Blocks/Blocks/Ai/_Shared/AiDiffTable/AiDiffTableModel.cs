namespace TKCode.Arc;

/// <summary>A column of an <see cref="AiDiffTableModel"/>: the new table's column and whether the old one had it.</summary>
/// <param name="Header">The header text.</param>
/// <param name="IsInserted">True when the old table has no column with this header.</param>
internal readonly record struct AiDiffTableColumn(string Header, bool IsInserted);

/// <summary>A cell of an <see cref="AiDiffTableModel"/>.</summary>
/// <param name="Text">The new text.</param>
/// <param name="IsInserted">True when the cell is new (new row or column) or its text changed.</param>
internal readonly record struct AiDiffTableCellModel(string Text, bool IsInserted);

/// <summary>
/// The change between two versions of a page table, as the side panel's change card shows it: the NEW table, with every
/// cell that is not in the old one marked as inserted. Columns are matched by header text, rows by the text of the row
/// header column (the first matched column marked <see cref="AiPanelColumn.IsRowHeader"/>, else the first matched column;
/// with no matched column, by position). A cell is inserted when its row or column is new or its text differs; removed
/// rows and columns are not shown, only counted. Pure data, computed once per pair of tables.
/// </summary>
internal sealed class AiDiffTableModel
{
    /// <summary>No table.</summary>
    public static AiDiffTableModel Empty { get; } = new([], [], [], 0, 0, 0, 0);

    private AiDiffTableModel(AiDiffTableColumn[] columns, AiDiffTableCellModel[][] rows, string[] addedColumns, int removedColumns,
                             int addedRows, int removedRows, int changedCells)
    {
        Columns = columns;
        Rows = rows;
        AddedColumns = addedColumns;
        RemovedColumns = removedColumns;
        AddedRows = addedRows;
        RemovedRows = removedRows;
        ChangedCells = changedCells;
        var inserted = 0;
        foreach (var row in rows)
        {
            foreach (var cell in row)
            {
                inserted += cell.IsInserted ? 1 : 0;
            }
        }
        InsertedCells = inserted;
    }

    /// <summary>The new table's columns.</summary>
    public IReadOnlyList<AiDiffTableColumn> Columns { get; }

    /// <summary>The new table's rows, one cell per column (missing cells are empty).</summary>
    public IReadOnlyList<IReadOnlyList<AiDiffTableCellModel>> Rows { get; }

    /// <summary>Headers of the columns the old table did not have.</summary>
    public IReadOnlyList<string> AddedColumns { get; }

    /// <summary>Columns of the old table that are gone.</summary>
    public int RemovedColumns { get; }

    /// <summary>Rows that are new.</summary>
    public int AddedRows { get; }

    /// <summary>Rows of the old table that are gone.</summary>
    public int RemovedRows { get; }

    /// <summary>Cells of kept rows and columns whose text changed.</summary>
    public int ChangedCells { get; }

    /// <summary>Body cells marked as inserted (header cells not counted).</summary>
    public int InsertedCells { get; }

    /// <summary>Whether anything differs.</summary>
    public bool HasChanges => AddedColumns.Count > 0 || RemovedColumns > 0 || AddedRows > 0 || RemovedRows > 0 || ChangedCells > 0;

    /// <summary>True when the only change is one new column (the card then says "Adds <i>header</i> column").</summary>
    public bool IsSingleAddedColumn => AddedColumns.Count == 1 && RemovedColumns == 0 && AddedRows == 0 && RemovedRows == 0 && ChangedCells == 0;

    /// <summary>Compares <paramref name="before"/> with <paramref name="after"/>; a missing old table makes every cell new.</summary>
    public static AiDiffTableModel Compare(AiPanelTable? before, AiPanelTable? after)
    {
        if (after is null)
        {
            return Empty;
        }
        var oldColumns = before?.Columns.ToArray() ?? [];
        var oldRows = before?.Rows.ToArray() ?? [];
        var newColumns = after.Columns.ToArray();

        // Columns by header, each old column used once.
        var columnMatch = new int[newColumns.Length];
        var usedColumns = new bool[oldColumns.Length];
        var added = new List<string>();
        for (var j = 0; j < newColumns.Length; j++)
        {
            columnMatch[j] = -1;
            for (var k = 0; k < oldColumns.Length; k++)
            {
                if (!usedColumns[k] && string.Equals(oldColumns[k].Header, newColumns[j].Header, StringComparison.Ordinal))
                {
                    columnMatch[j] = k;
                    usedColumns[k] = true;
                    break;
                }
            }
            if (columnMatch[j] < 0)
            {
                added.Add(newColumns[j].Header);
            }
        }
        var removedColumns = usedColumns.Count(used => !used);

        // The key column: a kept row header, else any kept column.
        var key = -1;
        for (var j = 0; j < newColumns.Length && key < 0; j++)
        {
            if (columnMatch[j] >= 0 && newColumns[j].IsRowHeader)
            {
                key = j;
            }
        }
        for (var j = 0; j < newColumns.Length && key < 0; j++)
        {
            if (columnMatch[j] >= 0)
            {
                key = j;
            }
        }

        var newRows = after.Rows.ToArray();
        var usedRows = new bool[oldRows.Length];
        var rows = new AiDiffTableCellModel[newRows.Length][];
        var addedRows = 0;
        var changedCells = 0;
        for (var i = 0; i < newRows.Length; i++)
        {
            var oldIndex = -1;
            if (key >= 0)
            {
                var text = CellText(newRows[i], key);
                for (var r = 0; r < oldRows.Length; r++)
                {
                    if (!usedRows[r] && string.Equals(CellText(oldRows[r], columnMatch[key]), text, StringComparison.Ordinal))
                    {
                        oldIndex = r;
                        break;
                    }
                }
            }
            else if (i < oldRows.Length && before is not null && oldColumns.Length > 0)
            {
                oldIndex = i;
            }
            if (oldIndex >= 0)
            {
                usedRows[oldIndex] = true;
            }
            else
            {
                addedRows++;
            }
            var cells = new AiDiffTableCellModel[newColumns.Length];
            for (var j = 0; j < newColumns.Length; j++)
            {
                var text = CellText(newRows[i], j);
                var inserted = oldIndex < 0 || columnMatch[j] < 0;
                if (!inserted && !string.Equals(CellText(oldRows[oldIndex], columnMatch[j]), text, StringComparison.Ordinal))
                {
                    inserted = true;
                    changedCells++;
                }
                cells[j] = new AiDiffTableCellModel(text, inserted);
            }
            rows[i] = cells;
        }
        var removedRows = usedRows.Count(used => !used);
        var columns = new AiDiffTableColumn[newColumns.Length];
        for (var j = 0; j < newColumns.Length; j++)
        {
            columns[j] = new AiDiffTableColumn(newColumns[j].Header, columnMatch[j] < 0);
        }
        return new AiDiffTableModel(columns, rows, added.ToArray(), removedColumns, addedRows, removedRows, changedCells);
    }

    private static string CellText(AiPanelRow row, int column) =>
        column >= 0 && column < row.Cells.Count ? row.Cells[column] ?? string.Empty : string.Empty;
}

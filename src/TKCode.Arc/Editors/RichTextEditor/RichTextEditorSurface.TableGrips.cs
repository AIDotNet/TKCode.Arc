using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

internal partial class RichTextEditorSurface
{
    /// <summary>The corner grip of the selected image at <paramref name="index"/>: dragging it resizes the picture.</summary>
    private Rect ResizeGripRect(int index)
    {
        var view = _views[index];
        var corner = new Point(view.ImageSize.Width, _tops[index] + view.ImageSize.Height);
        return new Rect(corner.X - ResizeGrip, corner.Y - ResizeGrip, ResizeGrip + 4, ResizeGrip + 4);
    }

    private bool OverResizeGrip(Point point, out int index)
    {
        index = -1;
        var engine = Engine;
        if (engine is not { IsImageSelected: true } || Owner is not { IsReadOnly: false })
        {
            return false;
        }
        index = engine.Selection.Focus.Block;
        return index < _views.Count && ResizeGripRect(index).Contains(point);
    }

    private void UpdateResize(double x)
    {
        _resizeWidth = Math.Clamp(Math.Round(_resizeStartWidth + x - _resizeStartX), 48, Math.Max(48, Bounds.Width));
        if (_resizeBlock is not null && _cache.TryGetValue(_resizeBlock, out var view))
        {
            view.StyleVersion = -1;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EndResize(bool commit)
    {
        var block = _resizeBlock;
        _resizeBlock = null;
        if (block is not null && commit && Engine is { } engine && _resizeIndex < engine.Blocks.Count && ReferenceEquals(engine.Blocks[_resizeIndex], block))
        {
            engine.SetImageWidth(_resizeIndex, (int)_resizeWidth);
        }
        else
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>The drawn column widths of <paramref name="table"/>, or the dragged ones while its border is being dragged.</summary>
    private double[] ColumnWidths(IReadOnlyList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, double width) =>
        _columnDrag is { } drag && drag.Start == table.Start && drag.Widths.Length == table.Cols
            ? drag.Widths
            : RichTextEditorTables.ColumnWidths(blocks, table, width);

    /// <summary>The table and inner column border under <paramref name="point"/> (border <c>b</c> lies between columns <c>b - 1</c> and <c>b</c>).</summary>
    internal (int Start, int Border)? ColumnBorderAt(Point point)
    {
        if (Engine is not { Docs: true } || Owner is not { IsReadOnly: false })
        {
            return null;
        }
        foreach (var table in _tables)
        {
            if (point.Y < table.Top || point.Y > table.Bottom)
            {
                continue;
            }
            for (var b = 1; b < table.Cols; b++)
            {
                if (Math.Abs(point.X - table.Offsets[b]) <= ColumnGrip)
                {
                    return (table.Start, b);
                }
            }
        }
        return null;
    }

    private void BeginColumnDrag(int start, int border, double x)
    {
        var table = _tables.First(t => t.Start == start);
        var widths = new double[table.Cols];
        for (var c = 0; c < table.Cols; c++)
        {
            widths[c] = table.Offsets[c + 1] - table.Offsets[c];
        }
        _columnDrag = (start, border, x, widths, (double[])widths.Clone());
    }

    private void UpdateColumnDrag(double x)
    {
        if (_columnDrag is not { } drag)
        {
            return;
        }
        var left = drag.StartWidths[drag.Border - 1];
        var right = drag.StartWidths[drag.Border];
        var min = Math.Min(RichTextEditorTables.MinColumnWidth, Math.Min(left, right));
        var delta = Math.Clamp(x - drag.StartX, min - left, right - min);
        drag.Widths[drag.Border - 1] = left + delta;
        drag.Widths[drag.Border] = right - delta;
        _laidOut = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EndColumnDrag(bool commit)
    {
        if (_columnDrag is not { } drag)
        {
            return;
        }
        _columnDrag = null;
        _laidOut = null;
        var moved = drag.Widths.Where((w, c) => Math.Abs(w - drag.StartWidths[c]) >= 0.5).Any();
        if (!commit || !moved || Engine is not { } engine || !engine.SetColumnWidths(drag.Start, drag.Widths.Select(w => (int)Math.Round(w)).ToArray()))
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>The table whose grips show: the one under the pointer, else the one holding the caret while the editor has focus.</summary>
    private TableBox? GripTable()
    {
        if (Engine is not { Docs: true } engine || Owner is not { IsReadOnly: false })
        {
            return null;
        }
        if (_gripDrag is { } drag)
        {
            return _tables.FirstOrDefault(t => t.Start == drag.Start) is { Rows: > 0 } dragged ? dragged : null;
        }
        if (_hoverTable >= 0)
        {
            return _tables[_hoverTable];
        }
        var focus = engine.Selection.Focus.Block;
        if (!IsKeyboardFocusWithin || focus >= engine.Blocks.Count || !engine.Blocks[focus].IsCell)
        {
            return null;
        }
        foreach (var table in _tables)
        {
            if (focus >= table.Start && focus < table.Start + table.Rows * table.Cols)
            {
                return table;
            }
        }
        return null;
    }

    /// <summary>The drawn pill of a column's grip (centred in the top of the first row) or a row's (in the left of the first column).</summary>
    private static Rect GripRect(TableBox table, bool column, int index) => column
        ? new Rect((table.Offsets[index] + table.Offsets[index + 1]) / 2 - 12, table.Top + 2, 24, 4)
        : new Rect(table.Offsets[0] + 2, (table.RowTops[index] + table.RowTops[index + 1]) / 2 - 12, 4, 24);

    /// <summary>The row or column grip under <paramref name="point"/> (its pill grown to an easy target).</summary>
    internal (int Start, bool Column, int Index)? GripAt(Point point)
    {
        if (GripTable() is not { } table)
        {
            return null;
        }
        for (var c = 0; c < table.Cols; c++)
        {
            if (GripRect(table, true, c).Inflate(new Thickness(4, 3, 4, 5)).Contains(point))
            {
                return (table.Start, true, c);
            }
        }
        for (var r = 0; r < table.Rows; r++)
        {
            if (GripRect(table, false, r).Inflate(new Thickness(3, 4, 5, 4)).Contains(point))
            {
                return (table.Start, false, r);
            }
        }
        return null;
    }

    /// <summary>The gap (0 before the first row or column) nearest to <paramref name="point"/> that the dragged row or column may move to, or -1.</summary>
    private int DropGap(GripDrag drag, Point point)
    {
        if (Engine is not { } engine || _tables.FirstOrDefault(t => t.Start == drag.Start) is not { Rows: > 0 } box)
        {
            return -1;
        }
        var edges = drag.Column ? box.Offsets : box.RowTops;
        var at = drag.Column ? point.X : point.Y;
        var gap = 0;
        for (var k = 1; k < edges.Length; k++)
        {
            if (Math.Abs(edges[k] - at) < Math.Abs(edges[gap] - at))
            {
                gap = k;
            }
        }
        var blocks = engine.Blocks;
        var table = RichTextEditorTables.Extent(blocks, drag.Start);
        RichTextEditorBlock At(int r, int c) => blocks[table.IndexOf(r, c)];
        var allowed = drag.Column
            ? RichTextEditorTables.CanMoveColumn(At, table.Rows, table.Cols, drag.Index, gap)
            : RichTextEditorTables.CanMoveRow(At, table.Rows, table.Cols, drag.Index, gap);
        return allowed ? gap : -1;
    }

    private void EndGripDrag(bool commit)
    {
        if (_gripDrag is not { } drag)
        {
            return;
        }
        _gripDrag = null;
        InvalidateVisual();
        if (!commit || Engine is not { } engine || Owner is not { } owner || drag.Start >= engine.Blocks.Count || !engine.Blocks[drag.Start].IsCell)
        {
            return;
        }
        var table = RichTextEditorTables.Extent(engine.Blocks, drag.Start);
        if (!drag.Moved)
        {
            engine.SelectTableCells(drag.Start, drag.Column ? -1 : drag.Index, drag.Column ? drag.Index : -1);
            owner.Announce(drag.Column ? "Column selected" : "Row selected");
            return;
        }
        if (drag.Gap < 0)
        {
            return;
        }
        // The caret goes into the moved row or column, so the move works on this table and the caret travels with it.
        var cell = drag.Column ? RichTextEditorTables.OwnerIndex(engine.Blocks, table, 0, drag.Index) : RichTextEditorTables.OwnerIndex(engine.Blocks, table, drag.Index, 0);
        engine.SetSelection(RichTextEditorSelection.Caret(cell, 0));
        var moved = drag.Column ? engine.MoveTableColumn(drag.Index, drag.Gap) : engine.MoveTableRow(drag.Index, drag.Gap);
        if (moved)
        {
            owner.Announce(drag.Column ? "Column moved" : "Row moved");
        }
    }

    /// <summary>The grips of the active table, and where a dragged row or column would land.</summary>
    private void DrawTableGrips(DrawingContext context)
    {
        if (GripTable() is not { } table)
        {
            return;
        }
        var rest = MutedBrush ?? RuleBrush;
        for (var c = 0; c < table.Cols; c++)
        {
            var brush = _gripDrag is { Column: true } d && d.Index == c ? AccentBrush : rest;
            context.DrawRectangle(brush, null, new RoundedRect(GripRect(table, true, c), 2));
        }
        for (var r = 0; r < table.Rows; r++)
        {
            var brush = _gripDrag is { Column: false } d && d.Index == r ? AccentBrush : rest;
            context.DrawRectangle(brush, null, new RoundedRect(GripRect(table, false, r), 2));
        }
        if (_gripDrag is { Moved: true, Gap: >= 0 } drag && AccentBrush is { } accent)
        {
            var line = drag.Column
                ? new Rect(Math.Clamp(table.Offsets[drag.Gap] - 1, 0, Math.Max(0, table.Offsets[^1] - 2)), table.Top, 2, table.Bottom - table.Top)
                : new Rect(table.Offsets[0], Math.Clamp(table.RowTops[drag.Gap] - 1, table.Top, Math.Max(table.Top, table.Bottom - 2)), table.Offsets[^1] - table.Offsets[0], 2);
            context.FillRectangle(accent, line);
        }
    }
}

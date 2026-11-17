using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class Table
{
    #region Columns and layout

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataTableColumn column in e.OldItems)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
                column.AppearanceChanged -= OnColumnAppearanceChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (DataTableColumn column in e.NewItems)
            {
                column.DefinitionChanged += OnColumnDefinitionChanged;
                column.AppearanceChanged += OnColumnAppearanceChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var state in _states)
            {
                state.Column.DefinitionChanged -= OnColumnDefinitionChanged;
                state.Column.AppearanceChanged -= OnColumnAppearanceChanged;
            }
            foreach (var column in Columns)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
                column.DefinitionChanged += OnColumnDefinitionChanged;
                column.AppearanceChanged -= OnColumnAppearanceChanged;
                column.AppearanceChanged += OnColumnAppearanceChanged;
            }
        }
        RebuildColumns();
    }

    private void OnColumnDefinitionChanged(object? sender, EventArgs e) => RebuildColumns();

    /// <summary>A header text or track width changed: the header relabels and the layout is measured again; rows keep their state.</summary>
    private void OnColumnAppearanceChanged(object? sender, AvaloniaProperty property)
    {
        if (sender is not DataTableColumn column)
        {
            return;
        }
        var index = Columns.IndexOf(column);
        if (property == DataTableColumn.HeaderProperty)
        {
            // The header is part of a column's natural width (and the sort-by name of its cell).
            _naturalLocked = false;
            if (index >= 0 && index < _states.Length)
            {
                _states[index].Natural = -1;
            }
            if (_presenter?.Header.CellFor(index) is { } cell)
            {
                cell.UpdateAutomation();
                cell.InvalidateMeasure();
                cell.InvalidateVisual();
            }
        }
        _presenter?.Header.InvalidateMeasure();
        _presenter?.InvalidateMeasure();
    }

    private void RebuildColumns()
    {
        foreach (var state in _states)
        {
            state.ValueProbe?.Release();
            state.SortProbe?.Release();
        }
        _states = Columns.Select(column => new ColumnState(column)).ToArray();
        _wraps = _states.Any(state => state.Column.Wraps);
        _wrapsTemplated = _states.Any(state => state.Column.Wraps && state.Column.CellTemplate is not null);
        _naturalLocked = false;
        UpdateCompactLead();
        _presenter?.Header.Rebuild();
        _presenter?.Reset();
        if (SortColumn is { } sorted && !Columns.Contains(sorted))
        {
            var replacement = Columns.FirstOrDefault(c => c.EffectiveKey == sorted.EffectiveKey);
            SetSortState(replacement, SortDirection);
        }
        else if (SortColumn is null && SortKey is { } key)
        {
            var match = Columns.FirstOrDefault(c => c.EffectiveKey == key);
            if (match is not null)
            {
                SetSortState(match, SortDirection == DataTableSortDirection.None ? DataTableSortDirection.Ascending : SortDirection);
            }
        }
        ApplySort(animate: false, captureFlip: false);
    }

    private void UpdateCompactLead()
    {
        _compactLead = -1;
        for (var i = 1; i < _states.Length; i++)
        {
            if (IsNumericColumn(i))
            {
                _compactLead = i;
                break;
            }
        }
    }

    /// <summary>Natural width of every column: header and the first rows, measured once and held.</summary>
    private void EnsureNatural()
    {
        if (_naturalLocked && _states.All(s => s.Natural >= 0))
        {
            return;
        }
        var header = _presenter?.Header;
        var sample = Math.Min(_items.Count, 40);
        for (var c = 0; c < _states.Length; c++)
        {
            var state = _states[c];
            var headerWidth = header?.CellFor(c)?.MeasureNaturalWidth() ?? 0;
            var width = headerWidth;
            var lead = LeadingPadding(c);
            for (var i = 0; i < sample; i++)
            {
                var source = _order.Length > i ? _order[i] : i;
                var text = new FormattedText(CellText(source, c), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Palette.Regular, Palette.FontSize, null);
                width = Math.Max(width, Math.Ceiling(text.WidthIncludingTrailingWhitespace + lead + CellInlinePadding));
            }
            if (state.Column.Wraps)
            {
                // Long text wraps rather than widening the table; the header still fits on one line.
                width = Math.Max(headerWidth, Math.Min(width, WrapColumnMaxWidth));
            }
            state.Natural = width;
            state.ValueProbe?.Release();
        }
        // Arc measures the natural widths once, then holds them so nothing reflows while rows move.
        _naturalLocked = _items.Count > 0;
    }

    internal double NaturalTableWidth()
    {
        EnsureNatural();
        var sum = IsSelectable ? SelectColumnWidth : 0;
        foreach (var state in _states)
        {
            sum += state.Column.Width.IsAbsolute ? state.Column.Width.Value : state.Natural;
        }
        return sum;
    }

    internal void ComputeColumns(TableLayout layout)
    {
        EnsureNatural();
        var count = _states.Length;
        var select = IsSelectable && !layout.Compact ? SelectColumnWidth : 0;
        layout.SelectWidth = select;
        var widths = new double[count];
        var xs = new double[count];
        var available = Math.Max(0, layout.Width - select);
        double fixedSum = 0, autoSum = 0, starSum = 0;
        for (var i = 0; i < count; i++)
        {
            var width = _states[i].Column.Width;
            if (width.IsAbsolute)
            {
                widths[i] = width.Value;
                fixedSum += width.Value;
            }
            else if (width.IsStar)
            {
                starSum += Math.Max(0, width.Value);
            }
            else
            {
                widths[i] = _states[i].Natural;
                autoSum += _states[i].Natural;
            }
        }
        var remaining = available - fixedSum - autoSum;
        if (starSum > 0)
        {
            var share = Math.Max(0, remaining);
            for (var i = 0; i < count; i++)
            {
                if (_states[i].Column.Width.IsStar)
                {
                    widths[i] = share * Math.Max(0, _states[i].Column.Width.Value) / starSum;
                }
            }
        }
        else if (autoSum > 0)
        {
            // Like a 100%-wide auto table: extra room (or a shortfall) is shared in proportion to natural width.
            var scale = Math.Max(0.1, (available - fixedSum) / autoSum);
            for (var i = 0; i < count; i++)
            {
                if (_states[i].Column.Width.IsAuto)
                {
                    widths[i] = _states[i].Natural * scale;
                }
            }
        }
        var x = select;
        for (var i = 0; i < count; i++)
        {
            xs[i] = x;
            x += widths[i];
        }
        layout.SetColumns(xs, widths);
    }

    #endregion

    #region Row heights (wrapping columns)

    private readonly record struct HeightKey(object? Columns, int Data, int Palette, bool Compact, double Width, double WrapWidths,
                                             double RowHeight, double Padding, bool Selectable);

    /// <summary>True when some column wraps, so rows have their own heights.</summary>
    internal bool HasWrappingColumns => _wraps;

    /// <summary>
    /// Brings the row heights and their offsets up to date for a layout pass. Tables without wrapping columns keep
    /// the uniform row height (no per-row work). Heights are measured from the cell text for every row, again only
    /// when the data, the fonts or a wrapping column's width change; a re-sort only re-adds them in the new order.
    /// </summary>
    internal void EnsureRowHeights(TableLayout layout)
    {
        var count = _items.Count;
        if (!_wraps || count == 0 || _order.Length != count)
        {
            layout.SetTops(null);
            _rowHeights = [];
            _topsOrder = null;
            return;
        }
        var wrapWidths = 0.0;
        for (var c = 0; c < _states.Length; c++)
        {
            if (_states[c].Column.Wraps)
            {
                wrapWidths += layout.ColumnWidth(c) * (c + 1);
            }
        }
        var key = new HeightKey(_states, DataVersion, Palette.Version, layout.Compact, layout.Compact ? layout.Width : 0,
            layout.Compact ? 0 : wrapWidths, layout.RowHeight,
            layout.Compact ? CompactCellBlockPadding : CellBlockPadding, IsSelectable);
        if (key != _heightKey || _rowHeights.Length != count)
        {
            _heightKey = key;
            var heights = new double[count];
            for (var i = 0; i < count; i++)
            {
                heights[i] = MeasureRowHeight(i, null, layout);
            }
            foreach (var state in _states)
            {
                state.ValueProbe?.Release();
            }
            _rowHeights = heights;
            _topsDirty = true;
        }
        if (_topsDirty || !ReferenceEquals(_topsOrder, _order) || layout.Tops is null)
        {
            var tops = new double[count + 1];
            for (var d = 0; d < count; d++)
            {
                tops[d + 1] = tops[d] + _rowHeights[_order[d]];
            }
            layout.SetTops(tops);
            _topsOrder = _order;
            _topsDirty = false;
        }
    }

    /// <summary>
    /// Takes the height of a realized row whose wrapping columns host templates (their content is only known once
    /// measured); true when it changed, after which <see cref="EnsureRowHeights"/> rebuilds the offsets.
    /// </summary>
    internal bool UpdateRowHeight(TableRow row, TableLayout layout)
    {
        var source = row.SourceIndex;
        if (!_wrapsTemplated || source < 0 || source >= _rowHeights.Length)
        {
            return false;
        }
        var height = MeasureRowHeight(source, row, layout);
        if (Math.Abs(height - _rowHeights[source]) < 0.5)
        {
            return false;
        }
        _rowHeights[source] = height;
        _topsDirty = true;
        return true;
    }

    /// <summary>Whether the compact layout has a details line (columns that neither lead nor wrap).</summary>
    internal bool HasCompactDetails()
    {
        for (var i = 1; i < _states.Length; i++)
        {
            if (!IsCompactLeadNumeric(i) && !WrapsAt(i, compact: true))
            {
                return true;
            }
        }
        return false;
    }

    internal double CompactLeft => IsSelectable ? 44.0 : 16.0;

    internal double CompactLine1 => Palette.FontSize * 1.43;

    internal double CompactLine2 => Palette.SmallFontSize * 1.45;

    /// <summary>
    /// Height of one row: the standard height, or more when a wrapped cell needs it (its content plus the block
    /// padding and the divider). Templated cells count only when the realized <paramref name="row"/> is given.
    /// </summary>
    private double MeasureRowHeight(int source, TableRow? row, TableLayout layout)
    {
        if (layout.Compact)
        {
            return MeasureCompactRowHeight(source, row, layout);
        }
        var height = layout.RowHeight;
        for (var c = 0; c < _states.Length; c++)
        {
            if (!_states[c].Column.Wraps)
            {
                continue;
            }
            double content;
            if (_states[c].Column.CellTemplate is not null)
            {
                content = row?.CellSize(c).Height ?? 0;
            }
            else
            {
                content = CreateCellLayout(CellText(source, c), c, compact: false, null,
                    layout.ColumnWidth(c) - LeadingPadding(c) - CellInlinePadding).Height;
            }
            height = Math.Max(height, Math.Ceiling(content + 2 * CellBlockPadding) + 1);
        }
        return height;
    }

    /// <summary>Compact rows: the first line (a wrapping first column grows it), the details line, then one block per wrapping column.</summary>
    private double MeasureCompactRowHeight(int source, TableRow? row, TableLayout layout)
    {
        var count = _states.Length;
        var room = Math.Max(1, layout.Width - 16 - CompactLeft);
        var lead = _compactLead > 0 && _compactLead < count ? _compactLead : -1;
        var leadWidth = 0.0;
        if (lead > 0)
        {
            double width;
            if (_states[lead].Column.CellTemplate is not null)
            {
                width = row?.CellSize(lead).Width ?? room / 2;
            }
            else
            {
                width = CreateCellLayout(CellText(source, lead), lead, compact: true, null, double.PositiveInfinity).WidthIncludingTrailingWhitespace;
            }
            leadWidth = Math.Min(width, room / 2) + 8;
        }
        var content = CompactLine1;
        if (count > 0 && WrapsAt(0, compact: true))
        {
            content = Math.Max(content, CompactBlockHeight(source, 0, row, Math.Max(1, room - leadWidth)));
        }
        if (HasCompactDetails())
        {
            content += 2 + CompactLine2;
        }
        for (var i = 1; i < count; i++)
        {
            if (WrapsAt(i, compact: true))
            {
                var block = CompactBlockHeight(source, i, row, room);
                if (block > 0)
                {
                    content += 2 + block;
                }
            }
        }
        return Math.Max(layout.RowHeight, Math.Ceiling(content + 2 * CompactCellBlockPadding) + 1);
    }

    private double CompactBlockHeight(int source, int column, TableRow? row, double width)
    {
        if (_states[column].Column.CellTemplate is not null)
        {
            return row?.CellSize(column).Height ?? 0;
        }
        var value = CellText(source, column);
        if (column > 0 && string.IsNullOrEmpty(value))
        {
            return 0;
        }
        return CreateCellLayout(value, column, compact: true, null, width).Height;
    }

    #endregion
}

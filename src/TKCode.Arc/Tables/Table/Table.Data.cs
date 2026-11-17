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
    #region Data access (presenter, rows and header)

    internal int RowCount => _items.Count;

    /// <summary>Changes whenever the source is re-read; rows refresh their cached text when it moves.</summary>
    internal int DataVersion { get; private set; }
    internal int ColumnCount => _states.Length;
    internal int CurrentRow => _currentRow;
    internal double TableWidthHint => _widthHint;
    internal bool IsCompact => Layout.Compact;
    internal int CompactLeadNumericIndex => _compactLead;
    internal TablePresenter? Presenter => _presenter;

    internal DataTableColumn? ColumnAt(int index) => index >= 0 && index < _states.Length ? _states[index].Column : null;

    internal int SourceAt(int displayIndex) => _order[displayIndex];

    internal int DisplayIndexOf(int sourceIndex) => sourceIndex >= 0 && sourceIndex < _position.Length ? _position[sourceIndex] : -1;

    internal object ItemAt(int sourceIndex) => _items[sourceIndex];

    internal object KeyAt(int sourceIndex) => _keys[sourceIndex];

    internal int SourceIndexOfKey(object key) => _indexByKey.TryGetValue(key, out var index) ? index : -1;

    internal bool IsSourceSelected(int sourceIndex) => IsSelectable && _selected.Contains(_keys[sourceIndex]);

    internal int SortedColumnIndex
    {
        get
        {
            if (SortDirection == DataTableSortDirection.None || SortColumn is not { } column)
            {
                return -1;
            }
            for (var i = 0; i < _states.Length; i++)
            {
                if (ReferenceEquals(_states[i].Column, column))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    internal double LeadingPadding(int columnIndex) =>
        columnIndex == 0 && IsSelectable ? PrimaryInsetSelectable : CellInlinePadding;

    internal bool IsCompactLeadNumeric(int columnIndex) => columnIndex == _compactLead && columnIndex > 0;

    private object? ValueOf(int sourceIndex, int columnIndex)
    {
        var state = _states[columnIndex];
        var item = _items[sourceIndex];
        if (state.Column.ValueSelector is { } selector)
        {
            return selector(item);
        }
        if (state.ValueProbe is { } probe)
        {
            return probe.Evaluate(item);
        }
        return state.Column.CellTemplate is null ? item : null;
    }

    private object? SortValueOf(int sourceIndex, int columnIndex)
    {
        var state = _states[columnIndex];
        var item = _items[sourceIndex];
        if (state.Column.SortValueSelector is { } selector)
        {
            return selector(item);
        }
        if (state.SortProbe is { } probe)
        {
            return probe.Evaluate(item);
        }
        return ValueOf(sourceIndex, columnIndex);
    }

    internal string CellText(int sourceIndex, int columnIndex) =>
        TableValues.Format(_states[columnIndex].Column, ValueOf(sourceIndex, columnIndex));

    /// <summary>True when the cells of a column span lines in the given layout (the compact lead number never does).</summary>
    internal bool WrapsAt(int columnIndex, bool compact) =>
        columnIndex >= 0 && columnIndex < _states.Length && _states[columnIndex].Column.Wraps
        && !(compact && IsCompactLeadNumeric(columnIndex));

    /// <summary>
    /// The drawn text of one cell at <paramref name="maxWidth"/> (rows cache and draw it, row heights are measured from
    /// it). A <see cref="TablesBText"/> keeps its shaped lines; the <c>FormattedText</c> it replaces re-shaped the cell on
    /// every draw, which the hover and selection tweens do each frame.
    /// </summary>
    internal TablesBText CreateCellLayout(string text, int columnIndex, bool compact, IBrush? brush, double maxWidth)
    {
        var primary = columnIndex == 0;
        var small = compact && !primary && !IsCompactLeadNumeric(columnIndex);
        var typeface = compact && primary ? Palette.Medium : Palette.Regular;
        var wraps = WrapsAt(columnIndex, compact);
        var maxLines = !wraps ? 1 : _states[columnIndex].Column.MaxLines is > 0 and var lines ? lines : 0;
        var numeric = IsNumericColumn(columnIndex);
        return new TablesBText(text, typeface, small ? Palette.SmallFontSize : Palette.FontSize, brush, maxWidth, maxLines,
            numeric && wraps && !compact ? TextAlignment.Right : TextAlignment.Left, numeric ? TablePalette.TabularNumbers : null);
    }

    /// <summary>Arc right-aligns a column when every non-empty value is a number (sampled for large sources).</summary>
    internal bool IsNumericColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _states.Length)
        {
            return false;
        }
        var state = _states[columnIndex];
        if (state.Column.IsNumeric is { } explicitValue)
        {
            return explicitValue;
        }
        if (state.Numeric is { } known)
        {
            return known;
        }
        var any = false;
        var all = true;
        var limit = Math.Min(_items.Count, 500);
        for (var i = 0; i < limit && all; i++)
        {
            var value = ValueOf(i, columnIndex);
            if (TableValues.IsNumber(value))
            {
                any = true;
            }
            else if (!TableValues.IsEmpty(value))
            {
                all = false;
            }
        }
        state.Numeric = any && all;
        state.ValueProbe?.Release();
        return state.Numeric.Value;
    }

    internal string RowName(TableRow row)
    {
        if (row.SourceIndex < 0 || row.SourceIndex >= _items.Count || _states.Length == 0
            || !ReferenceEquals(_items[row.SourceIndex], row.Item))
        {
            return string.Empty;
        }
        var text = CellText(row.SourceIndex, 0);
        return string.IsNullOrEmpty(text) ? Convert.ToString(row.Key, CultureInfo.CurrentCulture) ?? string.Empty : text;
    }

    #endregion
}
